$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    dotnet run --project tools/CoreChecks -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Combat core checks failed.' }

    # Compiles Assets/Scripts and Assets/Editor against the real Unity assemblies. Skipped when
    # Unity is not installed, because the combat rules are checkable without it and a missing
    # editor should not stop that. An Android build has failed on an editor script that this
    # would have caught, so it runs here rather than only before a build.
    $unity = if ($env:UNITY_EDITOR_ROOT) { $env:UNITY_EDITOR_ROOT }
             else { 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1' }
    if (Test-Path (Join-Path $unity 'Editor/Data/Managed/UnityEngine')) {
        dotnet build tools/UnitySourceCheck -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Unity scripts failed to compile.' }
    } else {
        Write-Output "Unity not found at $unity - skipping the Unity script compile."
        Write-Output 'Set UNITY_EDITOR_ROOT to run it.'
    }

    git diff --check
    if ($LASTEXITCODE -ne 0) { throw 'Whitespace check failed.' }
} finally {
    Pop-Location
}
