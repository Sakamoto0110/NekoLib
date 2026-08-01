[CmdletBinding()]
param(
    [string]$BuildLogPath,
    [switch]$UpdateWarningBaseline
)

$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$repoPrefix = $repoRoot.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$warningBaselinePath = Join-Path $PSScriptRoot "warning-baseline.txt"
$errors = New-Object System.Collections.Generic.List[string]
$notes = New-Object System.Collections.Generic.List[string]

function Add-VerificationError([string]$message) {
    $script:errors.Add($message)
}

function Get-RepoRelativePath([string]$fullPath) {
    $absolute = [IO.Path]::GetFullPath($fullPath)
    if ($absolute.Equals($script:repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
        return ""
    }
    if (-not $absolute.StartsWith($script:repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        return $null
    }
    return $absolute.Substring($script:repoPrefix.Length).Replace('\', '/')
}

function Get-MetadataValue([string]$text, [string]$name) {
    $pattern = "(?m)^\*\*" + [regex]::Escape($name) + ":\*\*\s*(.+?)\s*$"
    $match = [regex]::Match($text, $pattern)
    if ($match.Success) {
        return $match.Groups[1].Value.Trim()
    }
    return $null
}

function Get-MarkdownTargets([string]$text) {
    foreach ($match in [regex]::Matches($text, '\[[^\]]+\]\((?<target>[^)]+)\)')) {
        $target = $match.Groups['target'].Value.Trim()
        if ($target.StartsWith('<') -and $target.EndsWith('>')) {
            $target = $target.Substring(1, $target.Length - 2)
        }
        $target
    }
}

function Resolve-RepositoryTarget([string]$documentPath, [string]$target) {
    if ($target -match '^[a-zA-Z][a-zA-Z0-9+.-]*:' -or $target.StartsWith('#')) {
        return $null
    }

    $pathPart = ($target -split '#', 2)[0]
    if ([string]::IsNullOrWhiteSpace($pathPart)) {
        return $null
    }

    $pathPart = [Uri]::UnescapeDataString($pathPart)
    $documentDirectory = Split-Path -Parent (Join-Path $script:repoRoot $documentPath)
    $fullPath = [IO.Path]::GetFullPath((Join-Path $documentDirectory $pathPart))
    $relative = Get-RepoRelativePath $fullPath
    if ($null -eq $relative) {
        Add-VerificationError "$documentPath links outside the repository: $target"
        return $null
    }
    return [pscustomobject]@{
        FullPath = $fullPath
        RelativePath = $relative.TrimEnd('/')
    }
}

function Test-TrackedPath([string]$relativePath) {
    $matches = @(& git -C $script:repoRoot ls-files -- $relativePath ($relativePath.TrimEnd('/') + '/**'))
    return $matches.Count -gt 0
}

function Test-PathAtCommit([string]$commit, [string]$relativePath) {
    if ($commit -notmatch '^[0-9a-fA-F]{40}$') {
        return $false
    }
    & git -C $script:repoRoot cat-file -e ($commit + ':' + $relativePath.TrimEnd('/')) 2>$null
    return $LASTEXITCODE -eq 0
}

function Normalize-Set([string[]]$values) {
    return @($values | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Sort-Object -Unique)
}

function Compare-StringSets(
    [string]$label,
    [string[]]$expected,
    [string[]]$actual) {
    $expectedSet = Normalize-Set $expected
    $actualSet = Normalize-Set $actual
    $missing = @($expectedSet | Where-Object { $_ -notin $actualSet })
    $extra = @($actualSet | Where-Object { $_ -notin $expectedSet })
    if ($missing.Count -gt 0 -or $extra.Count -gt 0) {
        Add-VerificationError ("$label differs. Missing: [{0}]. Extra: [{1}]." -f
            ($missing -join ', '), ($extra -join ', '))
    }
}

function Normalize-WarningIdentity([string]$line) {
    $plain = [regex]::Replace($line, '\x1B\[[0-9;]*[A-Za-z]', '')
    $separator = ': warning '
    $index = $plain.IndexOf($separator, [StringComparison]::OrdinalIgnoreCase)
    if ($index -lt 0) {
        return $null
    }

    $source = $plain.Substring(0, $index).Trim()
    $remainder = $plain.Substring($index + $separator.Length).Trim()
    if ($remainder -notmatch '^(?<code>[A-Z]{2,}\d+):\s*(?<message>.*)$') {
        return $null
    }

    $code = $matches['code']
    $message = $matches['message'].Trim()
    $project = ""
    if ($message -match '^(?<body>.*) \[(?<project>[^\]]+)\]$') {
        $message = $matches['body'].Trim()
        $project = $matches['project'] -replace '::TargetFramework=.*$', ''
    }

    $source = $source -replace '\(\d+,\d+\)$', ''
    $source = $source.Trim()
    foreach ($candidate in @($source, $project)) {
        if ($candidate -and [IO.Path]::IsPathRooted($candidate)) {
            $relative = Get-RepoRelativePath $candidate
            if ($null -ne $relative) {
                if ($candidate -eq $source) { $source = $relative }
                if ($candidate -eq $project) { $project = $relative }
            }
        }
    }

    $source = $source.Replace('\', '/')
    $project = $project.Replace('\', '/')
    $message = [regex]::Replace($message, '\s+', ' ')
    $owner = if ($source) { $source } else { $project }
    return "$owner|$code|$message"
}

function Get-WarningIdentities([string]$logPath) {
    $resolvedLog = [IO.Path]::GetFullPath($logPath)
    if (-not (Test-Path -LiteralPath $resolvedLog)) {
        throw "Build log does not exist: $resolvedLog"
    }
    return @(Get-Content -LiteralPath $resolvedLog |
        ForEach-Object { Normalize-WarningIdentity $_ } |
        Where-Object { $_ } |
        Sort-Object -Unique)
}

Push-Location $repoRoot
try {
    $markdownFiles = @(& git ls-files -- '*.md' | Sort-Object)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to enumerate tracked Markdown files."
    }

    $registryFiles = @('docs/README.md', 'docs/audit/README.md', 'docs/history/README.md')
    $registered = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($registryFile in $registryFiles) {
        $registryText = Get-Content -LiteralPath $registryFile -Raw
        foreach ($target in Get-MarkdownTargets $registryText) {
            $resolved = Resolve-RepositoryTarget $registryFile $target
            if ($null -ne $resolved -and $resolved.RelativePath.EndsWith('.md')) {
                [void]$registered.Add($resolved.RelativePath)
            }
        }
    }

    foreach ($markdownFile in $markdownFiles) {
        $text = Get-Content -LiteralPath $markdownFile -Raw
        $kind = Get-MetadataValue $text 'Kind'
        $lifecycle = Get-MetadataValue $text 'Lifecycle'
        $subject = Get-MetadataValue $text 'Subject'

        $hasEmbeddedClassification = $kind -and $lifecycle -and $subject
        if (-not $hasEmbeddedClassification -and -not $registered.Contains($markdownFile)) {
            Add-VerificationError "$markdownFile has no embedded classification and is absent from the documentation indexes."
        }

        if ($kind -and $kind -notin @('reference', 'guide', 'roadmap/status', 'audit')) {
            Add-VerificationError "$markdownFile has unsupported Kind '$kind'."
        }
        if ($lifecycle -and $lifecycle -notin @('current', 'frozen', 'historical')) {
            Add-VerificationError "$markdownFile has unsupported Lifecycle '$lifecycle'."
        }

        if ($markdownFile.StartsWith('docs/audit/') -and $markdownFile -ne 'docs/audit/README.md') {
            foreach ($field in @(
                'Kind', 'Lifecycle', 'Subject', 'Reference date',
                'Reference commit', 'Last reconciliation', 'Current state')) {
                if (-not (Get-MetadataValue $text $field)) {
                    Add-VerificationError "$markdownFile is missing audit metadata '$field'."
                }
            }
        }

        $referenceCommit = (Get-MetadataValue $text 'Reference commit') -replace '`', ''
        if ($referenceCommit) { $referenceCommit = $referenceCommit.Trim() }
        $lineNumber = 0
        foreach ($line in Get-Content -LiteralPath $markdownFile) {
            $lineNumber++
            foreach ($target in Get-MarkdownTargets $line) {
                $resolved = Resolve-RepositoryTarget $markdownFile $target
                if ($null -eq $resolved) { continue }

                if (Test-Path -LiteralPath $resolved.FullPath) {
                    if (-not (Test-TrackedPath $resolved.RelativePath)) {
                        & git -C $repoRoot check-ignore -q -- $resolved.RelativePath
                        $reason = if ($LASTEXITCODE -eq 0) { 'ignored' } else { 'untracked' }
                        Add-VerificationError "$markdownFile`:$lineNumber links to $reason path '$($resolved.RelativePath)'."
                    }
                    continue
                }

                if ($kind -eq 'audit' -and (Test-PathAtCommit $referenceCommit $resolved.RelativePath)) {
                    continue
                }

                Add-VerificationError "$markdownFile`:$lineNumber links to absent path '$($resolved.RelativePath)'."
            }
        }
    }

    $readme = Get-Content -LiteralPath 'README.md' -Raw
    $moduleRows = @{}
    foreach ($line in Get-Content -LiteralPath 'README.md') {
        if ($line -match '^\| `(?<id>NekoLib[^`]*)` \| `(?<path>src/[^`]+/)` \| (?<targets>[^|]+) \| (?<refs>[^|]+) \|$') {
            $moduleRows[$matches['id']] = [pscustomobject]@{
                Path = $matches['path']
                Targets = $matches['targets']
                References = $matches['refs']
            }
        }
    }

    $sourceProjects = @(Get-ChildItem -LiteralPath 'src' -Recurse -Filter '*.csproj' |
        Where-Object { $_.FullName -notlike '*\src\Tools\*' } |
        Sort-Object FullName)

    foreach ($project in $sourceProjects) {
        [xml]$xml = Get-Content -LiteralPath $project.FullName
        $id = @($xml.Project.PropertyGroup.PackageId | Where-Object { $_ } | Select-Object -First 1)
        if (-not $id) { $id = $project.BaseName }
        $id = [string]$id
        if (-not $moduleRows.ContainsKey($id)) {
            Add-VerificationError "README module map is missing $id."
            continue
        }

        $row = $moduleRows[$id]
        $actualPath = (Get-RepoRelativePath $project.Directory.FullName).TrimEnd('/') + '/'
        if ($row.Path -ne $actualPath) {
            Add-VerificationError "README path for $id is '$($row.Path)', expected '$actualPath'."
        }

        $targetNodes = @($xml.Project.PropertyGroup.TargetFrameworks | Where-Object { $_ })
        if ($targetNodes.Count -eq 0) {
            $targetNodes = @($xml.Project.PropertyGroup.TargetFramework | Where-Object { $_ })
        }
        $actualTargetText = [string]$targetNodes[0]
        $actualTargets = @($actualTargetText -split ';')
        $documentedTargets = @(($row.Targets -replace '`', '') -split ',' | ForEach-Object { $_.Trim() })
        Compare-StringSets "README targets for $id" $actualTargets $documentedTargets

        $actualReferences = @($xml.Project.ItemGroup.ProjectReference.Include |
            Where-Object { $_ } |
            ForEach-Object {
                $referenceProject = [IO.Path]::GetFullPath((Join-Path $project.Directory.FullName $_))
                ([IO.Path]::GetFileNameWithoutExtension($referenceProject)) -replace '^NekoLib\.', ''
            })
        $referenceText = ($row.References -replace '`', '').Trim()
        $documentedReferences = @()
        if ($referenceText -match '[A-Za-z0-9.]') {
            $documentedReferences = @($referenceText -split ',' | ForEach-Object { $_.Trim() })
        }
        Compare-StringSets "README references for $id" $actualReferences $documentedReferences
    }

    Compare-StringSets 'README module-map projects' @($sourceProjects.BaseName) @($moduleRows.Keys)

    $solutionProjects = @(& dotnet sln NekoLib.sln list |
        Where-Object { $_ -match '\.csproj$' } |
        ForEach-Object { $_.Trim().Replace('\', '/') })
    if ($LASTEXITCODE -ne 0) {
        Add-VerificationError 'dotnet sln NekoLib.sln list failed.'
    } else {
        $expectedSourceMembership = @($sourceProjects |
            Where-Object { $_.FullName -notlike '*\src\Hosting\NekoLib\*' } |
            ForEach-Object { Get-RepoRelativePath $_.FullName })
        $actualSourceMembership = @($solutionProjects | Where-Object { $_.StartsWith('src/') })
        Compare-StringSets 'Source project solution membership' $expectedSourceMembership $actualSourceMembership
    }

    $packText = Get-Content -LiteralPath 'eng/pack-local.ps1' -Raw
    $packageBlock = [regex]::Match($packText, '(?s)\$packageIds\s*=\s*@\((?<body>.*?)\)')
    if (-not $packageBlock.Success) {
        Add-VerificationError 'Unable to parse package IDs from eng/pack-local.ps1.'
    } else {
        $packageIds = @([regex]::Matches($packageBlock.Groups['body'].Value, '"(?<id>NekoLib[^"]+)"') |
            ForEach-Object { $_.Groups['id'].Value })
        if ('NekoLib.Watchdog.Host' -notin $packageIds) {
            Add-VerificationError 'Packaging list does not contain NekoLib.Watchdog.Host.'
        }
        if ($readme -notmatch 'Package production is opt-in: the (?<count>\d+) library projects') {
            Add-VerificationError 'Unable to parse the README library-package count.'
        } else {
            $documentedLibraryCount = [int]$matches['count']
            $actualLibraryCount = @($packageIds | Where-Object { $_ -ne 'NekoLib.Watchdog.Host' }).Count
            if ($documentedLibraryCount -ne $actualLibraryCount) {
                Add-VerificationError "README says $documentedLibraryCount library packages; packaging lists $actualLibraryCount."
            }
        }
    }

    if ($UpdateWarningBaseline -and -not $BuildLogPath) {
        Add-VerificationError '-UpdateWarningBaseline requires -BuildLogPath.'
    }

    if ($BuildLogPath) {
        $currentWarnings = Get-WarningIdentities $BuildLogPath
        if ($UpdateWarningBaseline) {
            $commit = (& git -C $repoRoot rev-parse HEAD).Trim()
            $treeState = if (& git -C $repoRoot status --porcelain=v1 --untracked-files=no) { 'dirty' } else { 'clean' }
            $header = @(
                '# Normalized warning identities: source path | warning code | message',
                "# Generated on 2026-08-01 from commit $commit ($treeState tree)."
            )
            @($header + $currentWarnings) | Set-Content -LiteralPath $warningBaselinePath -Encoding UTF8
            $notes.Add("Updated warning baseline with $($currentWarnings.Count) identities.")
        } elseif (-not (Test-Path -LiteralPath $warningBaselinePath)) {
            Add-VerificationError 'eng/warning-baseline.txt is missing.'
        } else {
            $baselineWarnings = @(Get-Content -LiteralPath $warningBaselinePath |
                Where-Object { $_ -and -not $_.StartsWith('#') } |
                Sort-Object -Unique)
            $newWarnings = @($currentWarnings | Where-Object { $_ -notin $baselineWarnings })
            $resolvedWarnings = @($baselineWarnings | Where-Object { $_ -notin $currentWarnings })
            foreach ($warning in $newWarnings) {
                Add-VerificationError "New warning identity: $warning"
            }
            if ($resolvedWarnings.Count -gt 0) {
                $notes.Add("$($resolvedWarnings.Count) baseline warning identities were not emitted by this build.")
            }
        }
    } elseif (-not (Test-Path -LiteralPath $warningBaselinePath)) {
        Add-VerificationError 'eng/warning-baseline.txt is missing; supply a rebuild log with -UpdateWarningBaseline.'
    } else {
        $notes.Add('Warning baseline exists; no build log was supplied for comparison.')
    }
}
finally {
    Pop-Location
}

foreach ($note in $notes) {
    Write-Host "NOTE: $note"
}

if ($errors.Count -gt 0) {
    foreach ($errorMessage in $errors) {
        Write-Error $errorMessage -ErrorAction Continue
    }
    throw "Documentation verification failed with $($errors.Count) error(s)."
}

Write-Host "Documentation verification passed."
