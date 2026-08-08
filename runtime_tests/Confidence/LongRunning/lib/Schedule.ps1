<#
    Campaign-wide fault schedule generation.

    The schedule is generated once, before any worker starts, and persisted
    before the first process is launched. That ordering is the point: a machine
    that dies mid-campaign still leaves a document saying what should have
    happened, which is what makes an abrupt failure investigable instead of
    merely lost.

    The orchestrator does not know what any fault means. It places events in
    time from the seed and hands each worker the plan; the owning scenario is
    what dispatches its own faults and acknowledges them. Business assertions
    live in the workers, never here.
#>

Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'Deterministic.ps1')

$script:ScheduleSchemaVersion = 1
$script:ScheduleGeneratorVersion = 'e3orch-schedule-1'

function New-CampaignSchedule {
    param(
        [Parameter(Mandatory = $true)][string] $CampaignId,
        [Parameter(Mandatory = $true)][int] $Seed,
        [Parameter(Mandatory = $true)][string] $Mode,
        [Parameter(Mandatory = $true)][timespan] $Duration,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]] $Scenarios
    )

    $total = [double] $Duration.TotalSeconds

    # Quiet windows scale with the run but never vanish. A fault landing during
    # warm-up measures start-up, and one landing during shutdown races the
    # cleanup it is supposed to leave clean.
    $quiet = [Math]::Max(30.0, [Math]::Min($total * 0.08, 300.0))
    $minRecovery = 45.0

    $windowStart = $quiet
    $windowEnd = [Math]::Max($quiet + 1.0, $total - $quiet)
    $span = $windowEnd - $windowStart

    # One flat list of (scenario, kind) pairs, so two scenarios never receive
    # simultaneous destructive faults: every pair gets its own slice of the
    # window, across all scenarios rather than per scenario.
    $pairs = @()
    foreach ($scenario in $Scenarios) {
        $kinds = @()
        if ($scenario.PSObject.Properties.Name -contains 'faultKinds') { $kinds = @($scenario.faultKinds) }

        foreach ($kind in $kinds) {
            $pairs += [pscustomobject]@{ ScenarioId = $scenario.id; Kind = $kind }
        }
    }

    $random = New-DeterministicRandom -Seed $Seed
    $ordered = @()
    if ($pairs.Count -gt 0) {
        $ordered = @(Get-DeterministicShuffle -Items $pairs -Random $random)
    }

    $events = @()
    $count = $ordered.Count
    if ($count -gt 0) {
        $slice = $span / $count

        for ($i = 0; $i -lt $count; $i++) {
            $sliceStart = $windowStart + ($i * $slice)
            $usable = [Math]::Max(0.0, $slice - $minRecovery)
            $offset = $sliceStart + ((Get-NextDouble -Random $random) * $usable)

            # Whole seconds, because a worker that reads this file may parse the
            # offset as an integer and a fractional value would be truncated
            # inconsistently rather than rounded.
            $events += [pscustomobject]@{
                id            = ('{0}-f{1:D2}' -f $CampaignId, ($i + 1))
                offsetSeconds = [int][Math]::Round($offset)
                scenarioId    = $ordered[$i].ScenarioId
                kind          = $ordered[$i].Kind
                target        = 'scenario-owned'
            }
        }
    }

    $events = @($events | Sort-Object -Property offsetSeconds, scenarioId, kind)

    $schedule = [pscustomobject]@{
        schemaVersion              = $script:ScheduleSchemaVersion
        generatorVersion           = $script:ScheduleGeneratorVersion
        campaignId                 = $CampaignId
        mode                       = $Mode
        seed                       = $Seed
        requestedDurationSeconds   = [int][Math]::Round($total)
        quietStartSeconds          = [int][Math]::Round($quiet)
        quietEndSeconds            = [int][Math]::Round($quiet)
        minRecoveryIntervalSeconds = [int] $minRecovery
        maxFaults                  = $count
        generatedUtc               = (Get-Date).ToUniversalTime().ToString('o')
        events                     = $events
        hash                       = ''
    }

    $schedule.hash = Get-ScheduleHash -Schedule $schedule
    return $schedule
}

<#
    The canonical text the hash covers.

    generatedUtc is deliberately absent: it is provenance, and including it
    would make two runs of the same seed disagree by construction, which is
    exactly the acceptance criterion this function exists to satisfy.
#>
function Get-ScheduleNormalizedText {
    param([Parameter(Mandatory = $true)] $Schedule)

    $text = New-Object System.Text.StringBuilder
    $null = $text.Append("schema=$($Schedule.schemaVersion)`n")
    $null = $text.Append("generator=$($Schedule.generatorVersion)`n")
    $null = $text.Append("mode=$($Schedule.mode)`n")
    $null = $text.Append("seed=$($Schedule.seed)`n")
    $null = $text.Append("duration=$($Schedule.requestedDurationSeconds)`n")
    $null = $text.Append("quietStart=$($Schedule.quietStartSeconds)`n")
    $null = $text.Append("quietEnd=$($Schedule.quietEndSeconds)`n")
    $null = $text.Append("minRecovery=$($Schedule.minRecoveryIntervalSeconds)`n")
    $null = $text.Append("maxFaults=$($Schedule.maxFaults)`n")

    foreach ($item in $Schedule.events) {
        $null = $text.Append("event=$($item.offsetSeconds)|$($item.scenarioId)|$($item.kind)|$($item.target)`n")
    }

    return $text.ToString()
}

function Get-ScheduleHash {
    param([Parameter(Mandatory = $true)] $Schedule)
    return (Get-Fnv1a64 -Text (Get-ScheduleNormalizedText -Schedule $Schedule))
}

function Save-CampaignSchedule {
    param(
        [Parameter(Mandatory = $true)] $Schedule,
        [Parameter(Mandatory = $true)][string] $Path
    )

    $json = $Schedule | ConvertTo-Json -Depth 8
    Set-Content -Path $Path -Value $json -Encoding utf8
    return $Path
}
