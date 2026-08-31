[CmdletBinding(DefaultParameterSetName = "Search")]
param(
    [Parameter(Mandatory = $true, ParameterSetName = "Search", Position = 0)]
    [string]$Query,

    [Parameter(ParameterSetName = "Search")]
    [string]$Boundary,

    [Parameter(ParameterSetName = "Search")]
    [string]$Kind,

    [Parameter(ParameterSetName = "Search")]
    [ValidateSet("all", "markdown", "xml")]
    [string]$Source = "all",

    [Parameter(ParameterSetName = "Search")]
    [ValidateRange(1, 200)]
    [int]$Limit = 20,

    [Parameter(ParameterSetName = "Search")]
    [switch]$AllowStale,

    [Parameter(Mandatory = $true, ParameterSetName = "Status")]
    [switch]$Status,

    [switch]$Json,

    [string]$DatabasePath
)

$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$indexerPath = Join-Path $PSScriptRoot "documentation_index.py"
$defaultDatabase = Join-Path $repoRoot ".local\documentation-migration\documentation-index.sqlite3"
$database = [IO.Path]::GetFullPath(
    $(if ([string]::IsNullOrWhiteSpace($DatabasePath)) {
        $defaultDatabase
    } elseif ([IO.Path]::IsPathRooted($DatabasePath)) {
        $DatabasePath
    } else {
        Join-Path $repoRoot $DatabasePath
    }))

if (-not (Test-Path -LiteralPath $indexerPath -PathType Leaf)) {
    throw "Documentation indexer is missing: $indexerPath"
}

$arguments = @(
    $indexerPath,
    "--root", $repoRoot,
    "--database", $database
)

if ($PSCmdlet.ParameterSetName -eq "Status") {
    $arguments += "status"
    if ($Json) { $arguments += "--json" }
} else {
    $arguments += @("search", "--query", $Query, "--source", $Source, "--limit", $Limit)
    if (-not [string]::IsNullOrWhiteSpace($Boundary)) {
        $arguments += @("--boundary", $Boundary)
    }
    if (-not [string]::IsNullOrWhiteSpace($Kind)) {
        $arguments += @("--kind", $Kind)
    }
    if ($AllowStale) { $arguments += "--allow-stale" }
    if ($Json) { $arguments += "--json" }
}

& python @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Documentation index command failed with exit code $LASTEXITCODE."
}
