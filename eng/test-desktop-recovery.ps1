[CmdletBinding()]
param(
    [Parameter(Mandatory)][string[]]$ExecutablePath,
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [string]$SkipPassedResultsPath,
    [switch]$Resume
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$results = [Collections.Generic.List[object]]::new()
$root = [IO.Path]::GetFullPath($EvidenceRoot)
New-Item -ItemType Directory -Force -Path $root | Out-Null
$previous = @(if ($SkipPassedResultsPath) { Get-Content -LiteralPath $SkipPassedResultsPath -Raw | ConvertFrom-Json })
if (@($previous | Where-Object result -ne 'passed').Count) { throw 'Only passed scenarios may be skipped.' }
if ($Resume -and (Test-Path (Join-Path $root results.json))) {
    $retained = @(Get-Content (Join-Path $root results.json) -Raw | ConvertFrom-Json)
    if (@($retained | Where-Object result -ne 'passed').Count) { throw 'Only passed recovery scenarios may be resumed.' }
    foreach ($item in $retained) { $results.Add($item) }
    $previous += $retained
}

function Wait-File([string]$Path, [int]$Seconds = 30) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path -LiteralPath $Path)) {
        if ($watch.Elapsed.TotalSeconds -gt $Seconds) { throw "Timed out waiting for $Path" }
        Start-Sleep -Milliseconds 100
    }
    Start-Sleep -Milliseconds 80
}

function Invoke-Control([string]$Exe, [string]$Command) {
    $output = & $Exe --control $Command 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Control $Command failed: $output" }
    return $output.Trim()
}

foreach ($executable in $ExecutablePath) {
    $exe = (Resolve-Path -LiteralPath $executable).Path
    $identity = Split-Path (Split-Path $exe -Parent) -Leaf
    # Include executable name and unique scenario token to avoid evidence collisions.
    foreach ($mode in @('ui-fatal', 'worker-fatal', 'kill', 'restart', 'recoverable', 'clean-stop')) {
        if (@($previous | Where-Object { [IO.Path]::GetFileName($_.executable) -eq [IO.Path]::GetFileName($exe) -and (Split-Path (Split-Path $_.executable -Parent) -Leaf) -eq $identity -and $_.mode -eq $mode }).Count) {
            Write-Host "SKIP previously passed $exe $mode"
            continue
        }
        $directory = Join-Path $root (([IO.Path]::GetFileNameWithoutExtension($exe)) + '-' + $identity + '-' + $mode + '-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $directory | Out-Null
        $process = $null
        $status = ''
        $hostLog = Join-Path (Split-Path $exe -Parent) 'watchdog.log'
        $previousLogLines = @(if (Test-Path $hostLog) { Get-Content $hostLog }).Count
        try {
            $process = Start-Process -FilePath $exe -ArgumentList @('--recovery', ('"' + $directory + '"'), $mode) -WorkingDirectory (Split-Path $exe -Parent) -WindowStyle Hidden -RedirectStandardOutput (Join-Path $directory 'stdout.txt') -RedirectStandardError (Join-Path $directory 'stderr.txt') -PassThru
            Wait-File (Join-Path $directory 'initial.ready')
            $initialPid = [int](Get-Content (Join-Path $directory 'initial.ready') -Raw)
            if ($mode -in @('restart', 'clean-stop')) {
                $initialHelperPid = [int](Get-Content (Join-Path $directory 'initial.helper.pid') -Raw)
                if (-not (Get-Process -Id $initialHelperPid -ErrorAction SilentlyContinue)) { throw 'Test-owned child was not alive before forced termination.' }
            }
            if ((Get-Content (Join-Path $directory 'initial.windows') -Raw).Trim() -ne '1') { throw 'Initial main window count differs from one.' }
            if ($mode -eq 'kill') { Stop-Process -Id $initialPid -Force }
            if ($mode -eq 'restart') { $null = Invoke-Control $exe 'restart' }

            if ($mode -in @('ui-fatal', 'worker-fatal', 'kill', 'restart')) {
                Wait-File (Join-Path $directory 'replacement.ready')
                $replacementPid = [int](Get-Content (Join-Path $directory 'replacement.ready') -Raw)
                if ($replacementPid -eq $initialPid) { throw 'Watchdog did not create a new process.' }
                if ((Get-Content (Join-Path $directory 'replacement.windows') -Raw).Trim() -ne '1') { throw 'Replacement main window count differs from one.' }
                if ($mode -eq 'restart') {
                    if (Get-Process -Id $initialHelperPid -ErrorAction SilentlyContinue) { throw 'Forced attached-tree restart left its test-owned child alive.' }
                    $replacementHelperPid = [int](Get-Content (Join-Path $directory 'replacement.helper.pid') -Raw)
                    if (-not (Get-Process -Id $replacementHelperPid -ErrorAction SilentlyContinue)) { throw 'Replacement test-owned child did not start.' }
                }
            }
            else {
                if ($mode -eq 'recoverable') {
                    Wait-File (Join-Path $directory 'reports.txt')
                    Wait-File (Join-Path $directory 'recovered.signal')
                }
                Start-Sleep -Milliseconds 800
                if (Test-Path (Join-Path $directory 'replacement.ready')) { throw 'Recoverable/clean run restarted unexpectedly.' }
                if (-not (Get-Process -Id $initialPid -ErrorAction SilentlyContinue)) { throw 'Initial process disappeared unexpectedly.' }
            }

            $reportPath = Join-Path $directory 'reports.txt'
            $reports = @(if (Test-Path $reportPath) { Get-Content $reportPath })
            $expectedReports = if ($mode -in @('ui-fatal', 'worker-fatal', 'recoverable')) { 1 } else { 0 }
            if ($reports.Count -ne $expectedReports) { throw "Expected $expectedReports incident report(s), found $($reports.Count)." }
            if ($expectedReports -eq 1) {
                $expectedFlag = if ($mode -eq 'recoverable') { 'terminating=False' } else { 'terminating=True' }
                if (-not $reports[0].Contains($expectedFlag)) { throw "Wrong incident policy: $($reports[0])" }
                $expectedSource = if ($mode -eq 'worker-fatal') { 'AppDomain.UnhandledException' } elseif ([IO.Path]::GetFileName($exe).StartsWith('Wpf')) { 'Dispatcher.UnhandledException' } else { 'Application.ThreadException' }
                if (-not $reports[0].StartsWith($expectedSource + ' ')) { throw "Wrong incident integration source: $($reports[0])" }
                $bundles = @(Get-ChildItem (Join-Path $directory 'incidents') -Recurse -Filter crash.txt)
                if ($bundles.Count -ne 1) { throw "Expected exactly one incident bundle, found $($bundles.Count)." }
            }
            $status = Invoke-Control $exe 'status'
            $telemetry = $status | ConvertFrom-Json
            $expectedRestarts = if ($mode -in @('ui-fatal', 'worker-fatal', 'kill', 'restart')) { 1 } else { 0 }
            $expectedPid = if ($expectedRestarts -eq 1) { $replacementPid } else { $initialPid }
            if ($telemetry.state -ne 'running' -or $telemetry.restartCount -ne $expectedRestarts -or $telemetry.childPid -ne $expectedPid) { throw "Unexpected supervision status: $status" }
            if ($mode -in @('ui-fatal', 'worker-fatal') -and $telemetry.lastExitCode -ne 71) { throw "Fatal policy exit code differs from 71: $status" }
            $status | Set-Content (Join-Path $directory 'watchdog-status.txt')
            $null = Invoke-Control $exe 'stop'
            $watch = [Diagnostics.Stopwatch]::StartNew()
            do {
                Start-Sleep -Milliseconds 100
                $ownedFiles = @(Get-ChildItem $directory -Filter '*.ready') + @(Get-ChildItem $directory -Filter '*.helper.pid')
                $ownedPids = @($ownedFiles | ForEach-Object { [int](Get-Content $_.FullName -Raw) })
                $remaining = @($ownedPids | Where-Object { Get-Process -Id $_ -ErrorAction SilentlyContinue })
            } while ($remaining.Count -gt 0 -and $watch.Elapsed.TotalSeconds -lt 15)
            if ($remaining.Count -gt 0) { throw "Owned application processes survived stop: $remaining" }
            Start-Sleep -Milliseconds 400
            & $exe --control status *> (Join-Path $directory 'after-stop.txt')
            if ($LASTEXITCODE -eq 0) { throw 'Watchdog still answers after stop.' }
            $scenarioLog = @(Get-Content $hostLog | Select-Object -Skip $previousLogLines) -join "`n"
            $scenarioLog | Set-Content (Join-Path $directory 'watchdog.log')
            if ($mode -in @('restart', 'clean-stop')) {
                if (-not $scenarioLog.Contains('[kill] graceful_attempt') -or -not $scenarioLog.Contains('[kill] attached_tree_preserving_supervisor')) { throw 'Forced attached termination was not observed in Host logs.' }
                if ($mode -eq 'restart' -and -not $scenarioLog.Contains('[kill] taskkill_tree')) { throw 'Replacement tree termination did not execute the taskkill path.' }
            }
            $results.Add([pscustomobject]@{ executable = $exe; mode = $mode; result = 'passed'; evidence = $directory; status = $status })
            Write-Host "PASS $exe $mode"
        }
        catch {
            $results.Add([pscustomobject]@{ executable = $exe; mode = $mode; result = 'failed'; evidence = $directory; error = $_.ToString() })
            throw
        }
        finally {
            if ($process) {
                & $exe --control stop *> (Join-Path $directory 'cleanup.txt')
                $process.Dispose()
            }
            $results | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'results.json')
        }
    }
}
Write-Host "Desktop recovery passed: $($results.Count) scenarios."
$global:LASTEXITCODE = 0
