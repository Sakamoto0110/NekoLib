[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string[]]$PackageId,

    [switch]$UpdateBaseline,

    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$srcRoot = Join-Path $repoRoot "src"
$solutionPath = Join-Path $repoRoot "NekoLib.sln"
$toolProject = Join-Path $repoRoot "src\Tools\NekoLib.PublicApiTool\NekoLib.PublicApiTool.csproj"
$baselineRoot = Join-Path $PSScriptRoot "public-api"
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts"))
$receivedRoot = [IO.Path]::GetFullPath((Join-Path $artifactsRoot "public-api"))

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-ProjectProperty {
    param(
        [Parameter(Mandatory = $true)]$ProjectXml,
        [Parameter(Mandatory = $true)][string]$Name
    )

    foreach ($propertyGroup in $ProjectXml.Project.PropertyGroup) {
        $property = $propertyGroup.$Name
        if ($null -ne $property) {
            $value = ([string]$property).Trim()
            if ($value.Length -gt 0) {
                return $value
            }
        }
    }

    return $null
}

function Get-NormalizedText {
    param([Parameter(Mandatory = $true)][string]$Path)

    return ([IO.File]::ReadAllText($Path) -replace "`r`n?", "`n")
}

$artifactsPrefix = $artifactsRoot.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar

if (-not $receivedRoot.StartsWith(
        $artifactsPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Public API output escaped the repository artifacts directory: $receivedRoot"
}

$projects = @(
    Get-ChildItem -LiteralPath $srcRoot -Recurse -File -Filter "*.csproj" |
        ForEach-Object {
            [xml]$projectXml = Get-Content -LiteralPath $_.FullName -Raw
            $isPackable = Get-ProjectProperty -ProjectXml $projectXml -Name "IsPackable"
            if ($isPackable -ne "true") {
                return
            }

            $package = Get-ProjectProperty -ProjectXml $projectXml -Name "PackageId"
            $targetFrameworks = Get-ProjectProperty -ProjectXml $projectXml -Name "TargetFrameworks"
            if ([string]::IsNullOrWhiteSpace($targetFrameworks)) {
                $targetFrameworks = Get-ProjectProperty -ProjectXml $projectXml -Name "TargetFramework"
            }

            if ([string]::IsNullOrWhiteSpace($package) -or
                [string]::IsNullOrWhiteSpace($targetFrameworks)) {
                throw "Packable project must declare PackageId and target frameworks: $($_.FullName)"
            }

            $assemblyName = Get-ProjectProperty -ProjectXml $projectXml -Name "AssemblyName"
            if ([string]::IsNullOrWhiteSpace($assemblyName)) {
                $assemblyName = $_.BaseName
            }

            [pscustomobject]@{
                PackageId = $package
                ProjectPath = $_.FullName
                ProjectDirectory = $_.DirectoryName
                AssemblyName = $assemblyName
                TargetFrameworks = @(
                    $targetFrameworks.Split(';') |
                        ForEach-Object { $_.Trim() } |
                        Where-Object { $_.Length -gt 0 })
            }
        } |
        Sort-Object PackageId)

if ($projects.Count -eq 0) {
    throw "No packable library projects were discovered under src."
}

$allProjects = $projects
if ($PackageId.Count -gt 0) {
    $unknownPackages = @(
        $PackageId | Where-Object { $_ -notin $allProjects.PackageId })
    if ($unknownPackages.Count -gt 0) {
        throw "Unknown library package(s): $($unknownPackages -join ', ')"
    }

    $projects = @($allProjects | Where-Object { $_.PackageId -in $PackageId })
}

if (-not $NoBuild) {
    if ($projects.Count -eq $allProjects.Count) {
        Invoke-DotNet -Arguments @(
            "build", $solutionPath, "-c", $Configuration, "--nologo",
            "-clp:ErrorsOnly;Summary")
    }
    else {
        foreach ($project in $projects) {
            Invoke-DotNet -Arguments @(
                "build", $project.ProjectPath, "-c", $Configuration, "--nologo",
                "-clp:ErrorsOnly;Summary")
        }
    }

    Invoke-DotNet -Arguments @(
        "build", $toolProject, "-c", $Configuration, "--nologo",
        "-clp:ErrorsOnly;Summary")
}

if (Test-Path -LiteralPath $receivedRoot) {
    Remove-Item -LiteralPath $receivedRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $receivedRoot | Out-Null

$expectedBaselines = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
$mismatches = [Collections.Generic.List[string]]::new()

foreach ($project in $projects) {
    foreach ($targetFramework in $project.TargetFrameworks) {
        $assemblyPath = Join-Path $project.ProjectDirectory (
            "bin\$Configuration\$targetFramework\$($project.AssemblyName).dll")
        if (-not (Test-Path -LiteralPath $assemblyPath)) {
            throw "Built assembly not found: $assemblyPath"
        }

        $runnerFramework = if ($targetFramework -eq "net481") {
            "net481"
        }
        else {
            "net9.0-windows"
        }

        $receivedDirectory = Join-Path $receivedRoot $project.PackageId
        $receivedPath = Join-Path $receivedDirectory "$targetFramework.received.txt"
        New-Item -ItemType Directory -Force -Path $receivedDirectory | Out-Null

        Invoke-DotNet -Arguments @(
            "run",
            "--project", $toolProject,
            "-c", $Configuration,
            "-f", $runnerFramework,
            "--no-build",
            "--",
            $assemblyPath,
            $receivedPath)

        $baselineDirectory = Join-Path $baselineRoot $project.PackageId
        $baselinePath = [IO.Path]::GetFullPath(
            (Join-Path $baselineDirectory "$targetFramework.approved.txt"))
        $expectedBaselines.Add($baselinePath) | Out-Null

        if ($UpdateBaseline) {
            New-Item -ItemType Directory -Force -Path $baselineDirectory | Out-Null
            [IO.File]::WriteAllText(
                $baselinePath,
                (Get-NormalizedText -Path $receivedPath),
                [Text.UTF8Encoding]::new($false))
            Write-Host "Updated $($project.PackageId) $targetFramework"
            continue
        }

        if (-not (Test-Path -LiteralPath $baselinePath)) {
            $mismatches.Add("Missing baseline: $baselinePath")
            continue
        }

        $expected = Get-NormalizedText -Path $baselinePath
        $actual = Get-NormalizedText -Path $receivedPath
        if ($expected -cne $actual) {
            $mismatches.Add(
                "API mismatch: $($project.PackageId) $targetFramework`n" +
                "  baseline: $baselinePath`n" +
                "  received: $receivedPath")

            Write-Host "Public API diff for $($project.PackageId) $targetFramework"
            & git -c core.autocrlf=false --no-pager diff --no-index --text -- $baselinePath $receivedPath
            if ($LASTEXITCODE -gt 1) {
                throw "Unable to render the public API diff for $($project.PackageId) $targetFramework."
            }
        }
        else {
            Write-Host "Verified $($project.PackageId) $targetFramework"
        }
    }
}

if ($PackageId.Count -eq 0 -and (Test-Path -LiteralPath $baselineRoot)) {
    $staleBaselines = @(
        Get-ChildItem -LiteralPath $baselineRoot -Recurse -File -Filter "*.approved.txt" |
            Where-Object { -not $expectedBaselines.Contains($_.FullName) })
    foreach ($staleBaseline in $staleBaselines) {
        $mismatches.Add("Stale baseline: $($staleBaseline.FullName)")
    }
}

if ($mismatches.Count -gt 0) {
    foreach ($mismatch in $mismatches) {
        Write-Error $mismatch -ErrorAction Continue
    }

    throw "Public API verification failed with $($mismatches.Count) mismatch(es)."
}

if ($UpdateBaseline) {
    Write-Host "Updated $($expectedBaselines.Count) public API baseline(s)."
}
else {
    Write-Host "Verified $($expectedBaselines.Count) public API baseline(s)."
}
