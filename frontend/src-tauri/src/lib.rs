use std::{
    fs,
    net::TcpListener,
    path::{Path, PathBuf},
    process::Command,
    sync::Mutex,
    time::Duration,
};

use tauri::{Manager, State};
use tauri_plugin_shell::{process::CommandChild, ShellExt};

#[derive(Clone, serde::Serialize)]
#[serde(rename_all = "camelCase")]
struct StartupContext {
    project_path: Option<String>,
    workflow_path: Option<String>,
}

struct BackendState {
    port: u16,
    child: Mutex<Option<CommandChild>>,
}

impl BackendState {
    fn base_url(&self) -> String {
        format!("http://127.0.0.1:{}", self.port)
    }
}

#[tauri::command]
fn backend_base_url(state: State<'_, BackendState>) -> String {
    state.base_url()
}

#[tauri::command]
fn startup_context(state: State<'_, StartupContext>) -> StartupContext {
    state.inner().clone()
}

#[tauri::command]
fn write_report_file(path: String, contents: String) -> Result<(), String> {
    log::info!("Writing exported report to selected path");
    fs::write(path, contents).map_err(|error| format!("Report file could not be written: {error}"))
}

#[tauri::command]
fn open_workflow_in_studio(project_path: String, workflow_path: String) -> Result<(), String> {
    let project_root = fs::canonicalize(&project_path)
        .map_err(|_| "The selected UiPath project folder could not be found.".to_string())?;
    if !project_root.join("project.json").is_file() {
        return Err("The selected folder is not a UiPath project.".to_string());
    }

    let relative_workflow = Path::new(&workflow_path);
    if relative_workflow.is_absolute() || workflow_path.trim().is_empty() {
        return Err("Workflow path must be relative to the selected project.".to_string());
    }
    let workflow = fs::canonicalize(project_root.join(relative_workflow))
        .map_err(|_| "The selected workflow file could not be found.".to_string())?;
    if !is_path_within(&workflow, &project_root)
        || workflow.extension().and_then(|value| value.to_str()).map(|value| value.eq_ignore_ascii_case("xaml")) != Some(true)
    {
        return Err("Only XAML workflows inside the selected project can be opened.".to_string());
    }

    launch_with_default_application(&workflow)
}

fn is_path_within(path: &Path, root: &Path) -> bool {
    #[cfg(target_os = "windows")]
    {
        return path.to_string_lossy().to_lowercase().starts_with(&format!("{}\\", root.to_string_lossy().trim_end_matches(['\\', '/']).to_lowercase()))
            || path.to_string_lossy().eq_ignore_ascii_case(&root.to_string_lossy());
    }
    #[cfg(not(target_os = "windows"))]
    path.starts_with(root)
}

fn launch_with_default_application(path: &PathBuf) -> Result<(), String> {
    #[cfg(target_os = "windows")]
    let mut command = {
        let mut value = Command::new("explorer.exe");
        value.arg(path);
        value
    };
    #[cfg(target_os = "macos")]
    let mut command = {
        let mut value = Command::new("open");
        value.arg(path);
        value
    };
    #[cfg(all(not(target_os = "windows"), not(target_os = "macos")))]
    let mut command = {
        let mut value = Command::new("xdg-open");
        value.arg(path);
        value
    };

    command.spawn()
        .map(|_| ())
        .map_err(|error| format!("UiPath Studio could not be opened: {error}"))
}

pub fn run() {
    let backend_port = find_available_local_port();
    let startup = parse_startup_context(std::env::args().skip(1));

    tauri::Builder::default()
        .plugin(tauri_plugin_log::Builder::new().build())
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_process::init())
        .plugin(tauri_plugin_shell::init())
        .plugin(tauri_plugin_updater::Builder::new().build())
        .manage(BackendState {
            port: backend_port,
            child: Mutex::new(None),
        })
        .manage(startup)
        .setup(move |app| {
            start_backend_sidecar(app, backend_port)?;
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![backend_base_url, startup_context, write_report_file, open_workflow_in_studio])
        .on_window_event(|window, event| {
            if matches!(event, tauri::WindowEvent::CloseRequested { .. }) {
                shutdown_backend(window.app_handle());
            }
        })
        .run(tauri::generate_context!())
        .expect("error while running RPA Dev Assistant");
}

fn parse_startup_context(arguments: impl Iterator<Item = String>) -> StartupContext {
    let values = arguments.collect::<Vec<_>>();
    let value_after = |name: &str| values.iter().position(|value| value == name)
        .and_then(|index| values.get(index + 1))
        .filter(|value| !value.trim().is_empty() && !value.starts_with("--"))
        .cloned();
    StartupContext {
        project_path: value_after("--project"),
        workflow_path: value_after("--workflow"),
    }
}

#[cfg(test)]
mod tests {
    use super::parse_startup_context;

    #[test]
    fn parses_uipath_studio_external_tool_context() {
        let result = parse_startup_context([
            "--project".to_string(), "C:\\Rpa\\Project".to_string(),
            "--workflow".to_string(), "Business\\Login.xaml".to_string(),
        ].into_iter());
        assert_eq!(result.project_path.as_deref(), Some("C:\\Rpa\\Project"));
        assert_eq!(result.workflow_path.as_deref(), Some("Business\\Login.xaml"));
    }

    #[test]
    fn ignores_missing_startup_values() {
        let result = parse_startup_context(["--project".to_string(), "--workflow".to_string()].into_iter());
        assert!(result.project_path.is_none());
        assert!(result.workflow_path.is_none());
    }
}

fn start_backend_sidecar(app: &tauri::App, port: u16) -> tauri::Result<()> {
    let bind_url = format!("http://127.0.0.1:{port}");
    log::info!("Starting RPA Dev Assistant backend on {bind_url}");

    let (_events, child) = app
        .shell()
        .sidecar("RpaDevAssistant.Api")
        .map_err(|error| tauri::Error::Anyhow(anyhow::anyhow!("backend sidecar could not be prepared: {error}")))?
        .args(["--urls", bind_url.as_str()])
        .spawn()
        .map_err(|error| tauri::Error::Anyhow(anyhow::anyhow!("backend sidecar could not be started: {error}")))?;

    let state = app.state::<BackendState>();
    *state.child.lock().expect("backend state mutex poisoned") = Some(child);

    log::info!("RPA Dev Assistant backend started on port {port}");
    std::thread::sleep(Duration::from_millis(200));
    Ok(())
}

fn shutdown_backend(app: &tauri::AppHandle) {
    let state = app.state::<BackendState>();
    let child = state.child.lock().expect("backend state mutex poisoned").take();
    if let Some(child) = child {
        log::info!("Shutting down RPA Dev Assistant backend");
        if let Err(error) = child.kill() {
            log::warn!("Backend shutdown failed: {error}");
        }
    }
}

fn find_available_local_port() -> u16 {
    let listener = TcpListener::bind("127.0.0.1:0").expect("failed to reserve a local backend port");
    listener.local_addr().expect("failed to read local backend port").port()
}
