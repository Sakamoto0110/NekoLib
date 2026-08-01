[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repoRoot "src\Tools\BundlerTool\BundlerTool.csproj"
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts"))
$outputPath = [IO.Path]::GetFullPath(
    (Join-Path $artifactsRoot ("tools\BundlerTool\" + $Configuration)))

$artifactsPrefix = $artifactsRoot.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar

if (-not $outputPath.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "BundlerTool output escaped the repository artifacts directory: $outputPath"
}

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

& dotnet publish $projectPath `
    -c $Configuration `
    -f net481 `
    -o $outputPath `
    --nologo `
    /p:ContinuousIntegrationBuild=true

if ($LASTEXITCODE -ne 0) {
    throw "BundlerTool publish failed with exit code $LASTEXITCODE."
}

$executablePath = Join-Path $outputPath "BundlerTool.exe"
if (-not (Test-Path -LiteralPath $executablePath)) {
    throw "BundlerTool publish did not produce $executablePath."
}

$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Unable to resolve the source commit."
}

$treeState = if (& git -C $repoRoot status --porcelain=v1 --untracked-files=no) {
    "dirty"
} else {
    "clean"
}

$manifest = [ordered]@{
    tool = "BundlerTool"
    version = [Reflection.AssemblyName]::GetAssemblyName($executablePath).Version.ToString()
    configuration = $Configuration
    targetFramework = "net481"
    sourceCommit = $sourceCommit
    sourceTreeState = $treeState
    projectPath = "src/Tools/BundlerTool/BundlerTool.csproj"
    projectSha256 = (Get-FileHash -LiteralPath $projectPath -Algorithm SHA256).Hash
    executableSha256 = (Get-FileHash -LiteralPath $executablePath -Algorithm SHA256).Hash
}

$manifestPath = Join-Path $outputPath "build-manifest.json"
$manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host "BundlerTool published to $outputPath"
Write-Host "Manifest: $manifestPath"
