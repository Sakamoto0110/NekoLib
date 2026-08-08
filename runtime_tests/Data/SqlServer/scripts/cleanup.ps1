<#
.SYNOPSIS
    Reports and, on request, removes what an E4-SQL run left behind.

.DESCRIPTION
    Scope is deliberately narrow. The only things this script will remove are
    databases carrying the scenario prefix from container.json, and it refuses
    any other name. The adopted container is never removed or recreated; -Stop
    only stops it.

    A normal run cleans up after itself. This script exists for the runs that
    did not: a killed process, a machine that rebooted, a -KeepDatabase run
    whose database is no longer wanted.

.PARAMETER DropDatabases
    Drops every database carrying the scenario prefix. Without it, they are only
    listed.

.PARAMETER Stop
    Stops the adopted container after the database work.

.EXAMPLE
    .\cleanup.ps1
    .\cleanup.ps1 -DropDatabases -Stop
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch] $DropDatabases,
    [switch] $Stop
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'common.ps1')

$definition = Get-ContainerDefinition
$prefix = $definition.scenarioDatabasePrefix

Write-Host "E4-SQL cleanup"
Write-Host ""

$facts = Get-ContainerFacts -Name $definition.containerName
if ($null -eq $facts) {
    Write-Host ("  container  no container named '{0}'; nothing to reconcile" -f $definition.containerName)
    exit 0
}

Write-Host ("  container  {0}  status {1}" -f $facts.Name, $facts.Status)

if ($facts.Status -eq 'paused') {
    Write-Host "  container  unpausing, because a paused container looks like a hung machine to the next run"
    $null = Invoke-Docker -Arguments @('unpause', $facts.Name)
    $facts = Get-ContainerFacts -Name $definition.containerName
}

if ($facts.Status -ne 'running') {
    Write-Host "  databases  cannot be inspected while the container is stopped"
    if ($DropDatabases) {
        Write-Host "             start it first, or run setup.ps1 -Start"
    }
} else {
    $scope = Test-PasswordVariable -Name $definition.passwordVariable
    if (-not $scope) {
        Write-Host ("  databases  cannot be inspected: {0} is not set" -f $definition.passwordVariable)
    } else {
        $connection = $null
        try {
            $connection = Connect-MasterWithRetry -Definition $definition
            $names = @(Get-ScenarioDatabases -Connection $connection -Prefix $prefix)

            if ($names.Count -eq 0) {
                Write-Host ("  databases  none carrying the prefix '{0}'" -f $prefix)
            } else {
                Write-Host ("  databases  {0} carrying the prefix '{1}':" -f $names.Count, $prefix)
                foreach ($name in $names) { Write-Host ("               {0}" -f $name) }

                if ($DropDatabases) {
                    foreach ($name in $names) {
                        if ($PSCmdlet.ShouldProcess($name, 'DROP DATABASE')) {
                            Remove-ScenarioDatabase -Connection $connection -Name $name -Prefix $prefix
                            Write-Host ("               dropped {0}" -f $name)
                        }
                    }
                } else {
                    Write-Host "             pass -DropDatabases to remove them"
                }
            }
        } finally {
            if ($connection) { $connection.Dispose() }
        }
    }
}

if ($Stop) {
    if ($PSCmdlet.ShouldProcess($facts.Name, 'docker stop')) {
        $result = Invoke-Docker -Arguments @('stop', $facts.Name)
        if ($result.ExitCode -eq 0) {
            Write-Host ("  container  stopped")
        } else {
            Write-Host ("  container  stop failed - {0}" -f $result.Output)
            exit 6
        }
    }
}

Write-Host ""
Write-Host "Cleanup complete. The container itself was never removed or recreated."
exit 0
