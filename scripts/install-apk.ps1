# Installs the development APK onto a USB-connected Android phone.
#
#   ./scripts/install-apk.ps1
#
# The phone needs USB debugging on (Settings > Developer options) and has to have
# accepted this computer's debugging key. adb ships inside the Unity editor install,
# so nothing extra has to be downloaded.
[CmdletBinding()]
param(
    [string]$Apk = (Join-Path (Split-Path $PSScriptRoot -Parent) 'game/Builds/PetThem-development.apk'),
    [string]$Editor = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1'
)

$ErrorActionPreference = 'Stop'
$adb = Join-Path $Editor 'Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe'
if (-not (Test-Path $adb)) { throw "adb not found at $adb. Check the editor path." }
if (-not (Test-Path $Apk)) {
    throw "No APK at $Apk. Build it first: Unity menu PET THEM > Build Android development APK"
}

$built = (Get-Item $Apk).LastWriteTime
Write-Output "APK    : $Apk"
Write-Output "Built  : $built"
if ($built -lt (Get-Date).AddHours(-12)) {
    Write-Warning "This APK is more than 12 hours old. Rebuild it if you expect recent changes."
}

$devices = & $adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '\S' }
$ready = @($devices | Where-Object { $_ -match '\sdevice$' })
if ($ready.Count -eq 0) {
    Write-Output ''
    Write-Output ($devices -join "`n")
    throw @'
No phone is ready. Check that:
  - the cable carries data, not just power
  - USB debugging is on in Developer options
  - the "Allow USB debugging?" prompt on the phone was accepted
A line ending in "unauthorized" means the prompt is still waiting on the phone.
'@
}
Write-Output "Device : $($ready[0])"

# -r replaces an existing install and keeps the save file. -d allows going back to an
# older build, which otherwise fails with INSTALL_FAILED_VERSION_DOWNGRADE.
Write-Output ''
& $adb install -r -d $Apk
if ($LASTEXITCODE -ne 0) { throw "adb install failed with exit code $LASTEXITCODE." }

Write-Output ''
Write-Output 'Installed. Launching...'
& $adb shell monkey -p com.petthem.game -c android.intent.category.LAUNCHER 1 | Out-Null
Write-Output 'PET THEM! should be running on the phone now.'
Write-Output ''
Write-Output 'To watch the game log while you play:'
Write-Output "  & '$adb' logcat -s Unity"
