[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$NoBuild,

    [string]$DatabasePath
)

$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$indexerPath = Join-Path $PSScriptRoot "documentation_index.py"
$apiVerifierPath = Join-Path $PSScriptRoot "verify-public-api.ps1"
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
if (-not (Test-Path -LiteralPath $apiVerifierPath -PathType Leaf)) {
    throw "Public API verifier is missing: $apiVerifierPath"
}

$treeStatus = @(& git -C $repoRoot status --porcelain=v1 --untracked-files=normal)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect the repository working tree."
}
if ($treeStatus.Count -gt 0) {
    throw "Documentation index refresh requires a clean Git working tree."
}

Push-Location $repoRoot
try {
    $apiArguments = @{
        Configuration = $Configuration
    }
    if ($NoBuild) {
        $apiArguments.NoBuild = $true
    }

    & $apiVerifierPath @apiArguments
    if (-not $?) {
        throw "Public API verification did not complete successfully."
    }

    & python $indexerPath --root $repoRoot --database $database `
        build --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Documentation index build failed with exit code $LASTEXITCODE."
    }

    & python $indexerPath --root $repoRoot --database $database `
        status --require-current
    if ($LASTEXITCODE -ne 0) {
        throw "Documentation index freshness verification failed with exit code $LASTEXITCODE."
    }
} finally {
    Pop-Location
}
