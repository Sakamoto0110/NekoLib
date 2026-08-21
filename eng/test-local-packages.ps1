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

function Invoke-DotNetExpectFailure {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$ExpectedText
    )

    Write-Host "dotnet $($Arguments -join ' ') (expected failure)"
    $output = (& dotnet @Arguments 2>&1 | Out-String)
    $exitCode = $LASTEXITCODE
    Write-Host $output

    if ($exitCode -eq 0) {
        throw "dotnet unexpectedly succeeded."
    }

    if ($output.IndexOf($ExpectedText, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Expected failure did not contain '$ExpectedText'."
    }
}

function Invoke-Program {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$WorkingDirectory,

        [Parameter()]
        [string[]]$Arguments = @()
    )

    Write-Host "$Path $($Arguments -join ' ')"
    Push-Location $WorkingDirectory
    try {
        & $Path @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Program exited with code $LASTEXITCODE`: $Path"
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-WatchdogProtocolScenario {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$WorkingDirectory
    )

    $markerPath = Join-Path $WorkingDirectory "watchdog-host-ready.marker"
    $stdoutPath = Join-Path $WorkingDirectory "watchdog-host-startup.stdout.log"
    $stderrPath = Join-Path $WorkingDirectory "watchdog-host-startup.stderr.log"
    foreach ($pathToRemove in @($markerPath, $stdoutPath, $stderrPath)) {
        if (Test-Path -LiteralPath $pathToRemove) {
            Remove-Item -LiteralPath $pathToRemove -Force
        }
    }

    Write-Host "$Path startup"
    $supervisedProcess = Start-Process `
        -FilePath $Path `
        -ArgumentList @("startup") `
        -WorkingDirectory $WorkingDirectory `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -PassThru

    try {
        $elapsed = [Diagnostics.Stopwatch]::StartNew()
        while ($elapsed.Elapsed -lt [TimeSpan]::FromSeconds(20) -and
            -not (Test-Path -LiteralPath $markerPath)) {
            $supervisedProcess.Refresh()
            if ($supervisedProcess.HasExited) {
                break
            }
            Start-Sleep -Milliseconds 50
        }

        if (-not (Test-Path -LiteralPath $markerPath)) {
            $stdout = if (Test-Path -LiteralPath $stdoutPath) {
                Get-Content -LiteralPath $stdoutPath -Raw
            }
            else { "<missing>" }
            $stderr = if (Test-Path -LiteralPath $stderrPath) {
                Get-Content -LiteralPath $stderrPath -Raw
            }
            else { "<missing>" }
            throw "The packaged Host did not report startup readiness. stdout=$stdout stderr=$stderr"
        }

        Invoke-Program `
            -Path $Path `
            -WorkingDirectory $WorkingDirectory `
            -Arguments @("stop")

        if (-not $supervisedProcess.WaitForExit(15000)) {
            throw "The supervised package consumer did not exit after Host stop."
        }
    }
    finally {
        try {
            $supervisedProcess.Refresh()
            if (-not $supervisedProcess.HasExited) {
                Stop-Process -Id $supervisedProcess.Id -Force
            }
        }
        catch {
        }
        $supervisedProcess.Dispose()
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

function Get-PackageEntryBytes {
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
            return $memory.ToArray()
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

function Get-PeInfo {
    param(
        [Parameter(Mandatory)]
        [byte[]]$Bytes
    )

    $peHeaderOffset = [BitConverter]::ToInt32($Bytes, 0x3c)
    $machine = [BitConverter]::ToUInt16($Bytes, $peHeaderOffset + 4)
    $sectionCount = [BitConverter]::ToUInt16($Bytes, $peHeaderOffset + 6)
    $optionalHeaderSize = [BitConverter]::ToUInt16($Bytes, $peHeaderOffset + 20)
    $optionalHeaderOffset = $peHeaderOffset + 24
    $optionalMagic = [BitConverter]::ToUInt16($Bytes, $optionalHeaderOffset)
    $dataDirectoryOffset = switch ($optionalMagic) {
        0x010b { $optionalHeaderOffset + 96 }
        0x020b { $optionalHeaderOffset + 112 }
        default { throw "Unsupported PE optional-header magic 0x$($optionalMagic.ToString('x4'))." }
    }

    $clrRva = [BitConverter]::ToUInt32($Bytes, $dataDirectoryOffset + (14 * 8))
    $corFlags = $null
    if ($clrRva -ne 0) {
        $sectionOffset = $optionalHeaderOffset + $optionalHeaderSize
        for ($index = 0; $index -lt $sectionCount; $index++) {
            $currentSection = $sectionOffset + ($index * 40)
            $virtualSize = [BitConverter]::ToUInt32($Bytes, $currentSection + 8)
            $virtualAddress = [BitConverter]::ToUInt32($Bytes, $currentSection + 12)
            $rawSize = [BitConverter]::ToUInt32($Bytes, $currentSection + 16)
            $rawPointer = [BitConverter]::ToUInt32($Bytes, $currentSection + 20)
            $sectionSpan = [Math]::Max($virtualSize, $rawSize)

            if ($clrRva -ge $virtualAddress -and
                $clrRva -lt ($virtualAddress + $sectionSpan)) {
                $clrOffset = $rawPointer + ($clrRva - $virtualAddress)
                $corFlags = [BitConverter]::ToUInt32($Bytes, $clrOffset + 16)
                break
            }
        }

        if ($null -eq $corFlags) {
            throw "Unable to resolve the CLR header in the PE image."
        }
    }

    return [pscustomobject]@{
        Machine = $machine
        CorFlags = $corFlags
    }
}

function Get-PackagePeInfo {
    param(
        [Parameter(Mandatory)]
        [string]$PackagePath,

        [Parameter(Mandatory)]
        [string]$EntryName
    )

    return Get-PeInfo -Bytes (Get-PackageEntryBytes `
        -PackagePath $PackagePath `
        -EntryName $EntryName)
}

function Get-FilePeInfo {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    return Get-PeInfo -Bytes ([System.IO.File]::ReadAllBytes($Path))
}

function Get-BytesSha256 {
    param(
        [Parameter(Mandatory)]
        [byte[]]$Bytes
    )

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($sha256.ComputeHash($Bytes)).Replace("-", "")
    }
    finally {
        $sha256.Dispose()
    }
}

function Assert-FileMatchesPackageEntry {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$PackagePath,

        [Parameter(Mandatory)]
        [string]$EntryName
    )

    $fileHash = Get-BytesSha256 -Bytes ([System.IO.File]::ReadAllBytes($Path))
    $packageHash = Get-BytesSha256 -Bytes (Get-PackageEntryBytes `
        -PackagePath $PackagePath `
        -EntryName $EntryName)

    if (-not [string]::Equals($fileHash, $packageHash, [StringComparison]::Ordinal)) {
        throw "Deployed Host bytes do not match package entry $EntryName`: $Path"
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
    "NekoLib.Inspection" = "net9.0"
    "NekoLib.Devices" = "net9.0"
    "NekoLib.Diagnostics" = "net9.0"
    "NekoLib.Diagnostics.Windows" = "net9.0-windows7.0"
    "NekoLib.Http" = "net9.0"
    "NekoLib.Logging" = "net9.0"
    "NekoLib.Mvvm" = "net9.0"
    "NekoLib.Navigation" = "net9.0"
    "NekoLib.Navigation.WinForms" = "net9.0-windows7.0"
    "NekoLib.Navigation.Wpf" = "net9.0-windows7.0"
    "NekoLib.Pipes" = "net9.0"
    "NekoLib.Telemetry" = "net9.0"
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
    "tools/net481/NekoLib.Watchdog.Host.exe",
    "tools/net9.0-windows7.0/win-x86/NekoLib.Watchdog.Host.exe",
    "tools/net9.0-windows7.0/win-x64/NekoLib.Watchdog.Host.exe"
)

foreach ($requiredEntry in $requiredHostEntries) {
    if ($hostEntries -notcontains $requiredEntry) {
        throw "NekoLib.Watchdog.Host is missing $requiredEntry."
    }
}

$forbiddenHostEntries = @(
    "buildTransitive/NekoLib.Watchdog.Host.targets"
)
foreach ($forbiddenEntry in $forbiddenHostEntries) {
    if ($hostEntries -contains $forbiddenEntry) {
        throw "NekoLib.Watchdog.Host must not contain $forbiddenEntry."
    }
}

if (@($hostEntries | Where-Object { $_.StartsWith("lib/", [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0) {
    throw "NekoLib.Watchdog.Host must not expose a library under lib/."
}

$net481Pe = Get-PackagePeInfo `
    -PackagePath $hostPackagePath `
    -EntryName "tools/net481/NekoLib.Watchdog.Host.exe"
$x86Pe = Get-PackagePeInfo `
    -PackagePath $hostPackagePath `
    -EntryName "tools/net9.0-windows7.0/win-x86/NekoLib.Watchdog.Host.exe"
$x64Pe = Get-PackagePeInfo `
    -PackagePath $hostPackagePath `
    -EntryName "tools/net9.0-windows7.0/win-x64/NekoLib.Watchdog.Host.exe"

if ($net481Pe.Machine -ne 0x014c -or
    $null -eq $net481Pe.CorFlags -or
    ($net481Pe.CorFlags -band 0x00000001) -eq 0 -or
    ($net481Pe.CorFlags -band 0x00000002) -ne 0 -or
    ($net481Pe.CorFlags -band 0x00020000) -ne 0) {
    throw "The net481 Watchdog Host payload is not managed AnyCPU IL."
}

if ($x86Pe.Machine -ne 0x014c) {
    throw "The win-x86 Watchdog Host apphost has unexpected PE machine 0x$($x86Pe.Machine.ToString('x4'))."
}

if ($x64Pe.Machine -ne 0x8664) {
    throw "The win-x64 Watchdog Host apphost has unexpected PE machine 0x$($x64Pe.Machine.ToString('x4'))."
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
$protocolConsumer = Join-Path $consumerRoot "WatchdogHostProtocol\WatchdogHostProtocol.csproj"
$wrapperProject = Join-Path $consumerRoot "WatchdogHostWrapper\WatchdogHostWrapper.csproj"
$transitiveConsumer = Join-Path $consumerRoot "WatchdogHostTransitive\WatchdogHostTransitive.csproj"

$smokeArtifactsRoot = Join-Path $repoRoot "artifacts\package-smoke"
$smokeSessionRoot = Get-VerifiedChildPath `
    -ParentPath $smokeArtifactsRoot `
    -ChildPath "sessions\$([Guid]::NewGuid().ToString('N'))"
$cachePath = Join-Path $smokeSessionRoot "global-packages"
$outputRoot = Join-Path $smokeSessionRoot "bin"
$intermediateRoot = Join-Path $smokeSessionRoot "obj"
$publishRoot = Join-Path $smokeSessionRoot "publish"
$combinedFeed = Join-Path $smokeSessionRoot "feed"
New-Item -ItemType Directory -Force -Path $cachePath | Out-Null
New-Item -ItemType Directory -Force -Path $combinedFeed | Out-Null

Get-ChildItem -LiteralPath $FeedPath -Filter "*.nupkg" -File |
    ForEach-Object {
        [System.IO.File]::Copy(
            $_.FullName,
            (Join-Path $combinedFeed $_.Name),
            $false)
    }

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
$env:NEKOLIB_LOCAL_FEED = $combinedFeed
$env:NEKOLIB_PACKAGE_CACHE = $cachePath

try {
    $wrapperBuildProperties = @(
        Get-ConsumerBuildProperties -ProjectPath $wrapperProject
    )
    Invoke-DotNet -Arguments (@(
        "restore",
        $wrapperProject,
        "--configfile",
        $nugetConfig,
        "--force-evaluate",
        "--no-http-cache"
    ) + $wrapperBuildProperties)
    Invoke-DotNet -Arguments (@(
        "pack",
        $wrapperProject,
        "-c",
        $Configuration,
        "--no-restore",
        "-o",
        $combinedFeed
    ) + $wrapperBuildProperties)

    $transitiveBuildProperties = @(
        Get-ConsumerBuildProperties -ProjectPath $transitiveConsumer
    )
    Invoke-DotNet -Arguments (@(
        "restore",
        $transitiveConsumer,
        "--configfile",
        $nugetConfig,
        "--force-evaluate",
        "--no-http-cache"
    ) + $transitiveBuildProperties)
    Invoke-DotNet -Arguments (@(
        "build",
        $transitiveConsumer,
        "-c",
        $Configuration,
        "--no-restore"
    ) + $transitiveBuildProperties)

    $transitiveHostOutput = Join-Path `
        $outputRoot `
        "WatchdogHostTransitive\$Configuration\net9.0-windows\NekoLib.Watchdog.Host"
    if (Test-Path -LiteralPath $transitiveHostOutput) {
        throw "The Watchdog Host sidecar propagated through a transitive package reference."
    }

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

    $deployedHost = Join-Path $net9HostOutput "NekoLib.Watchdog.Host.exe"
    $defaultHostPe = Get-FilePeInfo -Path $deployedHost
    if ($defaultHostPe.Machine -ne 0x8664) {
        throw "The default Watchdog Host deployment did not select win-x64."
    }
    Assert-FileMatchesPackageEntry `
        -Path $deployedHost `
        -PackagePath $hostPackagePath `
        -EntryName "tools/net9.0-windows7.0/win-x64/NekoLib.Watchdog.Host.exe"

    Invoke-DotNet -Arguments (@(
        "build",
        $winForms9Project,
        "-c",
        $Configuration,
        "--no-restore",
        "-p:NekoLibWatchdogHostRid=win-x86"
    ) + $winForms9BuildProperties)

    $selectedX86HostPe = Get-FilePeInfo -Path $deployedHost
    if ($selectedX86HostPe.Machine -ne 0x014c) {
        throw "NekoLibWatchdogHostRid=win-x86 did not deploy the x86 Host apphost."
    }
    Assert-FileMatchesPackageEntry `
        -Path $deployedHost `
        -PackagePath $hostPackagePath `
        -EntryName "tools/net9.0-windows7.0/win-x86/NekoLib.Watchdog.Host.exe"

    Invoke-DotNetExpectFailure -Arguments (@(
        "build",
        $winForms9Project,
        "-c",
        $Configuration,
        "--no-restore",
        "-p:NekoLibWatchdogHostRid=win-arm64"
    ) + $winForms9BuildProperties) -ExpectedText "contains win-x86 and win-x64 payloads only"

    Invoke-DotNet -Arguments (@(
        "build",
        $winForms9Project,
        "-c",
        $Configuration,
        "--no-restore"
    ) + $winForms9BuildProperties)

    $restoredDefaultHostPe = Get-FilePeInfo -Path $deployedHost
    if ($restoredDefaultHostPe.Machine -ne 0x8664) {
        throw "The default Watchdog Host deployment was not restored after RID probes."
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

    $deployedNet481Pe = Get-FilePeInfo -Path (
        Join-Path $net481HostOutput "NekoLib.Watchdog.Host.exe")
    if ($deployedNet481Pe.Machine -ne 0x014c -or
        $null -eq $deployedNet481Pe.CorFlags -or
        ($deployedNet481Pe.CorFlags -band 0x00000001) -eq 0 -or
        ($deployedNet481Pe.CorFlags -band 0x00000002) -ne 0 -or
        ($deployedNet481Pe.CorFlags -band 0x00020000) -ne 0) {
        throw "The deployed net481 Watchdog Host is not managed AnyCPU IL."
    }
    Assert-FileMatchesPackageEntry `
        -Path (Join-Path $net481HostOutput "NekoLib.Watchdog.Host.exe") `
        -PackagePath $hostPackagePath `
        -EntryName "tools/net481/NekoLib.Watchdog.Host.exe"

    $protocolBuildProperties = @(
        Get-ConsumerBuildProperties -ProjectPath $protocolConsumer
    )
    Invoke-DotNet -Arguments (@(
        "restore",
        $protocolConsumer,
        "--configfile",
        $nugetConfig,
        "--force-evaluate",
        "--no-http-cache"
    ) + $protocolBuildProperties)
    Invoke-DotNet -Arguments (@(
        "build",
        $protocolConsumer,
        "-c",
        $Configuration,
        "--no-restore",
        "-p:NekoLibWatchdogHostDeploy=false"
    ) + $protocolBuildProperties)

    foreach ($protocolTfm in @("net481", "net9.0-windows")) {
        $protocolOutput = Join-Path `
            $outputRoot `
            "WatchdogHostProtocol\$Configuration\$protocolTfm"
        $protocolExecutable = Join-Path $protocolOutput "WatchdogHostProtocol.exe"
        $protocolWorkingDirectory = Join-Path `
            $smokeSessionRoot `
            "protocol-runs\$protocolTfm"
        New-Item -ItemType Directory -Force -Path $protocolWorkingDirectory | Out-Null

        Invoke-Program `
            -Path $protocolExecutable `
            -WorkingDirectory $protocolWorkingDirectory `
            -Arguments @("mismatch")
    }

    Invoke-DotNet -Arguments (@(
        "build",
        $protocolConsumer,
        "-c",
        $Configuration,
        "--no-restore"
    ) + $protocolBuildProperties)

    foreach ($protocolTfm in @("net481", "net9.0-windows")) {
        $protocolOutput = Join-Path `
            $outputRoot `
            "WatchdogHostProtocol\$Configuration\$protocolTfm"
        $protocolExecutable = Join-Path $protocolOutput "WatchdogHostProtocol.exe"
        $protocolWorkingDirectory = Join-Path `
            $smokeSessionRoot `
            "protocol-runs\$protocolTfm"

        Invoke-WatchdogProtocolScenario `
            -Path $protocolExecutable `
            -WorkingDirectory $protocolWorkingDirectory
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
