[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageVersion,
    [Parameter(Mandatory)][string]$SourceCommit,
    [Parameter(Mandatory)][string]$CandidateFeed,
    [Parameter(Mandatory)][string]$EvidenceRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = [IO.Path]::GetFullPath($EvidenceRoot)
if (Test-Path -LiteralPath $root) { throw 'Published-package verification requires a new evidence directory.' }
$feed = Join-Path $root 'feed'
New-Item -ItemType Directory -Path $feed -Force | Out-Null
$candidate = (Resolve-Path -LiteralPath $CandidateFeed).Path
$packages = @(Get-ChildItem -LiteralPath $candidate -Filter "NekoLib.*.$PackageVersion.nupkg" -File | Sort-Object Name)
if ($packages.Count -ne 16) { throw "Expected 16 qualified main packages, found $($packages.Count)." }
$results = [Collections.Generic.List[object]]::new()
$availabilityDeadline = [DateTime]::UtcNow.AddMinutes(10)

function Get-EntryHashes([string]$PackagePath) {
    $archive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
    $entries = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    try {
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName -eq '.signature.p7s') { continue }
            if ($entries.ContainsKey($entry.FullName)) { throw "Duplicate ZIP entry: $($entry.FullName)" }
            $stream = $entry.Open()
            $algorithm = [Security.Cryptography.SHA256]::Create()
            try { $entries.Add($entry.FullName, [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '')) }
            finally { $algorithm.Dispose(); $stream.Dispose() }
        }
        return ,$entries
    }
    finally { $archive.Dispose() }
}

foreach ($package in $packages) {
    $id = $package.Name.Substring(0, $package.Name.Length - ".$PackageVersion.nupkg".Length)
    $lower = $id.ToLowerInvariant()
    $url = "https://api.nuget.org/v3-flatcontainer/$lower/$PackageVersion/$lower.$PackageVersion.nupkg"
    $destination = Join-Path $feed $package.Name
    while ($true) {
        try { Invoke-WebRequest -Uri $url -OutFile $destination -TimeoutSec 30; break }
        catch {
            if ([DateTime]::UtcNow -ge $availabilityDeadline) { throw }
            Write-Host "Waiting for NuGet availability: $id"
            Start-Sleep -Seconds 5
        }
    }
    $localEntries = Get-EntryHashes $package.FullName
    $remoteEntries = Get-EntryHashes $destination
    if ($localEntries.Count -ne $remoteEntries.Count) { throw "Payload entry count mismatch: $id" }
    foreach ($entry in $localEntries.Keys) {
        if (-not $remoteEntries.ContainsKey($entry) -or $remoteEntries[$entry] -ne $localEntries[$entry]) { throw "Published payload differs: $id $entry" }
    }
    $archive = [IO.Compression.ZipFile]::OpenRead($destination)
    try {
        if (-not $archive.GetEntry('.signature.p7s')) { throw "NuGet repository signature is missing: $id" }
        $nuspec = @($archive.Entries | Where-Object { $_.FullName -like '*.nuspec' })
        if ($nuspec.Count -ne 1) { throw "Invalid nuspec count: $id" }
        $reader = [IO.StreamReader]::new($nuspec[0].Open())
        try { [xml]$metadata = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($metadata.package.metadata.id -ne $id -or $metadata.package.metadata.version -ne $PackageVersion -or $metadata.package.metadata.repository.commit -ne $SourceCommit) { throw "Published provenance mismatch: $id" }
    }
    finally { $archive.Dispose() }
    & dotnet nuget verify $destination --all *> (Join-Path $root "$id.signature.txt")
    if ($LASTEXITCODE -ne 0) { throw "NuGet signature verification failed: $id" }
    $results.Add([pscustomobject]@{
        id = $id; version = $PackageVersion; sourceCommit = $SourceCommit; download = $url
        candidateSha256 = (Get-FileHash $package.FullName -Algorithm SHA256).Hash
        publishedSha256 = (Get-FileHash $destination -Algorithm SHA256).Hash
        payloadEntriesCompared = $localEntries.Count; payloadMatched = $true; signatureVerified = $true
    })
    $results | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'verification.json')
    Write-Host "PASS published payload, provenance and signature: $id $PackageVersion"
}
Write-Host "Verified $($results.Count) published main packages. Feed: $feed"
$global:LASTEXITCODE = 0
