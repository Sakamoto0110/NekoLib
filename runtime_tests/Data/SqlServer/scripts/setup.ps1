<#
.SYNOPSIS
    Verifies the E4-SQL prerequisites and, with -Start, brings the adopted
    container up.

.DESCRIPTION
    This script checks; it does not build. The container named in
    container.json belongs to the repository owner, and nothing here creates,
    removes, or reconfigures it. Where the machine does not match the pinned
    definition, the difference is reported as a setup gap and left alone.

    The SA password is never printed. The script reports only whether the
    variable is set and in which scope.

.PARAMETER Start
    Starts the container if it is stopped and waits for the database port.

.PARAMETER TimeoutSeconds
    How long to wait for the port after starting. Default 180.

.EXAMPLE
    .\setup.ps1
    .\setup.ps1 -Start
#>
[CmdletBinding()]
param(
    [switch] $Start,
    [int] $TimeoutSeconds = 180
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'common.ps1')

$definition = Get-ContainerDefinition
$gaps = @()

Write-Host "E4-SQL preflight"
Write-Host ""

try {
    $docker = Get-DockerPath
    $version = (Invoke-Docker -Arguments @('version', '--format', '{{.Server.Version}}')).Output
    Write-Host ("  engine     {0} (server {1})" -f $docker, $version)
} catch {
    Write-Host ("  engine     MISSING - {0}" -f $_.Exception.Message)
    exit 3
}

$facts = Get-ContainerFacts -Name $definition.containerName
if ($null -eq $facts) {
    Write-Host ("  container  MISSING - no container named '{0}'" -f $definition.containerName)
    Write-Host ""
    Write-Host "  This scenario adopts an existing container and never creates one."
    Write-Host "  See the scenario README for what the container has to look like."
    exit 3
}

Write-Host ("  container  {0}  status {1}" -f $facts.Name, $facts.Status)
Write-Host ("  image      {0}" -f $facts.Image)
Write-Host ("  digest     {0}" -f $facts.ImageDigest)
Write-Host ("  published  HostIp '{0}' port {1}" -f $facts.HostIp, $facts.HostPort)
Write-Host ("  mounts     {0}" -f $facts.Mounts)

if ($facts.Image -ne $definition.image) {
    $gaps += "image is '$($facts.Image)' but the pinned definition names '$($definition.image)'"
}

if ($definition.imageDigest -and $facts.ImageDigest -and $facts.ImageDigest -ne $definition.imageDigest) {
    $gaps += "image digest is '$($facts.ImageDigest)' but the pinned definition records '$($definition.imageDigest)'"
}

if ($facts.HostPort -ne [string] $definition.hostPort) {
    $gaps += "published host port is '$($facts.HostPort)' but the pinned definition expects $($definition.hostPort)"
}

# An empty HostIp means every interface. Connecting through localhost does not
# establish the loopback-only requirement, and recreating a user-owned container
# to change the binding is outside this scenario's authority.
if ($facts.HostIp -ne '127.0.0.1' -and $facts.HostIp -ne '::1') {
    $shown = $facts.HostIp
    if (-not $shown) { $shown = '(every interface)' }
    $gaps += "the database port is published on HostIp '$shown', so local-only exposure is not established"
}

$scope = Test-PasswordVariable -Name $definition.passwordVariable
if ($scope) {
    Write-Host ("  password   {0} is set ({1} scope); its value is never printed" -f $definition.passwordVariable, $scope)
} else {
    Write-Host ("  password   {0} is NOT set" -f $definition.passwordVariable)
    $gaps += "$($definition.passwordVariable) is not set; the scenario cannot log in without it"
}

if ($Start -and $facts.Status -ne 'running') {
    Write-Host ""
    Write-Host ("  starting   {0}" -f $facts.Name)
    $result = Invoke-Docker -Arguments @('start', $facts.Name)
    if ($result.ExitCode -ne 0) {
        Write-Host ("  starting   FAILED - {0}" -f $result.Output)
        exit 3
    }

    if (Wait-ForPort -Port ([int] $definition.hostPort) -TimeoutSeconds $TimeoutSeconds) {
        Write-Host ("  port       {0} is accepting connections" -f $definition.hostPort)
    } else {
        Write-Host ("  port       {0} never opened within {1}s" -f $definition.hostPort, $TimeoutSeconds)
        exit 3
    }

    # The port opens well before the engine accepts logins, so waiting only for
    # the port hands back a container the next command cannot use.
    if ($scope) {
        try {
            $probe = Connect-MasterWithRetry -Definition $definition -TimeoutSeconds $TimeoutSeconds
            $probe.Dispose()
            Write-Host "  login      the engine accepted a login"
        } catch {
            Write-Host ("  login      {0}" -f $_.Exception.Message)
            exit 3
        }
    } else {
        Write-Host "  login      not checked: the password variable is not set"
    }
} elseif ($facts.Status -eq 'running') {
    Write-Host ""
    if (Wait-ForPort -Port ([int] $definition.hostPort) -TimeoutSeconds 5) {
        Write-Host ("  port       {0} is accepting connections" -f $definition.hostPort)
    } else {
        Write-Host ("  port       {0} is not accepting connections although the container is running" -f $definition.hostPort)
    }
}

Write-Host ""

if ($gaps.Count -gt 0) {
    Write-Host ("Setup gaps ({0}) - recorded, not corrected:" -f $gaps.Count)
    foreach ($gap in $gaps) { Write-Host ("  - {0}" -f $gap) }
    Write-Host ""
}

# A gap is not a failure. The scenario records the same gaps in its own
# environment.json, and the only hard stops above are a missing engine, a
# missing container, or a port that never opened.
Write-Host "Preflight complete."
exit 0
