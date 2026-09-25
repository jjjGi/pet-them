# Measures whether the same fight comes out the same in two runtimes.
#
# Replay validation rests on it: the client would upload a seed and its inputs, the server would
# re-simulate and work out the score itself. If the two disagree, honest players get rejected.
#
# Writes two transcripts of one fixed fight, and two summaries of forty, then compares them.
# The transcripts record every float as raw bits, so a formatting difference cannot be mistaken
# for a divergence or hide one.
#
# This compares the editor's runtime against the server's. It does not reach a phone, where the
# answer may differ again: IL2CPP compiles to C++ and ARM64 is not x64. Two of three runtimes is
# what can be had without a device.

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $builds = Join-Path $projectRoot 'game/Builds'

    Write-Output '--- .NET ---'
    dotnet run --project tools/CoreChecks -c Release | Select-String 'Determinism transcript'
    if ($LASTEXITCODE -ne 0) { throw 'The .NET transcript failed.' }

    Write-Output '--- Unity ---'
    $unity = if ($env:UNITY_EDITOR_ROOT) { $env:UNITY_EDITOR_ROOT }
             else { 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1' }
    $editor = Join-Path $unity 'Editor/Unity.exe'
    if (-not (Test-Path $editor)) { throw "Unity not found at $unity. Set UNITY_EDITOR_ROOT." }
    $log = Join-Path $env:TEMP 'petthem-determinism.log'
    $run = Start-Process -FilePath $editor -PassThru -Wait -ArgumentList `
        '-batchmode','-quit','-projectPath',(Join-Path $projectRoot 'game'), `
        '-executeMethod','PetThem.Editor.DeterminismTranscript.Write','-logFile',$log
    if ($run.ExitCode -ne 0) { throw "Unity exited $($run.ExitCode). See $log." }
    Select-String -Path $log -Pattern 'Determinism transcript' | ForEach-Object { $_.Line }

    Write-Output ''
    Write-Output '--- one fight, every event ---'
    $a = Join-Path $builds 'determinism-dotnet.txt'
    $b = Join-Path $builds 'determinism-unity.txt'
    $lines = (Get-Content $a).Count
    $differing = (Compare-Object (Get-Content $a) (Get-Content $b) -SyncWindow 0 |
        Where-Object { $_.SideIndicator -eq '<=' }).Count
    if ($differing -eq 0) { Write-Output "identical, $lines lines" }
    else { Write-Output "$differing of $lines lines differ" }

    Write-Output ''
    Write-Output '--- forty fights, endings only ---'
    # The question the design turns on. Exact agreement is already known to be absent; what
    # matters is how often a last-bit difference grows into a different result.
    $x = Get-Content (Join-Path $builds 'outcomes-dotnet.txt')
    $y = Get-Content (Join-Path $builds 'outcomes-unity.txt')
    $same = 0
    for ($i = 0; $i -lt $x.Count; $i++) { if ($x[$i] -eq $y[$i]) { $same++ } }
    Write-Output "$same of $($x.Count) end the same"
    for ($i = 0; $i -lt $x.Count; $i++) {
        if ($x[$i] -ne $y[$i]) { Write-Output "  .NET  $($x[$i])"; Write-Output "  Unity $($y[$i])" }
    }
} finally {
    Pop-Location
}
