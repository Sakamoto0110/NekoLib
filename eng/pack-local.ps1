[CmdletBinding()]
param(
    [Parameter()]
    [string]$PackageVersion = "1.0.0-local.3",

    [Parameter()]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [Parameter()]
    [switch]$SkipTests,

    [Parameter()]
    [switch]$SkipPackageSmokeTests,

    [Parameter()]
    [switch]$AllowDirty,

    [Parameter()]
    [switch]$KeepIntermediateArtifacts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    Write-Host "dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet exited with code $LASTEXITCODE."
    }
}

function Get-VerifiedChildPath {
    param(
        [Parameter(Mandatory)]
        [string]$ParentPath,

        [Parameter(Mandatory)]
        [string]$ChildPath
    )

    $parentFullPath = [System.IO.Path]::GetFullPath($ParentPath).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    )
    $childFullPath = [System.IO.Path]::GetFullPath((Join-Path $parentFullPath $ChildPath))
    $requiredPrefix = $parentFullPath + [System.IO.Path]::DirectorySeparatorChar

    if (-not $childFullPath.StartsWith($requiredPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to use an artifact path outside $parentFullPath`: $childFullPath"
    }

    return $childFullPath
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solution = Join-Path $repoRoot "NekoLib.sln"
$hostProject = Join-Path $repoRoot "src\Watchdog\NekoLib.Watchdog.Host\NekoLib.Watchdog.Host.csproj"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$localFeed = Join-Path $artifactsRoot "local-feed"
$sessionId = [Guid]::NewGuid().ToString("N")

$packageIds = @(
    "NekoLib.Core",
    "NekoLib.Data",
    "NekoLib.Inspection",
    "NekoLib.Devices",
    "NekoLib.Diagnostics",
    "NekoLib.Diagnostics.Windows",
    "NekoLib.Logging",
    "NekoLib.Mvvm",
    "NekoLib.Navigation",
    "NekoLib.Navigation.WinForms",
    "NekoLib.Navigation.Wpf",
    "NekoLib.Pipes",
    "NekoLib.Telemetry",
    "NekoLib.Watchdog",
    "NekoLib.Watchdog.Host"
)

if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    throw "PackageVersion cannot be empty."
}

if ($PackageVersion.IndexOfAny([System.IO.Path]::GetInvalidFileNameChars()) -ge 0) {
    throw "PackageVersion contains characters that are not valid in an artifact path: $PackageVersion"
}

$packageOutput = Get-VerifiedChildPath `
    -ParentPath (Join-Path $artifactsRoot "packages") `
    -ChildPath "$PackageVersion\$sessionId"
$hostPayloadRoot = Get-VerifiedChildPath `
    -ParentPath (Join-Path $artifactsRoot "staging\watchdog-host") `
    -ChildPath $sessionId

$gitStatus = @(& git -C $repoRoot status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect the Git worktree before packing."
}

if (-not $AllowDirty -and $gitStatus.Count -gt 0) {
    throw @"
The Git worktree is not clean. Commit or stash the changes before publishing so
RepositoryCommit and Source Link identify the exact package sources.
Use -AllowDirty only for disposable validation packages.
"@
}

$repositoryCommit = (& git -C $repoRoot rev-parse HEAD | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryCommit)) {
    throw "Unable to resolve the repository commit before packing."
}

$expectedArtifactNames = @(
    foreach ($packageId in $packageIds) {
        "$packageId.$PackageVersion.nupkg"
        if ($packageId -ne "NekoLib.Watchdog.Host") {
            "$packageId.$PackageVersion.snupkg"
        }
    }
)

New-Item -ItemType Directory -Force -Path $localFeed | Out-Null

$existingFeedArtifacts = @(
    foreach ($artifactName in $expectedArtifactNames) {
        $candidate = Join-Path $localFeed $artifactName
        if (Test-Path -LiteralPath $candidate) {
            $candidate
        }
    }
)

if ($existingFeedArtifacts.Count -gt 0) {
    throw @"
Version $PackageVersion already exists in the local feed:
$($existingFeedArtifacts -join [Environment]::NewLine)
NuGet package versions are immutable. Choose a new PackageVersion.
"@
}

New-Item -ItemType Directory -Force -Path $packageOutput | Out-Null
New-Item -ItemType Directory -Force -Path $hostPayloadRoot | Out-Null

$commonProperties = @(
    "-p:NekoLibPackageVersion=$PackageVersion",
    "-p:Version=$PackageVersion",
    "-p:PackageVersion=$PackageVersion",
    "-p:RepositoryCommit=$repositoryCommit"
)

Invoke-DotNet (@("build", $solution, "-c", $Configuration) + $commonProperties)

if (-not $SkipTests) {
    Invoke-DotNet (@(
        "test",
        $solution,
        "-c",
        $Configuration,
        "--no-build",
        "--no-restore"
    ) + $commonProperties)
}

Invoke-DotNet (@(
    "publish",
    $hostProject,
    "-c",
    $Configuration,
    "-f",
    "net481",
    "--no-restore",
    "-o",
    (Join-Path $hostPayloadRoot "net481")
) + $commonProperties)

foreach ($rid in @("win-x86", "win-x64")) {
    Invoke-DotNet (@(
        "publish",
        $hostProject,
        "-c",
        $Configuration,
        "-f",
        "net9.0-windows",
        "-r",
        $rid,
        "--self-contained",
        "false",
        "--no-restore",
        "-o",
        (Join-Path $hostPayloadRoot "net9.0-windows\$rid")
    ) + $commonProperties)
}

Invoke-DotNet (@(
    "pack",
    $solution,
    "-c",
    $Configuration,
    "--no-build",
    "--no-restore",
    "-p:NekoLibWatchdogHostPayloadRoot=$hostPayloadRoot",
    "-p:PackageOutputPath=$packageOutput"
) + $commonProperties)

foreach ($artifactName in $expectedArtifactNames) {
    $artifactPath = Join-Path $packageOutput $artifactName
    if (-not (Test-Path -LiteralPath $artifactPath)) {
        throw "Expected package artifact was not generated: $artifactPath"
    }
}

$actualArtifactNames = @(
    Get-ChildItem -LiteralPath $packageOutput -File |
        Where-Object { $_.Extension -eq ".nupkg" -or $_.Extension -eq ".snupkg" } |
        Select-Object -ExpandProperty Name
)
$unexpectedArtifacts = @(
    $actualArtifactNames | Where-Object { $_ -notin $expectedArtifactNames }
)
if ($unexpectedArtifacts.Count -gt 0) {
    throw "Unexpected package artifacts were generated: $($unexpectedArtifacts -join ', ')"
}

if (-not $SkipPackageSmokeTests) {
    $smokeArguments = @{
        PackageVersion = $PackageVersion
        FeedPath = $packageOutput
        Configuration = $Configuration
    }
    if ($KeepIntermediateArtifacts) {
        $smokeArguments.KeepArtifacts = $true
    }

    & (Join-Path $PSScriptRoot "test-local-packages.ps1") @smokeArguments

    if ($LASTEXITCODE -ne 0) {
        throw "Package smoke tests exited with code $LASTEXITCODE."
    }
}

$finalRepositoryCommit = (& git -C $repoRoot rev-parse HEAD | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $finalRepositoryCommit -ne $repositoryCommit) {
    throw "The repository commit changed while packages were being produced. Run the pack again."
}

if (-not $AllowDirty) {
    $finalGitStatus = @(& git -C $repoRoot status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0 -or $finalGitStatus.Count -gt 0) {
        throw "The Git worktree changed while packages were being produced. Run the pack again."
    }
}

$publishPlan = @(
    foreach ($artifactName in $expectedArtifactNames) {
        [pscustomobject]@{
            Source = Join-Path $packageOutput $artifactName
            Destination = Join-Path $localFeed $artifactName
        }
    }
)

foreach ($publishItem in $publishPlan) {
    $destination = $publishItem.Destination
    if (Test-Path -LiteralPath $destination) {
        throw "Refusing to overwrite an existing local-feed artifact: $destination"
    }
}

$publishedDestinations = [System.Collections.Generic.List[string]]::new()
try {
    foreach ($publishItem in $publishPlan) {
        [System.IO.File]::Copy($publishItem.Source, $publishItem.Destination, $false)
        $publishedDestinations.Add($publishItem.Destination)
    }
}
catch {
    foreach ($publishedDestination in $publishedDestinations) {
        if (Test-Path -LiteralPath $publishedDestination) {
            Remove-Item -LiteralPath $publishedDestination -Force
        }
    }

    throw
}

if (-not $KeepIntermediateArtifacts) {
    foreach ($generatedDirectory in @($packageOutput, $hostPayloadRoot)) {
        if (Test-Path -LiteralPath $generatedDirectory) {
            Remove-Item -LiteralPath $generatedDirectory -Recurse -Force
        }
    }
}

Write-Host ""
Write-Host "Published $($packageIds.Count) packages to:"
Write-Host "  $localFeed"
Write-Host "Package version:"
Write-Host "  $PackageVersion"
