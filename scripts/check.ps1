$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    dotnet run --project tools/CoreChecks -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Combat core checks failed.' }
    dotnet build balance-lab/simulator -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Simulator build failed.' }
    git diff --check
    if ($LASTEXITCODE -ne 0) { throw 'Whitespace check failed.' }
} finally {
    Pop-Location
}
