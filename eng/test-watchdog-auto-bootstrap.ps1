[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageVersion,
    [Parameter(Mandatory)][string]$SessionRoot,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root = [IO.Path]::GetFullPath((Join-Path $SessionRoot 'auto-bootstrap'))
$consumers = Join-Path $repo 'tests/NekoLib.PackageConsumers'
$results = [Collections.Generic.List[object]]::new()
New-Item -ItemType Directory -Force -Path $root | Out-Null

function Invoke-Build([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE) { throw "dotnet failed: $($Arguments -join ' ')" }
}
function Wait-File([string]$Path) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path -LiteralPath $Path)) {
        if ($watch.Elapsed.TotalSeconds -gt 30) { throw "Timed out waiting for $Path" }
        Start-Sleep -Milliseconds 50
    }
    Start-Sleep -Milliseconds 50
}
function Invoke-Control([string]$Exe, [string]$Command) {
    $value = & $Exe --control $Command 2>&1 | Out-String
    if ($LASTEXITCODE) { throw "Control invocation failed: $value" }
    return $value.Trim()
}
function Get-Owned([string]$Exe, [string]$HostPath) {
    return @(Get-CimInstance Win32_Process -Filter "Name='WatchdogAutoForms.exe' OR Name='WatchdogAutoWpf.exe' OR Name='NekoLib.Watchdog.Host.exe'" | Where-Object {
        $_.ExecutablePath -eq $Exe -or $_.ExecutablePath -eq $HostPath
    })
}
function Invoke-Case([string]$Exe, [string]$Hosting, [string]$Mode) {
    $directory = Join-Path $root ('runs/' + [IO.Path]::GetFileNameWithoutExtension($Exe) + '-' + (Split-Path (Split-Path $Exe -Parent) -Leaf) + '-' + $Hosting + '-' + $Mode + '-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $hostPath = Join-Path (Split-Path $Exe -Parent) 'NekoLib.Watchdog.Host/NekoLib.Watchdog.Host.exe'
    $process = $null
    try {
        if ($Mode -eq 'invalid') {
            $sentinel = Join-Path $directory 'unowned.json'
            [IO.File]::WriteAllText($sentinel, 'must not be consumed')
            $process = Start-Process -FilePath $Exe -ArgumentList @('--nekolib-watchdog-supervisor', ('"' + $sentinel + '"')) -WorkingDirectory $directory -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $directory stderr.txt) -RedirectStandardOutput (Join-Path $directory stdout.txt)
            if (-not $process.WaitForExit(15000) -or $process.ExitCode -ne 1) { throw 'Invalid supervisor invocation must fail before application entry.' }
            if ([IO.File]::ReadAllText($sentinel) -ne 'must not be consumed') { throw 'Supervisor touched an unowned configuration file.' }
            if ((Get-Content (Join-Path $directory stderr.txt) -Raw) -notmatch 'Headless Watchdog supervisor failed') { throw 'Missing invalid-supervisor diagnostic.' }
        }
        else {
            $arguments = @($Mode, $directory, 'arg with spaces', 'embedded "quote"', 'backslash\', '', '猫')
            # Quote trailing backslashes according to Windows argv parsing.
            $quoted = @($arguments | ForEach-Object {
                $escaped = [regex]::Replace($_, '(\\*)"', '$1$1\"')
                $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
                '"' + $escaped + '"'
            })
            $process = Start-Process -FilePath $Exe -ArgumentList $quoted -WorkingDirectory $directory -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $directory stderr.txt) -RedirectStandardOutput (Join-Path $directory stdout.txt)
            Wait-File (Join-Path $directory initial.ready)
            $initialPid = [int](Get-Content (Join-Path $directory initial.ready) -Raw)
            if ((Get-Content (Join-Path $directory initial.windows) -Raw).Trim() -ne '1') { throw 'Initial native window count differs from one.' }
            $expectedArgs = @($arguments | ForEach-Object { [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($_)) })
            $actualArgs = @(Get-Content (Join-Path $directory initial.args))
            if (($expectedArgs -join '|') -cne ($actualArgs -join '|')) { throw 'Initial entry arguments were changed.' }
            if ($Mode -eq 'kill') { Stop-Process -Id $initialPid -Force }
            if ($Mode -in @('restart', 'restart-soft') -and (Invoke-Control $Exe 'restart') -ne 'True') { throw 'Restart was rejected.' }
            if ($Mode -eq 'stop' -and (Invoke-Control $Exe 'stop') -ne 'True') { throw 'Stop was rejected.' }
            if ($Mode -in @('crash', 'kill', 'restart', 'restart-soft')) {
                Wait-File (Join-Path $directory replacement.ready)
                $replacementPid = [int](Get-Content (Join-Path $directory replacement.ready) -Raw)
                if ($initialPid -eq $replacementPid) { throw 'Recovery did not replace the process.' }
                if ((Get-Content (Join-Path $directory replacement.windows) -Raw).Trim() -ne '1') { throw 'Replacement native window count differs from one.' }
                if (($expectedArgs -join '|') -cne (@(Get-Content (Join-Path $directory replacement.args)) -join '|')) { throw 'Restart arguments were changed.' }
                Wait-File (Join-Path $directory replacement.done)
            }
            if (-not $process.WaitForExit(15000)) { throw 'Initial application did not complete its scenario.' }
            if ($Mode -eq 'crash' -and $process.ExitCode -ne 71) { throw 'Fatal qualification exit code was not preserved.' }
            if ($Mode -eq 'disable-after' -and -not (Test-Path (Join-Path $directory disabled-alive.txt))) { throw 'Active cancellation did not leave the application alive.' }
            if ($Mode -eq 'warning' -and -not (Test-Path (Join-Path $directory warnings.txt))) { throw 'Missing fail-open activation warning.' }
            if ($Mode -notin @('defaults', 'disable-before', 'warning') -and -not (Test-Path (Join-Path $directory watchdog.log))) { throw 'Optional runtime configuration was not transferred.' }
            if ($Mode -eq 'restart' -and (Get-Content (Join-Path $directory watchdog.log) -Raw) -notmatch 'attached_tree_preserving_supervisor') { throw 'Forced same-executable/sidecar attached restart was not observed.' }
        }
        $watch = [Diagnostics.Stopwatch]::StartNew()
        do {
            $owned = @(Get-Owned $Exe $hostPath)
            if ($owned.Count -eq 0) { break }
            Start-Sleep -Milliseconds 100
        } while ($watch.Elapsed.TotalSeconds -lt 15)
        if ($owned.Count) { throw 'Application or supervisor survived normal-return cleanup.' }
        if ((Invoke-Control $Exe 'status') -notlike 'error=*') { throw 'Supervisor endpoint survived cleanup.' }
        $results.Add([pscustomobject]@{ executable=$Exe; hosting=$Hosting; mode=$Mode; result='passed'; evidence=$directory })
        Write-Host "PASS automatic bootstrap $Hosting $Exe $Mode"
    }
    finally {
        # Only processes with this exact qualification output path are owned here.
        $owned = @(Get-Owned $Exe $hostPath)
        foreach ($item in @($owned | Sort-Object @{Expression={ if ($_.Name -eq 'NekoLib.Watchdog.Host.exe' -or $_.CommandLine -like '*--nekolib-watchdog-supervisor*') {0} else {1} }})) {
            Stop-Process -Id $item.ProcessId -Force -ErrorAction SilentlyContinue
        }
        if ($process) { $process.Dispose() }
        $results | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $root results.json) -Encoding utf8
    }
}

foreach ($projectName in @('WatchdogAutoForms', 'WatchdogAutoWpf')) {
    $project = Join-Path $consumers "$projectName/$projectName.csproj"
    foreach ($hosting in @('SameExecutable', 'Sidecar')) {
        $output = Join-Path $root "bin/$projectName/$hosting/"
        $intermediate = Join-Path $root "obj/$projectName/$hosting/"
        $properties = @("-p:NekoLibPackageVersion=$PackageVersion", "-p:NekoLibWatchdogHostingMode=$hosting", "-p:BaseOutputPath=$output", "-p:BaseIntermediateOutputPath=$intermediate")
        Invoke-Build (@('restore', $project, '--configfile', (Join-Path $consumers NuGet.Config), '--force-evaluate', '--no-http-cache') + $properties)
        Invoke-Build (@('build', $project, '-c', $Configuration, '--no-restore') + $properties)
        foreach ($tfm in @('net481', 'net9.0-windows')) {
            $exe = Join-Path $output "$Configuration/$tfm/$projectName.exe"
            $hostDirectory = Join-Path (Split-Path $exe -Parent) 'NekoLib.Watchdog.Host'
            if ($hosting -eq 'SameExecutable' -and (Test-Path $hostDirectory)) { throw 'Same-executable mode deployed a separate Host payload.' }
            if ($hosting -eq 'Sidecar' -and -not (Test-Path $hostDirectory)) { throw 'Explicit sidecar payload is missing.' }
            $modes = if ($hosting -eq 'SameExecutable') { @('defaults', 'normal', 'disable-before', 'disable-after', 'warning', 'crash', 'kill', 'restart', 'restart-soft', 'stop', 'invalid') } else { @('normal', 'disable-after', 'crash') }
            foreach ($mode in $modes) { Invoke-Case $exe $hosting $mode }
        }
    }
}
Write-Host "Automatic Watchdog bootstrap passed: $($results.Count) scenarios."
