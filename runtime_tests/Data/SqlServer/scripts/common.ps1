<#
    Shared helpers for the E4-SQL setup and cleanup scripts.

    Two rules govern everything in this file.

    The container is adopted, never owned: it was created by the repository
    owner, so these scripts may start, stop and inspect it and may never remove,
    recreate, or reconfigure it.

    The password is never printed. It is read from the environment variable
    named in container.json, kept inside a SqlConnectionStringBuilder, and no
    code path here writes it to the console, to a file, or to a command line.
#>

Set-StrictMode -Version 2.0

function Get-ScenarioRoot {
    return (Split-Path -Parent $PSScriptRoot)
}

function Get-ContainerDefinition {
    $path = Join-Path (Get-ScenarioRoot) 'container\container.json'
    if (-not (Test-Path $path)) {
        throw "The pinned container definition is missing: $path"
    }

    return (Get-Content -Path $path -Raw | ConvertFrom-Json)
}

<#
    Docker Desktop installs per user and does not always put docker.exe on the
    machine PATH, so reporting "no container engine" from a bare Get-Command
    would be wrong on a machine where the engine is running. Set
    NEKOLIB_DOCKER_CLI to override.
#>
function Get-DockerPath {
    if ($env:NEKOLIB_DOCKER_CLI) {
        if (Test-Path $env:NEKOLIB_DOCKER_CLI) { return $env:NEKOLIB_DOCKER_CLI }
        throw "NEKOLIB_DOCKER_CLI points at a path that does not exist: $env:NEKOLIB_DOCKER_CLI"
    }

    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\DockerDesktop\resources\bin\docker.exe'),
        (Join-Path $env:ProgramFiles 'Docker\Docker\resources\bin\docker.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Docker\Docker\resources\bin\docker.exe')
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) { return $candidate }
    }

    $onPath = Get-Command docker -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    throw 'No container CLI found. Install Docker Desktop or set NEKOLIB_DOCKER_CLI to an absolute docker.exe path.'
}

function Invoke-Docker {
    param(
        [Parameter(Mandatory = $true)][string[]] $Arguments
    )

    $docker = Get-DockerPath
    $output = & $docker @Arguments
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output   = ($output -join "`n").Trim()
    }
}

function Get-ContainerStatus {
    param([Parameter(Mandatory = $true)][string] $Name)

    $result = Invoke-Docker -Arguments @('inspect', $Name, '--format', '{{.State.Status}}')
    if ($result.ExitCode -ne 0) { return $null }
    return $result.Output
}

<#
    Reads the facts the evidence record needs. .Config.Env is deliberately never
    requested: that array holds the SA password.
#>
function Get-ContainerFacts {
    param([Parameter(Mandatory = $true)][string] $Name)

    $status = Get-ContainerStatus -Name $Name
    if ($null -eq $status) { return $null }

    $image = (Invoke-Docker -Arguments @('inspect', $Name, '--format', '{{.Config.Image}}')).Output
    $binding = (Invoke-Docker -Arguments @('inspect', $Name, '--format',
        '{{range $port, $bindings := .HostConfig.PortBindings}}{{range $bindings}}{{$port}}|{{.HostIp}}|{{.HostPort}};{{end}}{{end}}')).Output
    $mounts = (Invoke-Docker -Arguments @('inspect', $Name, '--format', '{{json .Mounts}}')).Output

    $digest = ''
    $architecture = ''
    if ($image) {
        $imageFacts = Invoke-Docker -Arguments @('image', 'inspect', $image, '--format',
            '{{index .RepoDigests 0}}|{{.Architecture}}')
        if ($imageFacts.ExitCode -eq 0) {
            $parts = $imageFacts.Output -split '\|'
            $digest = $parts[0]
            if ($parts.Length -gt 1) { $architecture = $parts[1] }
        }
    }

    $hostIp = ''
    $hostPort = ''
    foreach ($entry in ($binding -split ';')) {
        $parts = $entry -split '\|'
        if ($parts.Length -eq 3 -and $parts[0].StartsWith('1433/')) {
            $hostIp = $parts[1]
            $hostPort = $parts[2]
        }
    }

    return [pscustomobject]@{
        Name              = $Name
        Status            = $status
        Image             = $image
        ImageDigest       = $digest
        ImageArchitecture = $architecture
        HostIp            = $hostIp
        HostPort          = $hostPort
        Mounts            = $mounts
    }
}

<#
    A port that accepts a TCP connection, which is weaker than a server that
    accepts logins. The scenario process runs the real readiness probe; this is
    only here so the scripts do not hand back a container whose port is not open
    yet.
#>
function Wait-ForPort {
    param(
        [string] $ComputerName = '127.0.0.1',
        [int] $Port = 1433,
        [int] $TimeoutSeconds = 120
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        $client = New-Object System.Net.Sockets.TcpClient
        try {
            $async = $client.BeginConnect($ComputerName, $Port, $null, $null)
            if ($async.AsyncWaitHandle.WaitOne(1500) -and $client.Connected) {
                $client.EndConnect($async)
                return $true
            }
        } catch {
            # Not listening yet; the loop is the retry.
        } finally {
            $client.Close()
        }

        Start-Sleep -Milliseconds 500
    }

    return $false
}

function Test-PasswordVariable {
    param([Parameter(Mandatory = $true)][string] $Name)

    if ([Environment]::GetEnvironmentVariable($Name, 'Process')) { return 'process' }
    if ([Environment]::GetEnvironmentVariable($Name, 'User')) { return 'user' }
    if ([Environment]::GetEnvironmentVariable($Name, 'Machine')) { return 'machine' }
    return $null
}

function Get-ScenarioPassword {
    param([Parameter(Mandatory = $true)][string] $Name)

    foreach ($scope in @('Process', 'User', 'Machine')) {
        $value = [Environment]::GetEnvironmentVariable($Name, $scope)
        if ($value) { return $value }
    }

    throw "The environment variable $Name is not set. It is never stored in this repository."
}

<#
    Opens a connection to master with the built-in provider.

    System.Data.SqlClient rather than Microsoft.Data.SqlClient on purpose: the
    scripts must run from a plain PowerShell session without resolving the
    scenario's package graph, and nothing here is evidence about the provider -
    only the scenario executable's own connections are.
#>
function Open-MasterConnection {
    param(
        [Parameter(Mandatory = $true)] $Definition,
        [string] $ComputerName = '127.0.0.1',
        [int] $Port = 0
    )

    Add-Type -AssemblyName System.Data | Out-Null

    if ($Port -eq 0) { $Port = [int] $Definition.hostPort }

    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder['Data Source'] = "$ComputerName,$Port"
    $builder['Initial Catalog'] = 'master'
    $builder['User ID'] = $Definition.loginUser
    $builder['Password'] = (Get-ScenarioPassword -Name $Definition.passwordVariable)
    $builder['TrustServerCertificate'] = $true
    $builder['Connect Timeout'] = 15
    $builder['Application Name'] = 'NekoLib.E4-SQL.script'
    $builder['Pooling'] = $false

    $connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
    $connection.Open()
    return $connection
}

<#
    Opens a master connection, retrying until the engine actually accepts a
    login.

    An open port is not a ready server: SQL Server binds 1433 well before it
    finishes recovering its databases, and a connection that arrives too early
    is refused during the pre-login handshake. Connecting once and reporting the
    refusal would blame the script's timing on the server.
#>
function Connect-MasterWithRetry {
    param(
        [Parameter(Mandatory = $true)] $Definition,
        [int] $TimeoutSeconds = 180
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $last = 'never attempted'

    while ((Get-Date) -lt $deadline) {
        try {
            return (Open-MasterConnection -Definition $Definition)
        } catch {
            $last = $_.Exception.Message
            Start-Sleep -Milliseconds 750
        }
    }

    throw "The server never accepted a login within $TimeoutSeconds seconds. Last error: $last"
}

function Get-ScenarioDatabases {
    param(
        [Parameter(Mandatory = $true)] $Connection,
        [Parameter(Mandatory = $true)][string] $Prefix
    )

    $command = $Connection.CreateCommand()
    $command.CommandText = "SELECT name FROM sys.databases WHERE name LIKE @prefix + N'%' ORDER BY name"
    $null = $command.Parameters.AddWithValue('@prefix', $Prefix)

    $names = @()
    $reader = $command.ExecuteReader()
    try {
        while ($reader.Read()) { $names += $reader.GetString(0) }
    } finally {
        $reader.Close()
    }

    return $names
}

function Remove-ScenarioDatabase {
    param(
        [Parameter(Mandatory = $true)] $Connection,
        [Parameter(Mandatory = $true)][string] $Name,
        [Parameter(Mandatory = $true)][string] $Prefix
    )

    # A guard, not a formality: this function is the only place these scripts
    # can destroy anything, and it must never be able to drop a database that
    # does not belong to the scenario.
    if (-not $Name.StartsWith($Prefix, [StringComparison]::Ordinal)) {
        throw "Refusing to drop '$Name': it does not carry the scenario prefix '$Prefix'."
    }

    if ($Name -match '[\]\[;]') {
        throw "Refusing to drop '$Name': the name contains characters that cannot appear in a generated scenario database."
    }

    $command = $Connection.CreateCommand()
    $command.CommandText = @"
IF DB_ID(@name) IS NOT NULL
BEGIN
    ALTER DATABASE [$Name] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$Name];
END
"@
    $null = $command.Parameters.AddWithValue('@name', $Name)
    $null = $command.ExecuteNonQuery()
}
