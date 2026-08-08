<#
    Worker processes: locate, build, launch, watch, stop, and reconcile.

    Ownership is the rule this file exists to enforce. The orchestrator may only
    ever stop something it started itself and recorded, and the record carries
    the process name and start time alongside the id so a recycled PID cannot be
    mistaken for a worker. A campaign that killed an unrelated process because a
    number matched would be worse than one that leaked.

    Nothing here knows what a scenario checks. Workers are launched, their exit
    codes are collected, and the meaning of those codes belongs to them.
#>

Set-StrictMode -Version 2.0

function Resolve-WorkerCommand {
    param(
        [Parameter(Mandatory = $true)] $Scenario,
        [Parameter(Mandatory = $true)][hashtable] $Tokens,
        [Parameter(Mandatory = $true)][string] $RepositoryRoot
    )

    $executable = $Scenario.executable
    $arguments = @()
    foreach ($argument in @($Scenario.arguments)) { $arguments += $argument }

    foreach ($key in $Tokens.Keys) {
        $placeholder = '{' + $key + '}'
        $executable = $executable.Replace($placeholder, [string] $Tokens[$key])
        for ($i = 0; $i -lt $arguments.Count; $i++) {
            $arguments[$i] = $arguments[$i].Replace($placeholder, [string] $Tokens[$key])
        }
    }

    if (-not [System.IO.Path]::IsPathRooted($executable)) {
        $executable = Join-Path $RepositoryRoot $executable
    }

    return [pscustomobject]@{
        Id         = $Scenario.id
        Executable = $executable
        Arguments  = $arguments
    }
}

<#
    Builds one scenario project explicitly.

    Never through dotnet test: these are runnable scenarios that live outside
    the solution, and driving them through the test host would both fail and
    misrepresent what they are.
#>
function Build-Worker {
    param(
        [Parameter(Mandatory = $true)] $Scenario,
        [Parameter(Mandatory = $true)][string] $RepositoryRoot
    )

    if (-not ($Scenario.PSObject.Properties.Name -contains 'project')) {
        return [pscustomobject]@{ Built = $false; Detail = 'no project recorded; nothing to build' }
    }

    $project = Join-Path $RepositoryRoot $Scenario.project
    if (-not (Test-Path $project)) {
        return [pscustomobject]@{ Built = $false; Detail = "project not found: $project" }
    }

    $arguments = @('build', $project, '-v', 'q', '--nologo')
    if ($Scenario.PSObject.Properties.Name -contains 'targetFramework' -and $Scenario.targetFramework) {
        $arguments += @('-f', $Scenario.targetFramework)
    }

    $output = & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        return [pscustomobject]@{ Built = $false; Detail = ($output -join ' ').Trim() }
    }

    return [pscustomobject]@{ Built = $true; Detail = 'built' }
}

function Start-Worker {
    param(
        [Parameter(Mandatory = $true)] $Command,
        [Parameter(Mandatory = $true)][string] $WorkingDirectory,
        [Parameter(Mandatory = $true)][string] $StandardOutputPath,
        [Parameter(Mandatory = $true)][string] $StandardErrorPath
    )

    # Each worker's streams go to its own files rather than to the
    # orchestrator's console, so a campaign with several workers does not
    # interleave their output into something nobody can attribute.
    $process = Start-Process -FilePath $Command.Executable `
        -ArgumentList $Command.Arguments `
        -WorkingDirectory $WorkingDirectory `
        -RedirectStandardOutput $StandardOutputPath `
        -RedirectStandardError $StandardErrorPath `
        -NoNewWindow -PassThru

    # Touching Handle caches the native process handle in the Process object.
    # Without it, Start-Process -PassThru hands back an object whose ExitCode is
    # null after the process ends - the handle is gone and there is nothing left
    # to ask. An orchestrator that reads that null as "not zero" reports every
    # worker as failed, which is exactly what happened the first time this ran.
    $null = $process.Handle

    return [pscustomobject]@{
        Id           = $Command.Id
        Process      = $process
        ProcessId    = $process.Id
        ProcessName  = $process.ProcessName
        StartTimeUtc = $process.StartTime.ToUniversalTime().ToString('o')
        Executable   = $Command.Executable
        Arguments    = ($Command.Arguments -join ' ')
        ExitCode     = $null
        Outcome      = 'running'
    }
}

<#
    Waits for every worker, bounded by the campaign deadline.

    A worker that outlives the deadline is a progress failure, not a slow
    success, and it is recorded as one before anything is forced.
#>
function Wait-Workers {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]] $Workers,
        [Parameter(Mandatory = $true)][datetime] $DeadlineUtc,
        [Parameter(Mandatory = $true)][scriptblock] $Report
    )

    $lastReport = [datetime]::UtcNow

    while ($true) {
        $running = @($Workers | Where-Object { $_.Outcome -eq 'running' })
        if ($running.Count -eq 0) { break }

        foreach ($worker in $running) {
            if ($worker.Process.HasExited) {
                # WaitForExit on an already-exited process returns at once and
                # guarantees the exit code and redirected streams are settled.
                $worker.Process.WaitForExit()
                $worker.ExitCode = $worker.Process.ExitCode

                if ($null -eq $worker.ExitCode) {
                    $worker.Outcome = 'unknown'
                    & $Report ("worker {0} exited but its code could not be read" -f $worker.Id)
                } elseif ($worker.ExitCode -eq 0) {
                    $worker.Outcome = 'passed'
                    & $Report ("worker {0} exited 0" -f $worker.Id)
                } else {
                    $worker.Outcome = 'failed'
                    & $Report ("worker {0} exited {1}" -f $worker.Id, $worker.ExitCode)
                }
            }
        }

        if ([datetime]::UtcNow -gt $DeadlineUtc) {
            foreach ($worker in @($Workers | Where-Object { $_.Outcome -eq 'running' })) {
                $worker.Outcome = 'timeout'
                & $Report ("worker {0} passed the campaign deadline and will be stopped" -f $worker.Id)
            }
            break
        }

        if (([datetime]::UtcNow - $lastReport).TotalSeconds -ge 60) {
            $lastReport = [datetime]::UtcNow
            $names = (@($Workers | Where-Object { $_.Outcome -eq 'running' } | ForEach-Object { $_.Id }) -join ', ')
            & $Report ("still running: $names")
        }

        Start-Sleep -Milliseconds 500
    }

    return $Workers
}

<#
    Stops a worker the orchestrator started, and only that one.

    Graceful comes first and means giving the process its own bounded time to
    finish and run its own cleanup; these are console scenarios and there is no
    reliable way to deliver Ctrl+C to another console group, so the honest
    description is "wait, then force". Forcing verifies name and start time
    before acting, because a PID on its own is not an identity.
#>
function Stop-Worker {
    param(
        [Parameter(Mandatory = $true)] $Worker,
        [int] $GraceSeconds = 30
    )

    if ($Worker.Process.HasExited) {
        return 'already exited'
    }

    $deadline = (Get-Date).AddSeconds($GraceSeconds)
    while ((Get-Date) -lt $deadline) {
        if ($Worker.Process.HasExited) { return 'exited during the grace period' }
        Start-Sleep -Milliseconds 500
    }

    $live = Get-Process -Id $Worker.ProcessId -ErrorAction SilentlyContinue
    if ($null -eq $live) { return 'gone before it could be forced' }

    if ($live.ProcessName -ne $Worker.ProcessName) {
        return "refused to force PID $($Worker.ProcessId): it is now '$($live.ProcessName)', not '$($Worker.ProcessName)'"
    }

    $liveStart = $live.StartTime.ToUniversalTime().ToString('o')
    if ($liveStart -ne $Worker.StartTimeUtc) {
        return "refused to force PID $($Worker.ProcessId): its start time no longer matches the recorded one"
    }

    Stop-Process -Id $Worker.ProcessId -Force -ErrorAction SilentlyContinue
    return 'forced after the grace period'
}

function Save-OwnedResources {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $CampaignId,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]] $Workers,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]] $Adopted
    )

    $record = [pscustomobject]@{
        campaignId  = $CampaignId
        writtenUtc  = (Get-Date).ToUniversalTime().ToString('o')
        processes   = @($Workers | ForEach-Object {
            [pscustomobject]@{
                scenarioId   = $_.Id
                processId    = $_.ProcessId
                processName  = $_.ProcessName
                startTimeUtc = $_.StartTimeUtc
                executable   = $_.Executable
            }
        })
        # Recorded, never owned: an adopted container belongs to the machine's
        # owner and is restored by the scenario that adopted it.
        adopted     = @($Adopted)
    }

    Set-Content -Path $Path -Value ($record | ConvertTo-Json -Depth 6) -Encoding utf8
}

<#
    Finds campaigns that never wrote a summary and reports whatever they
    recorded as owned that is still alive.

    Reporting is all this does. Killing something a previous campaign started is
    a decision for the operator, which is why run.ps1 exposes it behind an
    explicit switch and why the identity check above still applies.
#>
function Get-StaleCampaigns {
    param(
        [Parameter(Mandatory = $true)][string] $ArtifactsRoot,
        [Parameter(Mandatory = $true)][string] $CurrentCampaignId
    )

    $stale = @()
    if (-not (Test-Path $ArtifactsRoot)) { return $stale }

    foreach ($directory in Get-ChildItem -Path $ArtifactsRoot -Directory -ErrorAction SilentlyContinue) {
        if ($directory.Name -eq $CurrentCampaignId) { continue }

        $ownedPath = Join-Path $directory.FullName 'owned.json'
        if (-not (Test-Path $ownedPath)) { continue }
        if (Test-Path (Join-Path $directory.FullName 'summary.json')) { continue }

        $owned = Get-Content -Path $ownedPath -Raw | ConvertFrom-Json
        $alive = @()

        foreach ($entry in @($owned.processes)) {
            $live = Get-Process -Id $entry.processId -ErrorAction SilentlyContinue
            if ($null -eq $live) { continue }
            if ($live.ProcessName -ne $entry.processName) { continue }
            if ($live.StartTime.ToUniversalTime().ToString('o') -ne $entry.startTimeUtc) { continue }

            $alive += $entry
        }

        $stale += [pscustomobject]@{
            CampaignId = $owned.campaignId
            Directory  = $directory.FullName
            Alive      = $alive
        }
    }

    return $stale
}
