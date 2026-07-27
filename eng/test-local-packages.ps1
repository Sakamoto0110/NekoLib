[CmdletBinding()]
param(
    [Parameter()]
    [string]$PackageVersion = "1.0.0-local.3",

    [Parameter()]
    [string]$FeedPath,

    [Parameter()]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [Parameter()]
    [switch]$KeepArtifacts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

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

function Get-PackageEntries {
    param(
        [Parameter(Mandatory)]
        [string]$PackagePath
    )

    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        return @($archive.Entries | ForEach-Object { $_.FullName })
    }
    finally {
        $archive.Dispose()
    }
}

function Get-PackagePeMachine {
    param(
        [Parameter(Mandatory)]
        [string]$PackagePath,

        [Parameter(Mandatory)]
        [string]$EntryName
    )

    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entry = $archive.GetEntry($EntryName)
        if ($null -eq $entry) {
            throw "Package entry does not exist: $EntryName"
        }

        $entryStream = $entry.Open()
        $memory = New-Object System.IO.MemoryStream
        try {
            $entryStream.CopyTo($memory)
            $memory.Position = 0x3c

            $reader = New-Object System.IO.BinaryReader($memory)
            try {
                $peHeaderOffset = $reader.ReadInt32()
                $memory.Position = $peHeaderOffset + 4
                return $reader.ReadUInt16()
            }
            finally {
                $reader.Dispose()
            }
        }
        finally {
            $entryStream.Dispose()
            $memory.Dispose()
        }
    }
    finally {
        $archive.Dispose()
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
        throw "Refusing to use a smoke-test path outside $parentFullPath`: $childFullPath"
    }

    return $childFullPath
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$consumerRoot = Join-Path $repoRoot "tests\NekoLib.PackageConsumers"
$nugetConfig = Join-Path $consumerRoot "NuGet.Config"

if ([string]::IsNullOrWhiteSpace($FeedPath)) {
    $FeedPath = Join-Path $repoRoot "artifacts\local-feed"
}

$FeedPath = [System.IO.Path]::GetFullPath($FeedPath)
if (-not (Test-Path -LiteralPath $FeedPath -PathType Container)) {
    throw "Package feed does not exist: $FeedPath"
}

$packageTfms = [ordered]@{
    "NekoLib.Core" = "net9.0"
    "NekoLib.Data" = "net9.0"
    "NekoLib.DebugUtils" = "net9.0"
    "NekoLib.Devices" = "net9.0"
    "NekoLib.Diagnostics" = "net9.0"
    "NekoLib.Diagnostics.Windows" = "net9.0-windows7.0"
    "NekoLib.Logger" = "net9.0"
    "NekoLib.Mvvm" = "net9.0"
    "NekoLib.Navigation" = "net9.0"
    "NekoLib.Navigation.WinForms" = "net9.0-windows7.0"
    "NekoLib.Navigation.Wpf" = "net9.0-windows7.0"
    "NekoLib.Pipes" = "net9.0"
    "NekoLib.Watchdog" = "net9.0-windows7.0"
}

foreach ($entry in $packageTfms.GetEnumerator()) {
    $packagePath = Join-Path $FeedPath "$($entry.Key).$PackageVersion.nupkg"
    if (-not (Test-Path -LiteralPath $packagePath)) {
        throw "Expected package is missing from the smoke-test feed: $packagePath"
    }

    $entries = Get-PackageEntries -PackagePath $packagePath
    $net481Assembly = "lib/net481/$($entry.Key).dll"
    $net9Assembly = "lib/$($entry.Value)/$($entry.Key).dll"

    if ($entries -notcontains $net481Assembly) {
        throw "$($entry.Key) is missing $net481Assembly."
    }

    if ($entries -notcontains $net9Assembly) {
        throw "$($entry.Key) is missing $net9Assembly."
    }
}

$hostPackagePath = Join-Path $FeedPath "NekoLib.Watchdog.Host.$PackageVersion.nupkg"
if (-not (Test-Path -LiteralPath $hostPackagePath)) {
    throw "Expected Host package is missing: $hostPackagePath"
}

$hostEntries = Get-PackageEntries -PackagePath $hostPackagePath
$requiredHostEntries = @(
    "build/NekoLib.Watchdog.Host.targets",
    "buildTransitive/NekoLib.Watchdog.Host.targets",
    "tools/net481/NekoLib.Watchdog.Host.exe",
    "tools/net9.0-windows7.0/win-x86/NekoLib.Watchdog.Host.exe",
    "tools/net9.0-windows7.0/win-x64/NekoLib.Watchdog.Host.exe"
)

foreach ($requiredEntry in $requiredHostEntries) {
    if ($hostEntries -notcontains $requiredEntry) {
        throw "NekoLib.Watchdog.Host is missing $requiredEntry."
    }
}

if (@($hostEntries | Where-Object { $_.StartsWith("lib/", [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0) {
    throw "NekoLib.Watchdog.Host must not expose a library under lib/."
}

$x86Machine = Get-PackagePeMachine `
    -PackagePath $hostPackagePath `
    -EntryName "tools/net9.0-windows7.0/win-x86/NekoLib.Watchdog.Host.exe"
$x64Machine = Get-PackagePeMachine `
    -PackagePath $hostPackagePath `
    -EntryName "tools/net9.0-windows7.0/win-x64/NekoLib.Watchdog.Host.exe"

if ($x86Machine -ne 0x014c) {
    throw "The win-x86 Watchdog Host apphost has unexpected PE machine 0x$($x86Machine.ToString('x4'))."
}

if ($x64Machine -ne 0x8664) {
    throw "The win-x64 Watchdog Host apphost has unexpected PE machine 0x$($x64Machine.ToString('x4'))."
}

$projectReferenceMatches = @(
    Get-ChildItem -LiteralPath $consumerRoot -Filter "*.csproj" -Recurse |
        Select-String -Pattern "<ProjectReference"
)
if ($projectReferenceMatches.Count -gt 0) {
    throw "Package consumers must not contain ProjectReference entries."
}

$consumerProjects = @(
    (Join-Path $consumerRoot "WinForms481\WinForms481.csproj"),
    (Join-Path $consumerRoot "WinForms9\WinForms9.csproj"),
    (Join-Path $consumerRoot "Wpf9\Wpf9.csproj"),
    (Join-Path $consumerRoot "Wpf481\Wpf481.csproj")
)
$multiTargetConsumer = Join-Path $consumerRoot "WinFormsMultiTarget\WinFormsMultiTarget.csproj"

$smokeArtifactsRoot = Join-Path $repoRoot "artifacts\package-smoke"
$smokeSessionRoot = Get-VerifiedChildPath `
    -ParentPath $smokeArtifactsRoot `
    -ChildPath "sessions\$([Guid]::NewGuid().ToString('N'))"
$cachePath = Join-Path $smokeSessionRoot "global-packages"
$outputRoot = Join-Path $smokeSessionRoot "bin"
$intermediateRoot = Join-Path $smokeSessionRoot "obj"
$publishRoot = Join-Path $smokeSessionRoot "publish"
New-Item -ItemType Directory -Force -Path $cachePath | Out-Null

function Get-ConsumerBuildProperties {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath
    )

    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)
    $projectOutputRoot = (Join-Path $outputRoot $projectName) + [System.IO.Path]::DirectorySeparatorChar
    $projectIntermediateRoot = (Join-Path $intermediateRoot $projectName) + [System.IO.Path]::DirectorySeparatorChar

    return @(
        "-p:NekoLibPackageVersion=$PackageVersion",
        "-p:BaseOutputPath=$projectOutputRoot",
        "-p:BaseIntermediateOutputPath=$projectIntermediateRoot"
    )
}

$previousFeed = $env:NEKOLIB_LOCAL_FEED
$previousCache = $env:NEKOLIB_PACKAGE_CACHE
$env:NEKOLIB_LOCAL_FEED = $FeedPath
$env:NEKOLIB_PACKAGE_CACHE = $cachePath

try {
    foreach ($project in $consumerProjects) {
        $buildProperties = @(Get-ConsumerBuildProperties -ProjectPath $project)

        Invoke-DotNet -Arguments (@(
            "restore",
            $project,
            "--configfile",
            $nugetConfig,
            "--force-evaluate",
            "--no-http-cache"
        ) + $buildProperties)

        Invoke-DotNet -Arguments (@(
            "build",
            $project,
            "-c",
            $Configuration,
            "--no-restore"
        ) + $buildProperties)

        Invoke-DotNet -Arguments (@(
            "run",
            "--project",
            $project,
            "-c",
            $Configuration,
            "--no-build"
        ) + $buildProperties)
    }

    $winForms9Project = Join-Path $consumerRoot "WinForms9\WinForms9.csproj"
    $winForms9BuildProperties = @(
        Get-ConsumerBuildProperties -ProjectPath $winForms9Project
    )
    $net9HostOutput = Join-Path $outputRoot "WinForms9\$Configuration\net9.0-windows\NekoLib.Watchdog.Host"
    $stalePayloadProbe = Join-Path $net9HostOutput "stale-payload-probe.txt"
    [System.IO.File]::WriteAllText($stalePayloadProbe, "This file must be removed by the next package deployment.")

    Invoke-DotNet -Arguments (@(
        "build",
        $winForms9Project,
        "-c",
        $Configuration,
        "--no-restore"
    ) + $winForms9BuildProperties)

    if (Test-Path -LiteralPath $stalePayloadProbe) {
        throw "The Host package target did not replace a stale sidecar directory: $stalePayloadProbe"
    }

    $multiTargetBuildProperties = @(
        Get-ConsumerBuildProperties -ProjectPath $multiTargetConsumer
    )

    Invoke-DotNet -Arguments (@(
        "restore",
        $multiTargetConsumer,
        "--configfile",
        $nugetConfig,
        "--force-evaluate",
        "--no-http-cache"
    ) + $multiTargetBuildProperties)

    Invoke-DotNet -Arguments (@(
        "build",
        $multiTargetConsumer,
        "-c",
        $Configuration,
        "--no-restore"
    ) + $multiTargetBuildProperties)

    $publishProjects = [ordered]@{
        (Join-Path $consumerRoot "WinForms481\WinForms481.csproj") = (Join-Path $publishRoot "net481")
        (Join-Path $consumerRoot "WinForms9\WinForms9.csproj") = (Join-Path $publishRoot "net9")
    }

    foreach ($publishEntry in $publishProjects.GetEnumerator()) {
        $publishBuildProperties = @(
            Get-ConsumerBuildProperties -ProjectPath $publishEntry.Key
        )

        Invoke-DotNet -Arguments (@(
            "publish",
            $publishEntry.Key,
            "-c",
            $Configuration,
            "--no-build",
            "--no-restore",
            "-o",
            $publishEntry.Value
        ) + $publishBuildProperties)

        $publishedHost = Join-Path $publishEntry.Value "NekoLib.Watchdog.Host\NekoLib.Watchdog.Host.exe"
        if (-not (Test-Path -LiteralPath $publishedHost)) {
            throw "The Host package target did not deploy during publish: $publishedHost"
        }
    }

    $net481HostOutput = Join-Path $outputRoot "WinForms481\$Configuration\net481\NekoLib.Watchdog.Host"
    $multiTargetNet481HostOutput = Join-Path $outputRoot "WinFormsMultiTarget\$Configuration\net481\NekoLib.Watchdog.Host"
    $multiTargetNet9HostOutput = Join-Path $outputRoot "WinFormsMultiTarget\$Configuration\net9.0-windows\NekoLib.Watchdog.Host"

    foreach ($expectedFile in @(
        (Join-Path $net481HostOutput "NekoLib.Watchdog.Host.exe"),
        (Join-Path $net481HostOutput "Newtonsoft.Json.dll"),
        (Join-Path $net9HostOutput "NekoLib.Watchdog.Host.exe"),
        (Join-Path $net9HostOutput "NekoLib.Watchdog.Host.runtimeconfig.json"),
        (Join-Path $multiTargetNet481HostOutput "NekoLib.Watchdog.Host.exe"),
        (Join-Path $multiTargetNet9HostOutput "NekoLib.Watchdog.Host.exe")
    )) {
        if (-not (Test-Path -LiteralPath $expectedFile)) {
            throw "Expected deployed Host artifact is missing: $expectedFile"
        }
    }

    if (Test-Path -LiteralPath (Join-Path $net9HostOutput "Newtonsoft.Json.dll")) {
        throw "The net9 Watchdog Host payload must not contain Newtonsoft.Json.dll."
    }

    $net9PublishOutput = Join-Path $publishRoot "net9"
    Invoke-DotNet -Arguments (@(
        "publish",
        $winForms9Project,
        "-c",
        $Configuration,
        "--no-build",
        "--no-restore",
        "-o",
        $net9PublishOutput,
        "-p:NekoLibWatchdogHostDeploy=false"
    ) + $winForms9BuildProperties)

    if (Test-Path -LiteralPath (Join-Path $net9PublishOutput "NekoLib.Watchdog.Host")) {
        throw "NekoLibWatchdogHostDeploy=false did not remove the sidecar from publish output."
    }

    Invoke-DotNet -Arguments (@(
        "build",
        $winForms9Project,
        "-c",
        $Configuration,
        "--no-restore",
        "-p:NekoLibWatchdogHostDeploy=false"
    ) + $winForms9BuildProperties)

    if (Test-Path -LiteralPath $net9HostOutput) {
        throw "NekoLibWatchdogHostDeploy=false did not remove the sidecar from build output."
    }

    Invoke-DotNet -Arguments (@(
        "build",
        $winForms9Project,
        "-c",
        $Configuration,
        "--no-restore"
    ) + $winForms9BuildProperties)

    if (-not (Test-Path -LiteralPath (Join-Path $net9HostOutput "NekoLib.Watchdog.Host.exe"))) {
        throw "The Host package did not restore the sidecar after deployment was re-enabled."
    }

    Invoke-DotNet -Arguments (@(
        "clean",
        $winForms9Project,
        "-c",
        $Configuration
    ) + $winForms9BuildProperties)

    if (Test-Path -LiteralPath $net9HostOutput) {
        throw "dotnet clean did not remove the Watchdog Host sidecar."
    }
}
finally {
    $env:NEKOLIB_LOCAL_FEED = $previousFeed
    $env:NEKOLIB_PACKAGE_CACHE = $previousCache

    if ($KeepArtifacts) {
        Write-Host "Smoke-test artifacts kept at $smokeSessionRoot"
    }
    elseif (Test-Path -LiteralPath $smokeSessionRoot) {
        Remove-Item -LiteralPath $smokeSessionRoot -Recurse -Force
    }
}

Write-Host ""
Write-Host "Package smoke tests passed for $PackageVersion."
