use std::{
    fs,
    net::TcpListener,
    sync::Mutex,
    time::Duration,
};

use tauri::{Manager, State};
use tauri_plugin_shell::{process::CommandChild, ShellExt};

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
fn write_report_file(path: String, contents: String) -> Result<(), String> {
    log::info!("Writing exported report to selected path");
    fs::write(path, contents).map_err(|error| format!("Report file could not be written: {error}"))
}

pub fn run() {
    let backend_port = find_available_local_port();

    tauri::Builder::default()
        .plugin(tauri_plugin_log::Builder::new().build())
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_shell::init())
        .manage(BackendState {
            port: backend_port,
            child: Mutex::new(None),
        })
        .setup(move |app| {
            start_backend_sidecar(app, backend_port)?;
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![backend_base_url, write_report_file])
        .on_window_event(|window, event| {
            if matches!(event, tauri::WindowEvent::CloseRequested { .. }) {
                shutdown_backend(window.app_handle());
            }
        })
        .run(tauri::generate_context!())
        .expect("error while running RPA Dev Assistant");
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
