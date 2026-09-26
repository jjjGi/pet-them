$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (Get-Process Unity -ErrorAction SilentlyContinue) {
    throw 'Close Unity yourself before running this batch check. No running editor will be stopped.'
}
$editorRoot = if ($env:UNITY_EDITOR_ROOT) { $env:UNITY_EDITOR_ROOT }
              else { 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1' }
$editor = Join-Path $editorRoot 'Editor/Unity.exe'
if (-not (Test-Path $editor)) { throw "Unity not found: $editor" }
if (Get-NetTCPConnection -LocalPort 5165 -State Listen -ErrorAction SilentlyContinue) {
    throw 'Port 5165 is already in use. This check requires its own server.'
}
$output = Join-Path $projectRoot 'experiments'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$database = Join-Path $output ('unity-transport-' + [guid]::NewGuid().ToString('N') + '.db')
$log = Join-Path $output 'server-transport.log'
$serverProcess = $null
$unityProcess = $null
Push-Location $projectRoot
try {
    dotnet build server/PetThem.Server -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Server build failed.' }
    $serverProcess = Start-Process dotnet -WindowStyle Hidden -PassThru -ArgumentList @(
        'server/PetThem.Server/bin/Release/net10.0/PetThem.Server.dll',
        '--urls=http://127.0.0.1:5165', ('"--Database=' + $database + '"')
    ) -RedirectStandardOutput (Join-Path $output 'unity-server.log') `
      -RedirectStandardError (Join-Path $output 'unity-server-error.log')
    $ready = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        if ($serverProcess.HasExited) { throw 'Fixture server exited before becoming ready.' }
        try {
            $health = Invoke-RestMethod 'http://127.0.0.1:5165/health' -TimeoutSec 1
            if ($health.status -eq 'ok') { $ready = $true; break }
        } catch { Start-Sleep -Milliseconds 250 }
    }
    if (-not $ready) { throw 'Fixture server did not become ready.' }
    $unityProcess = Start-Process $editor -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode', '-projectPath', ('"' + (Join-Path $projectRoot 'game') + '"'),
        '-executeMethod', 'PetThem.Editor.ServerTransportValidation.Verify', '-logFile', ('"' + $log + '"')
    )
    if (-not $unityProcess.WaitForExit(180000)) { throw 'Unity transport check timed out.' }
    if ($unityProcess.ExitCode -ne 0) { throw "Unity transport check failed. See $log" }
    $proof = Select-String -Path $log -Pattern 'UNITY HTTP VERIFIED:'
    if (-not $proof) { throw "Unity exited without verification evidence. See $log" }
    $proof | ForEach-Object { $_.Line }
} finally {
    if ($unityProcess -and -not $unityProcess.HasExited) { Stop-Process -Id $unityProcess.Id }
    if ($serverProcess -and -not $serverProcess.HasExited) { Stop-Process -Id $serverProcess.Id }
    Pop-Location
}
