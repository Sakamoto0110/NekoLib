<#
.SYNOPSIS
    E3-ORCH - deterministic Phase E campaign orchestration.

.DESCRIPTION
    A thin orchestrator, on purpose. It generates and persists a seeded fault
    schedule, launches the selected scenario executables, watches them, collects
    their exit codes, and writes one aggregate result. It contains no business
    assertions: what a fault means and whether a run passed are the workers'
    decisions, and this script only reports what they decided.

    Ownership is strict. It starts only local processes it records, stops only
    those, verifies process name and start time before forcing anything, and
    never touches a container, service, or endpoint it did not start. Resources
    a scenario adopts - the SQL Server container, for instance - are recorded as
    adopted and restored by the scenario that adopted them.

.PARAMETER Mode
    smoke, recovery, or soak.

.PARAMETER Duration
    Campaign window, for example 20m, 90m, 16h. Defaults per mode.

.PARAMETER Seed
    Integer seed. The same seed and duration must produce the same normalized
    schedule; -PrintScheduleOnly is how that is checked.

.PARAMETER Scenarios
    Explicit scenario ids. Defaults to every enabled scenario in the config.

.PARAMETER Build
    Builds each selected scenario project first, explicitly, never through
    dotnet test.

.PARAMETER PreflightOnly
    Runs the preflight phase and stops.

.PARAMETER PrintScheduleOnly
    Generates and prints the schedule, then stops. Touches nothing.

.PARAMETER FailWorker
    Deliberately corrupts one worker's arguments so it exits nonzero. This
    exists for the suite's acceptance criterion: a failed worker must fail the
    campaign without preventing the others from finishing and cleaning up.

.PARAMETER StopStale
    Stops processes recorded by an earlier campaign that never wrote a summary.
    Without it, stale processes are only reported.

.EXAMPLE
    .\run.ps1 -Mode smoke
    .\run.ps1 -Mode recovery -Duration 90m -Seed 20260808
    .\run.ps1 -Mode smoke -PrintScheduleOnly
#>
[CmdletBinding()]
param(
    [ValidateSet('smoke', 'recovery', 'soak')]
    [string] $Mode = 'smoke',

    [string] $Duration,
    [int] $Seed = 20260808,
    [string[]] $Scenarios,
    [string] $Config,
    [string] $ArtifactsRoot,
    [switch] $Build,
    [switch] $PreflightOnly,
    [switch] $PrintScheduleOnly,
    [string] $FailWorker,
    [switch] $StopStale
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'lib\Schedule.ps1')
. (Join-Path $PSScriptRoot 'lib\Workers.ps1')

# Exit codes mirror the scenarios' own contract so an aggregate result reads the
# same way as a worker's.
$ExitSuccess = 0
$ExitUsage = 2
$ExitPrerequisite = 3
$ExitWorkerFailed = 4
$ExitTimeout = 5
$ExitReconciliation = 6
$ExitUnexpected = 7

$script:Log = New-Object System.Collections.Generic.List[string]

function Write-Phase {
    param([string] $Text)
    $line = ('[{0}] {1}' -f (Get-Date).ToUniversalTime().ToString('HH:mm:ss'), $Text)
    Write-Host $line
    $script:Log.Add($line) | Out-Null
}

function Get-RepositoryRoot {
    $directory = Get-Item -LiteralPath $PSScriptRoot
    while ($null -ne $directory) {
        if (Test-Path (Join-Path $directory.FullName 'NekoLib.sln')) { return $directory.FullName }
        $directory = $directory.Parent
    }

    throw 'No repository root found above this script.'
}

function ConvertTo-Duration {
    param([string] $Text, [timespan] $Fallback)

    if ([string]::IsNullOrWhiteSpace($Text)) { return $Fallback }

    $trimmed = $Text.Trim()
    $suffix = $trimmed.Substring($trimmed.Length - 1)
    $number = $trimmed
    if ($suffix -notmatch '\d') { $number = $trimmed.Substring(0, $trimmed.Length - 1) }

    $amount = 0.0
    if (-not [double]::TryParse($number, [ref] $amount) -or $amount -le 0) {
        throw "'$Text' is not a positive duration; use a form like 20m, 90m or 16h."
    }

    switch ($suffix.ToLowerInvariant()) {
        'h' { return [timespan]::FromHours($amount) }
        'm' { return [timespan]::FromMinutes($amount) }
        's' { return [timespan]::FromSeconds($amount) }
        default { return [timespan]::FromSeconds($amount) }
    }
}

function Get-DefaultDuration {
    param([string] $ForMode)
    switch ($ForMode) {
        'smoke' { return [timespan]::FromMinutes(20) }
        'recovery' { return [timespan]::FromMinutes(60) }
        default { return [timespan]::FromHours(16) }
    }
}

function Get-ResourceSample {
    param([string] $Marker)

    $os = Get-CimInstance -ClassName Win32_OperatingSystem
    return [pscustomobject]@{
        marker              = $Marker
        utc                 = (Get-Date).ToUniversalTime().ToString('o')
        processCount        = (Get-Process).Count
        freePhysicalKb      = [int64] $os.FreePhysicalMemory
        totalPhysicalKb     = [int64] $os.TotalVisibleMemorySize
    }
}

function Test-SafePathSegment {
    param([string] $Value)

    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -eq '.' -or $Value -eq '..') { return $false }
    if ($Value.IndexOf([System.IO.Path]::DirectorySeparatorChar) -ge 0 -or
        $Value.IndexOf([System.IO.Path]::AltDirectorySeparatorChar) -ge 0) { return $false }
    return $Value.IndexOfAny([System.IO.Path]::GetInvalidFileNameChars()) -lt 0
}

# ---------------------------------------------------------------- preflight --

$repositoryRoot = Get-RepositoryRoot

if (-not $Config) { $Config = Join-Path $PSScriptRoot 'campaign.json' }
if (-not (Test-Path $Config)) {
    Write-Host "E3-ORCH: configuration not found: $Config"
    exit $ExitUsage
}

$configuration = Get-Content -Path $Config -Raw | ConvertFrom-Json
if ($configuration.schemaVersion -notin @(1, 2)) {
    Write-Host "E3-ORCH: configuration schemaVersion $($configuration.schemaVersion) is not supported by this orchestrator."
    exit $ExitUsage
}

$window = ConvertTo-Duration -Text $Duration -Fallback (Get-DefaultDuration -ForMode $Mode)

if (-not $ArtifactsRoot) { $ArtifactsRoot = $configuration.artifactsRoot }
if (-not [System.IO.Path]::IsPathRooted($ArtifactsRoot)) {
    $ArtifactsRoot = Join-Path $repositoryRoot $ArtifactsRoot
}

$selected = @()
foreach ($scenario in @($configuration.scenarios)) {
    if ($Scenarios) {
        if ($Scenarios -notcontains $scenario.id) { continue }
    } elseif (-not $scenario.enabled) {
        continue
    }

    if (-not ($scenario.modes.PSObject.Properties.Name -contains $Mode)) {
        Write-Host "E3-ORCH: scenario '$($scenario.id)' defines no '$Mode' mode."
        exit $ExitUsage
    }

    $selected += $scenario
}

if ($selected.Count -eq 0) {
    Write-Host 'E3-ORCH: no scenario selected.'
    exit $ExitUsage
}

# Milliseconds, not seconds. Two campaigns started inside the same second
# produced the same id and the second one silently reused the first's
# directory - found by running the stale-detection acceptance test twice in a
# row, which is exactly the kind of thing a campaign must never do quietly.
$campaignId = '{0}-{1}-s{2}-{3}' -f $configuration.campaignPrefix, $Mode, $Seed,
    (Get-Date).ToUniversalTime().ToString("yyyyMMdd'T'HHmmssfff'Z'")

# Smoke deliberately carries no faults: the suite defines it as every workload
# class without destructive fault density. The schedule is still generated and
# written, so a campaign directory always records what was planned.
$scheduleScenarios = @()
if ($Mode -ne 'smoke') { $scheduleScenarios = $selected }

$schedule = New-CampaignSchedule -CampaignId $campaignId -Seed $Seed -Mode $Mode `
    -Duration $window -Scenarios $scheduleScenarios

if ($PrintScheduleOnly) {
    $schedule | ConvertTo-Json -Depth 8
    Write-Host ''
    Write-Host ("normalized-hash {0}" -f $schedule.hash)
    exit $ExitSuccess
}

$campaignDirectory = Join-Path $ArtifactsRoot $campaignId
$null = New-Item -ItemType Directory -Path $campaignDirectory -Force

Write-Phase ("campaign  {0}" -f $campaignId)
Write-Phase ("mode      {0}, window {1}, seed {2}" -f $Mode, $window, $Seed)
Write-Phase ("run       {0}" -f $campaignDirectory)
Write-Phase ''
Write-Phase '=== 1 preflight ==='

$problems = @()

$drive = Get-PSDrive -Name ([System.IO.Path]::GetPathRoot($campaignDirectory).TrimEnd('\', ':'))
$freeGb = [Math]::Round($drive.Free / 1GB, 1)
Write-Phase ("disk      {0} GB free on {1}:" -f $freeGb, $drive.Name)
if ($freeGb -lt 2) { $problems += "less than 2 GB free on $($drive.Name):" }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $problems += 'the dotnet CLI is not on PATH'
}

$stale = @(Get-StaleCampaigns -ArtifactsRoot $ArtifactsRoot -CurrentCampaignId $campaignId)
foreach ($entry in $stale) {
    Write-Phase ("stale     campaign {0} never wrote a summary; {1} recorded process(es) still alive" -f $entry.CampaignId, $entry.Alive.Count)
    foreach ($alive in $entry.Alive) {
        if ($StopStale) {
            Stop-Process -Id $alive.processId -Force -ErrorAction SilentlyContinue
            Write-Phase ("stale     stopped PID {0} ({1}) from {2}" -f $alive.processId, $alive.processName, $entry.CampaignId)
        } else {
            Write-Phase ("stale     PID {0} ({1}) is still running; pass -StopStale to end it" -f $alive.processId, $alive.processName)
        }
    }
}

$commands = @()
$workerIds = @{}
foreach ($scenario in $selected) {
    if ($Build) {
        $built = Build-Worker -Scenario $scenario -RepositoryRoot $repositoryRoot
        Write-Phase ("build     {0}: {1}" -f $scenario.id, $built.Detail)
        if (-not $built.Built) { $problems += "$($scenario.id) did not build" }
    }

    $workerId = if ($scenario.PSObject.Properties.Name -contains 'workerId') {
        [string] $scenario.workerId
    } else {
        [string] $scenario.id
    }
    $scenarioId = [string] $scenario.id
    $artifactLayoutVersion = if ($scenario.PSObject.Properties.Name -contains 'artifactLayoutVersion') {
        [int] $scenario.artifactLayoutVersion
    } else {
        1
    }

    if (-not (Test-SafePathSegment $workerId)) {
        $problems += "$scenarioId has an unsafe workerId '$workerId'"
    } elseif ($workerIds.ContainsKey($workerId)) {
        $problems += "$scenarioId duplicates workerId '$workerId'"
    } else {
        $workerIds[$workerId] = $true
    }
    if ($artifactLayoutVersion -notin @(1, 2)) {
        $problems += "$scenarioId has unsupported artifactLayoutVersion $artifactLayoutVersion"
    }

    $workerDirectory = if ($artifactLayoutVersion -eq 2) {
        Join-Path (Join-Path $campaignDirectory 'workers') $workerId
    } else {
        Join-Path $campaignDirectory $scenarioId
    }
    $null = New-Item -ItemType Directory -Path $workerDirectory -Force

    $workerArtifactsRoot = if ($artifactLayoutVersion -eq 2) { $ArtifactsRoot } else { $campaignDirectory }

    $tokens = @{
        'seed'            = $Seed
        'artifacts'       = $workerArtifactsRoot
        'campaignId'      = $campaignId
        'workerId'        = $workerId
        'schedule'        = (Join-Path $campaignDirectory 'schedule.json')
        'duration'        = $Duration
        'durationSeconds' = [int] $window.TotalSeconds
    }
    if (-not $tokens['duration']) { $tokens['duration'] = ('{0}s' -f [int] $window.TotalSeconds) }

    $modeConfiguration = $scenario.modes.$Mode
    $withArguments = [pscustomobject]@{
        id         = $workerId
        executable = $scenario.executable
        arguments  = @($modeConfiguration.arguments)
    }

    $command = Resolve-WorkerCommand -Scenario $withArguments -Tokens $tokens -RepositoryRoot $repositoryRoot

    if ($FailWorker -and $FailWorker -eq $scenario.id) {
        # The acceptance case: one worker is made to fail on purpose so the
        # campaign can be shown to fail without the others being disturbed.
        $command.Arguments = @('--this-option-does-not-exist')
        Write-Phase ("inject    {0} will be launched with a deliberately invalid argument" -f $scenario.id)
    }

    if (-not (Test-Path $command.Executable)) {
        $problems += "$($scenario.id): executable not found at $($command.Executable)"
    }

    foreach ($prerequisite in @($scenario.prerequisites)) {
        if ($prerequisite.kind -ne 'environmentVariable') { continue }

        $present = $false
        foreach ($scope in @('Process', 'User', 'Machine')) {
            if ([Environment]::GetEnvironmentVariable($prerequisite.name, $scope)) { $present = $true }
        }

        if ($present) {
            Write-Phase ("prereq    {0}: {1} is set (value never read)" -f $scenario.id, $prerequisite.name)
        } else {
            $problems += "$($scenario.id): $($prerequisite.name) is not set"
        }
    }

    foreach ($adopted in @($scenario.adopts)) {
        Write-Phase ("adopted   {0} uses {1}; the orchestrator neither starts nor stops it" -f $scenario.id, $adopted)
    }

    $commands += [pscustomobject]@{
        Scenario              = $scenario
        ScenarioId            = $scenarioId
        WorkerId              = $workerId
        ArtifactLayoutVersion = $artifactLayoutVersion
        Command               = $command
        Directory             = $workerDirectory
        ResultPath            = if ($artifactLayoutVersion -eq 2) {
            Join-Path (Join-Path $workerDirectory $scenarioId) 'result.json'
        } else {
            $null
        }
        ResultRelativePath    = if ($artifactLayoutVersion -eq 2) {
            Join-Path (Join-Path (Join-Path 'workers' $workerId) $scenarioId) 'result.json'
        } else {
            $null
        }
    }
}

$aggregateArtifactLayoutVersion = if (@($commands | Where-Object { $_.ArtifactLayoutVersion -eq 2 }).Count -gt 0) {
    2
} else {
    1
}

$schedulePath = Save-CampaignSchedule -Schedule $schedule -Path (Join-Path $campaignDirectory 'schedule.json')
Write-Phase ("schedule  {0} event(s), {1}" -f $schedule.events.Count, $schedule.hash)
Write-Phase ("schedule  persisted before any worker started: {0}" -f $schedulePath)

if ($problems.Count -gt 0) {
    foreach ($problem in $problems) { Write-Phase ("BLOCKED   {0}" -f $problem) }
    Set-Content -Path (Join-Path $campaignDirectory 'summary.md') `
        -Value ("# $campaignId`n`nPreflight blocked the campaign:`n`n" + (($problems | ForEach-Object { "- $_" }) -join "`n")) `
        -Encoding utf8
    exit $ExitPrerequisite
}

if ($PreflightOnly) {
    Write-Phase 'preflight only; stopping here'
    exit $ExitSuccess
}

# ----------------------------------------------------------------- baseline --

Write-Phase ''
Write-Phase '=== 2 baseline ==='
$samples = @()
$samples += Get-ResourceSample -Marker 'baseline'
Write-Phase ("baseline  {0} processes, {1} MB free" -f $samples[0].processCount, [int]($samples[0].freePhysicalKb / 1024))

# ------------------------------------------------------------------ warm-up --

Write-Phase ''
Write-Phase '=== 3 warm-up ==='

$startedUtc = (Get-Date).ToUniversalTime()
$workers = @()
$ownedPath = Join-Path $campaignDirectory 'owned.json'
$adoptedAll = @()

foreach ($entry in $commands) {
    foreach ($adopted in @($entry.Scenario.adopts)) { $adoptedAll += $adopted }

    $processStdoutName = if ($entry.ArtifactLayoutVersion -eq 2) { 'process.stdout.log' } else { 'stdout.log' }
    $processStderrName = if ($entry.ArtifactLayoutVersion -eq 2) { 'process.stderr.log' } else { 'stderr.log' }

    $worker = Start-Worker -Command $entry.Command `
        -WorkingDirectory $repositoryRoot `
        -StandardOutputPath (Join-Path $entry.Directory $processStdoutName) `
        -StandardErrorPath (Join-Path $entry.Directory $processStderrName)

    $worker | Add-Member -NotePropertyName ScenarioId -NotePropertyValue $entry.ScenarioId
    $worker | Add-Member -NotePropertyName WorkerId -NotePropertyValue $entry.WorkerId
    $worker | Add-Member -NotePropertyName ArtifactLayoutVersion -NotePropertyValue $entry.ArtifactLayoutVersion
    $worker | Add-Member -NotePropertyName ResultPath -NotePropertyValue $entry.ResultPath
    $worker | Add-Member -NotePropertyName ResultRelativePath -NotePropertyValue $entry.ResultRelativePath

    $workers += $worker
    Write-Phase ("started   {0} as PID {1}" -f $worker.Id, $worker.ProcessId)

    # Recorded immediately, one worker at a time: a machine that dies here must
    # still leave a list of what was owned.
    Save-OwnedResources -Path $ownedPath -CampaignId $campaignId -Workers $workers -Adopted $adoptedAll
}

Start-Sleep -Seconds 2
foreach ($worker in $workers) {
    if ($worker.Process.HasExited -and $worker.Process.ExitCode -ne 0) {
        Write-Phase ("warm-up   {0} exited immediately with {1}" -f $worker.Id, $worker.Process.ExitCode)
    }
}

$samples += Get-ResourceSample -Marker 'post-warm-up'

# --------------------------------------------- workload, faults, cool-down --

Write-Phase ''
Write-Phase '=== 4-6 workload, fault window, cool-down ==='
if ($schedule.events.Count -gt 0) {
    Write-Phase ("faults    dispatched by their owning scenarios from the persisted schedule")
    foreach ($item in $schedule.events) {
        Write-Phase ("plan      +{0,6}s  {1}  {2}" -f $item.offsetSeconds, $item.scenarioId, $item.kind)
    }
} else {
    Write-Phase 'faults    none planned for this mode'
}

# The deadline is the window plus a margin for the workers' own bounded
# cleanup. A worker still running past it is a progress failure.
$deadlineUtc = $startedUtc.Add($window).Add([timespan]::FromMinutes(15))
$workers = @(Wait-Workers -Workers $workers -DeadlineUtc $deadlineUtc -Report { param($text) Write-Phase $text })

$samples += Get-ResourceSample -Marker 'cool-down'

# ----------------------------------------------------------------- shutdown --

Write-Phase ''
Write-Phase '=== 7 shutdown ==='
foreach ($worker in $workers) {
    if ($worker.Outcome -eq 'running' -or $worker.Outcome -eq 'timeout') {
        $result = Stop-Worker -Worker $worker -GraceSeconds 30
        Write-Phase ("stop      {0}: {1}" -f $worker.Id, $result)
        if ($worker.Process.HasExited) { $worker.ExitCode = $worker.Process.ExitCode }
    }
}

# ----------------------------------------------------------- reconciliation --

Write-Phase ''
Write-Phase '=== 8 reconciliation ==='

$reconciliation = @()
foreach ($worker in $workers) {
    $live = Get-Process -Id $worker.ProcessId -ErrorAction SilentlyContinue
    if ($null -ne $live -and $live.ProcessName -eq $worker.ProcessName) {
        $reconciliation += "$($worker.Id) is still running as PID $($worker.ProcessId)"
    }
}

foreach ($entry in $commands) {
    $stdoutName = if ($entry.ArtifactLayoutVersion -eq 2) { 'process.stdout.log' } else { 'stdout.log' }
    $stdout = Join-Path $entry.Directory $stdoutName
    if (-not (Test-Path $stdout)) {
        $reconciliation += "$($entry.WorkerId) produced no $stdoutName"
    }
    if ($entry.ArtifactLayoutVersion -eq 2 -and -not (Test-Path $entry.ResultPath)) {
        $reconciliation += "$($entry.WorkerId) produced no scenario result at $($entry.ResultPath)"
    }
}

$samples += Get-ResourceSample -Marker 'final'
$finishedUtc = (Get-Date).ToUniversalTime()

$failed = @($workers | Where-Object { $_.Outcome -eq 'failed' })
$timedOut = @($workers | Where-Object { $_.Outcome -eq 'timeout' })
$unknown = @($workers | Where-Object { $_.Outcome -eq 'unknown' })

foreach ($worker in $unknown) {
    $reconciliation += "$($worker.Id) exited but its exit code could not be read, so its result is unknown"
}

$exitCode = $ExitSuccess
if ($timedOut.Count -gt 0) { $exitCode = $ExitTimeout }
elseif ($failed.Count -gt 0) { $exitCode = $ExitWorkerFailed }
elseif ($reconciliation.Count -gt 0) { $exitCode = $ExitReconciliation }

$summary = [pscustomobject]@{
    campaignId       = $campaignId
    artifactLayoutVersion = $aggregateArtifactLayoutVersion
    mode             = $Mode
    seed             = $Seed
    windowSeconds    = [int] $window.TotalSeconds
    scheduleHash     = $schedule.hash
    plannedFaults    = $schedule.events.Count
    startedUtc       = $startedUtc.ToString('o')
    finishedUtc      = $finishedUtc.ToString('o')
    elapsedSeconds   = [int] ($finishedUtc - $startedUtc).TotalSeconds
    exitCode         = $exitCode
    adopted          = @($adoptedAll | Select-Object -Unique)
    reconciliation   = $reconciliation
    samples          = $samples
    workers          = @($workers | ForEach-Object {
        [pscustomobject]@{
            workerId              = $_.WorkerId
            scenarioId            = $_.ScenarioId
            artifactLayoutVersion = $_.ArtifactLayoutVersion
            resultPath            = $_.ResultRelativePath
            processId             = $_.ProcessId
            exitCode              = $_.ExitCode
            outcome               = $_.Outcome
            arguments             = $_.Arguments
        }
    })
}

Set-Content -Path (Join-Path $campaignDirectory 'summary.json') `
    -Value ($summary | ConvertTo-Json -Depth 8) -Encoding utf8

$markdown = New-Object System.Text.StringBuilder
$null = $markdown.AppendLine("# $campaignId")
$null = $markdown.AppendLine('')
$null = $markdown.AppendLine("| | |")
$null = $markdown.AppendLine("|---|---|")
$null = $markdown.AppendLine("| Mode | $Mode |")
$null = $markdown.AppendLine("| Artifact layout | v$aggregateArtifactLayoutVersion |")
$null = $markdown.AppendLine("| Seed | $Seed |")
$null = $markdown.AppendLine("| Window | $window |")
$null = $markdown.AppendLine("| Schedule | $($schedule.events.Count) fault(s), $($schedule.hash) |")
$null = $markdown.AppendLine("| Elapsed | $($summary.elapsedSeconds)s |")
$null = $markdown.AppendLine("| Exit code | $exitCode |")
$null = $markdown.AppendLine('')
$null = $markdown.AppendLine('## Workers')
$null = $markdown.AppendLine('')
$null = $markdown.AppendLine('| Worker | Scenario | Layout | PID | Exit | Outcome |')
$null = $markdown.AppendLine('|---|---|---|---|---|---|')
foreach ($worker in $workers) {
    $null = $markdown.AppendLine("| $($worker.WorkerId) | $($worker.ScenarioId) | v$($worker.ArtifactLayoutVersion) | $($worker.ProcessId) | $($worker.ExitCode) | $($worker.Outcome) |")
}
if ($reconciliation.Count -gt 0) {
    $null = $markdown.AppendLine('')
    $null = $markdown.AppendLine('## Reconciliation problems')
    $null = $markdown.AppendLine('')
    foreach ($problem in $reconciliation) { $null = $markdown.AppendLine("- $problem") }
}
$null = $markdown.AppendLine('')
$null = $markdown.AppendLine('## Orchestrator log')
$null = $markdown.AppendLine('')
$null = $markdown.AppendLine('```')
foreach ($line in $script:Log) { $null = $markdown.AppendLine($line) }
$null = $markdown.AppendLine('```')

Set-Content -Path (Join-Path $campaignDirectory 'summary.md') -Value $markdown.ToString() -Encoding utf8

foreach ($worker in $workers) {
    Write-Phase ("result    {0,-28} exit {1,-4} {2}" -f $worker.Id, $worker.ExitCode, $worker.Outcome)
}
foreach ($problem in $reconciliation) { Write-Phase ("PROBLEM   {0}" -f $problem) }

Write-Phase ''
Write-Phase ("aggregate exit {0}" -f $exitCode)
exit $exitCode
