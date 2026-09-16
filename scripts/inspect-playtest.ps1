# Read-only diagnostic. Does not validate visual layout or human enjoyment.
[CmdletBinding()]
param(
    [string]$RunsDirectory = (Join-Path $env:USERPROFILE 'AppData/LocalLow/PetThem/PET THEM!/runs'),
    [ValidateRange(1, 50)][int]$Latest = 5
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $RunsDirectory -PathType Container)) {
    throw "Run directory does not exist: $RunsDirectory"
}
$files = @(Get-ChildItem -LiteralPath $RunsDirectory -Filter '*.jsonl' -File |
    Sort-Object LastWriteTime -Descending | Select-Object -First $Latest)
$results = @(
    foreach ($file in $files) {
        if ($file.Length -gt 64MB) { throw "Run exceeds 64 MB limit: $($file.Name)" }
        $warnings = New-Object 'System.Collections.Generic.List[string]'
        $records = New-Object 'System.Collections.Generic.List[object]'
        $lineNumber = 0
        foreach ($line in (Get-Content -LiteralPath $file.FullName -Encoding UTF8)) {
            $lineNumber++
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            try { $records.Add(($line | ConvertFrom-Json -ErrorAction Stop)) }
            catch { $warnings.Add("Unreadable JSON at line $lineNumber; an active writer may not have finished it.") }
        }
        $headers = @($records | Where-Object type -eq 'run_start')
        $header = $headers | Select-Object -First 1
        if ($headers.Count -ne 1) { $warnings.Add("Expected one run_start, found $($headers.Count).") }
        $ends = @($records | Where-Object type -eq 'run_end')
        $end = $ends | Select-Object -Last 1
        if ($ends.Count -eq 0) { $warnings.Add('No terminal event: still running or incomplete, not evidence of death.') }
        if ($ends.Count -gt 1) { $warnings.Add('Multiple terminal events.') }
        $snapshots = @($records | Where-Object type -eq 'snapshot')
        $lastSnapshot = $snapshots | Select-Object -Last 1
        $punches = @($records | Where-Object { $_.type -eq 'attack' -and $_.source -eq 'punch' })
        $petAttacks = @($records | Where-Object { $_.type -eq 'attack' -and $_.source -eq 'pet' })
        $kills = @($records | Where-Object type -eq 'kill')
        $hurt = @($records | Where-Object type -eq 'hurt')
        $damage = ($hurt | Measure-Object -Property value -Sum).Sum
        if ($null -eq $damage) { $damage = 0 }
        if ($ends.Count -eq 1 -and $end.value -ne $kills.Count) { $warnings.Add('Terminal kill total differs from kill events.') }
        $positions = @($snapshots | ForEach-Object {
            '{0:R},{1:R}' -f [double]$_.x, [double]$_.y
        } | Select-Object -Unique)
        $health = if ($null -ne $end) { $end.health } elseif ($null -ne $lastSnapshot) { $lastSnapshot.health } else { $null }
        $seconds = if ($null -ne $end) { $end.time } elseif ($null -ne $lastSnapshot) { $lastSnapshot.time } else { $null }
        [PSCustomObject]@{
            file = $file.Name
            declaredSource = $header.source
            platform = $header.platform
            startedUtc = $header.startedUtc
            termination = if ($null -ne $end) { $end.source } else { 'active_or_incomplete' }
            recordedSeconds = $seconds
            recordCount = $records.Count
            snapshotCount = $snapshots.Count
            distinctRecordedPositions = $positions.Count
            punchAttacks = $punches.Count
            petAttacks = $petAttacks.Count
            kills = $kills.Count
            damageTaken = $damage
            finalRecordedHealth = $health
            warnings = @($warnings.ToArray())
        }
    }
)
[PSCustomObject]@{
    schemaVersion = '1'
    inspectedUtc = [DateTime]::UtcNow.ToString('o')
    scope = 'Local Unity-client records. Source is the writer label, not independent proof of who operated it.'
    limitations = @(
        'Movement samples and attacks do not establish simultaneous input or exact key presses.',
        'These records cannot establish pause-button behavior, visual readability, or enjoyment.',
        'Abandoned records are not deaths or completed three-minute survival attempts.',
        'An empty warnings list means only that these narrow log checks passed.'
    )
    runs = $results
} | ConvertTo-Json -Depth 8
