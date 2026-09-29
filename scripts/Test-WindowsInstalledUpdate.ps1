param(
    [Parameter(Mandatory = $true)][string]$FromTag,
    [Parameter(Mandatory = $true)][string]$ToTag,
    [Parameter(Mandatory = $true)][string]$Repository,
    [Parameter(Mandatory = $true)][string]$WorkingRoot
)

$ErrorActionPreference = "Stop"
$fromDirectory = Join-Path $WorkingRoot "from"
$toDirectory = Join-Path $WorkingRoot "to"
New-Item -ItemType Directory -Force -Path $fromDirectory, $toDirectory | Out-Null

gh release download $FromTag --repo $Repository --pattern "*-setup.exe" --dir $fromDirectory
if ($LASTEXITCODE -ne 0) { throw "Could not download release $FromTag." }
gh release download $ToTag --repo $Repository --pattern "*-setup.exe" --pattern "*-setup.exe.sig" --pattern "latest.json" --dir $toDirectory
if ($LASTEXITCODE -ne 0) { throw "Could not download release $ToTag." }

$oldInstaller = Get-ChildItem $fromDirectory -Filter "*-setup.exe" | Select-Object -First 1
$newInstaller = Get-ChildItem $toDirectory -Filter "*-setup.exe" | Select-Object -First 1
$signatureFile = Get-ChildItem $toDirectory -Filter "*-setup.exe.sig" | Select-Object -First 1
$manifestPath = Join-Path $toDirectory "latest.json"
if (-not $oldInstaller -or -not $newInstaller -or -not $signatureFile -or -not (Test-Path $manifestPath)) {
    throw "Release assets required for installed update acceptance are missing."
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$expectedVersion = $ToTag.TrimStart('v')
if ($manifest.version -ne $expectedVersion) {
    throw "Manifest version '$($manifest.version)' does not match '$expectedVersion'."
}
$platform = $manifest.platforms.'windows-x86_64'
if (-not $platform) { throw "latest.json does not contain windows-x86_64 metadata." }
$signature = (Get-Content $signatureFile.FullName -Raw).Trim()
if ($platform.signature -ne $signature) {
    throw "latest.json signature does not match the published detached signature."
}
$response = Invoke-WebRequest -Uri $platform.url -Method Head -MaximumRedirection 5
if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 400) {
    throw "Updater package URL is not reachable: $($response.StatusCode)."
}

$installDirectory = Join-Path $WorkingRoot "RpaDevAssistantUpgrade"
$userDataDirectory = Join-Path $env:LOCALAPPDATA "RpaDevAssistant"
$marker = Join-Path $userDataDirectory "update-acceptance.marker"
New-Item -ItemType Directory -Force -Path $userDataDirectory | Out-Null
Set-Content -Path $marker -Value "preserve-me" -NoNewline

$oldInstall = Start-Process -FilePath $oldInstaller.FullName -ArgumentList "/S", "/D=$installDirectory" -Wait -PassThru
if ($oldInstall.ExitCode -ne 0) { throw "Previous release installation failed with exit code $($oldInstall.ExitCode)." }
$desktopExecutable = Join-Path $installDirectory "rpadevassistant-desktop.exe"
if (-not (Test-Path $desktopExecutable)) { throw "Previous desktop executable was not installed." }
$oldHash = (Get-FileHash $desktopExecutable -Algorithm SHA256).Hash

Get-Process "rpadevassistant-desktop" -ErrorAction SilentlyContinue | Stop-Process -Force
$newInstall = Start-Process -FilePath $newInstaller.FullName -ArgumentList "/S", "/D=$installDirectory" -Wait -PassThru
if ($newInstall.ExitCode -ne 0) { throw "Updated release installation failed with exit code $($newInstall.ExitCode)." }
if (-not (Test-Path $desktopExecutable)) { throw "Updated desktop executable is missing." }

$newHash = (Get-FileHash $desktopExecutable -Algorithm SHA256).Hash
if ($newHash -eq $oldHash) { throw "The installed desktop binary was not replaced during upgrade." }
if ((Get-Content $marker -Raw) -ne "preserve-me") { throw "Application user data was not preserved during upgrade." }
$installedVersion = (Get-Item $desktopExecutable).VersionInfo.ProductVersion
if (-not $installedVersion.StartsWith($expectedVersion)) {
    throw "Installed product version '$installedVersion' does not match '$expectedVersion'."
}
$sidecar = Get-ChildItem $installDirectory -Recurse -Filter "RpaDevAssistant.Api*.exe" | Select-Object -First 1
if (-not $sidecar) { throw "The updated backend sidecar is missing." }

Write-Host "Accepted installed upgrade $FromTag -> $ToTag"
Write-Host "Installed desktop: $desktopExecutable"
Write-Host "Installed sidecar: $($sidecar.FullName)"
