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
$utf8Encoding = New-Object System.Text.UTF8Encoding -ArgumentList $false, $true

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

function Read-Utf8Text([string]$path) {
    $absolute = [IO.Path]::GetFullPath($path)
    return [IO.File]::ReadAllText($absolute, $script:utf8Encoding)
}

function Read-Utf8Lines([string]$path) {
    $absolute = [IO.Path]::GetFullPath($path)
    return @([IO.File]::ReadAllLines($absolute, $script:utf8Encoding))
}

function Get-MetadataMatch([string]$text, [string]$name) {
    $pattern = "(?m)^\*\*" + [regex]::Escape($name) + ":\*\*[ \t]*(?<value>[^\r\n]*?)[ \t]*\r?$"
    return [regex]::Match($text, $pattern)
}

function Get-MetadataValue([string]$text, [string]$name) {
    $match = Get-MetadataMatch $text $name
    if ($match.Success) {
        return $match.Groups['value'].Value.Trim()
    }
    return $null
}

function Test-MetadataValueIsSingleLine([string]$text, [string]$name) {
    $match = Get-MetadataMatch $text $name
    if (-not $match.Success) { return $true }

    $lineEnd = $text.IndexOf("`n", $match.Index + $match.Length)
    if ($lineEnd -lt 0) { return $true }
    $nextStart = $lineEnd + 1
    $nextEnd = $text.IndexOf("`n", $nextStart)
    if ($nextEnd -lt 0) { $nextEnd = $text.Length }
    $nextLine = $text.Substring($nextStart, $nextEnd - $nextStart).TrimEnd("`r")
    return [string]::IsNullOrWhiteSpace($nextLine) -or
        $nextLine -match '^\*\*[^*\r\n]+:\*\*'
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

function Test-VersionedCandidatePath([string]$relativePath) {
    $normalized = $relativePath.Replace('\', '/').TrimEnd('/')
    if ($script:versionedPathSet.Contains($normalized)) {
        return $true
    }

    $prefix = $normalized + '/'
    foreach ($candidate in $script:versionedPaths) {
        if ($candidate.StartsWith($prefix, [StringComparison]::Ordinal)) {
            return $true
        }
    }
    return $false
}

function Test-RepositoryPathCase([string]$relativePath) {
    $normalized = $relativePath.Replace('\', '/').Trim('/')
    if (-not $normalized) { return $true }

    $current = $script:repoRoot
    foreach ($segment in $normalized.Split('/')) {
        $match = @(Get-ChildItem -LiteralPath $current -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ceq $segment })
        if ($match.Count -ne 1) {
            return $false
        }
        $current = $match[0].FullName
    }
    return $true
}

function Split-MetadataList([string]$value) {
    if (-not $value) { return @() }
    $plain = ($value -replace '`', '').Trim()
    if (-not $plain -or $plain -eq 'none') { return @() }
    return @($plain -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

function Get-MarkdownAnchorSet([string]$fullPath) {
    $anchors = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    $counts = @{}
    $insideFence = $false
    foreach ($line in Read-Utf8Lines $fullPath) {
        if ($line -match '^\s*(`{3,}|~{3,})') {
            $insideFence = -not $insideFence
            continue
        }
        if ($insideFence -or $line -notmatch '^#{1,6}\s+(?<heading>.+?)\s*#*\s*$') {
            continue
        }

        $heading = $matches['heading']
        $heading = [regex]::Replace($heading, '<[^>]+>', '')
        $heading = $heading -replace '[`*_~]', ''
        $anchor = $heading.ToLowerInvariant()
        $anchor = [regex]::Replace($anchor, '[^\p{L}\p{Nd}\-_ ]', '')
        $anchor = [regex]::Replace($anchor.Trim(), '\s+', '-')
        if (-not $anchor) { continue }

        if ($counts.ContainsKey($anchor)) {
            $counts[$anchor]++
            $anchor = "$anchor-$($counts[$anchor])"
        } else {
            $counts[$anchor] = 0
        }
        [void]$anchors.Add($anchor)
    }
    return $anchors
}

function Test-MarkdownAnchor([string]$fullPath, [string]$anchor) {
    if (-not $anchor -or $anchor -match '^L\d+(-L\d+)?$') {
        return $true
    }
    $decoded = [Uri]::UnescapeDataString($anchor)
    $anchors = Get-MarkdownAnchorSet $fullPath
    return $anchors.Contains($decoded)
}

function Get-HeadingBlocks([string]$text) {
    $headings = New-Object System.Collections.Generic.List[object]
    $insideFence = $false
    foreach ($lineMatch in [regex]::Matches($text, '(?m)^.*(?:\r?\n|$)')) {
        if ($lineMatch.Length -eq 0) { continue }
        $line = $lineMatch.Value -replace '\r?\n$', ''
        if ($line -match '^\s*(`{3,}|~{3,})') {
            $insideFence = -not $insideFence
            continue
        }
        if (-not $insideFence -and $line -match '^##\s+(?<heading>.+?)\s*$') {
            $headings.Add([pscustomobject]@{
                Index = $lineMatch.Index
                Heading = $matches['heading'].Trim()
            })
        }
    }

    for ($index = 0; $index -lt $headings.Count; $index++) {
        $start = $headings[$index].Index
        $end = if ($index + 1 -lt $headings.Count) { $headings[$index + 1].Index } else { $text.Length }
        [pscustomobject]@{
            Heading = $headings[$index].Heading
            Text = $text.Substring($start, $end - $start)
        }
    }
}

function Test-DocumentationParserPrimitives {
    $headingSample = @'
## REAL-001

body

```text
## FAKE-001
```

~~~text
## FAKE-002
~~~
'@
    $blocks = @(Get-HeadingBlocks $headingSample)
    if ($blocks.Count -ne 1 -or $blocks[0].Heading -ne 'REAL-001') {
        Add-VerificationError 'Heading parser self-test failed to ignore fenced Markdown.'
    }

    $singleLineMetadata = "**Subject:** one line`n`nbody"
    $wrappedMetadata = "**Subject:** first line`ncontinuation`n"
    if (-not (Test-MetadataValueIsSingleLine $singleLineMetadata 'Subject') -or
        (Test-MetadataValueIsSingleLine $wrappedMetadata 'Subject')) {
        Add-VerificationError 'Metadata parser self-test failed to enforce single-line values.'
    }
}

function Test-PathAtCommit([string]$commit, [string]$relativePath) {
    if ($commit -notmatch '^[0-9a-fA-F]{40}$') {
        return $false
    }
    & git -C $script:repoRoot cat-file -e ($commit + ':' + $relativePath.TrimEnd('/')) 2>$null
    return $LASTEXITCODE -eq 0
}

function Normalize-Set([string[]]$values) {
    return @($values | Where-Object { $null -ne $_ } |
        ForEach-Object { $_.Trim() } | Where-Object { $_ } | Sort-Object -Unique)
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
    Test-DocumentationParserPrimitives

    $trackedPaths = @(& git ls-files | ForEach-Object { $_.Replace('\', '/') })
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to enumerate tracked repository files."
    }
    $untrackedPaths = @(& git ls-files --others --exclude-standard |
        ForEach-Object { $_.Replace('\', '/') })
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to enumerate untracked repository candidates."
    }

    $script:versionedPaths = @($trackedPaths + $untrackedPaths | Sort-Object -Unique)
    $script:versionedPathSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($path in $script:versionedPaths) {
        [void]$script:versionedPathSet.Add($path)
    }

    $historyTemplatePath = 'docs/templates/HISTORY.md'
    if (Test-Path -LiteralPath $historyTemplatePath) {
        $historyTemplateText = Read-Utf8Text $historyTemplatePath
        $emDash = [char]0x2014
        if (-not $historyTemplateText.Contains("YYYY-MM-DD $emDash")) {
            Add-VerificationError "$historyTemplatePath did not decode as UTF-8 with its em dash intact."
        }
    }

    $markdownFiles = @($script:versionedPaths | Where-Object { $_.EndsWith('.md') } | Sort-Object)
    $untrackedMarkdown = @($untrackedPaths | Where-Object { $_.EndsWith('.md') })
    if ($untrackedMarkdown.Count -gt 0) {
        $notes.Add("Validated $($untrackedMarkdown.Count) untracked Markdown candidate(s); they are not clean-clone evidence until committed.")
    }

    $documentationSchemaPath = 'docs/schemas/documentation-schema.json'
    if (-not (Test-Path -LiteralPath $documentationSchemaPath)) {
        Add-VerificationError "$documentationSchemaPath is missing."
        $documentationSchema = $null
    } else {
        try {
            $documentationSchema = Read-Utf8Text $documentationSchemaPath | ConvertFrom-Json
            if ($documentationSchema.schemaVersion -ne 1) {
                Add-VerificationError "$documentationSchemaPath has unsupported schemaVersion '$($documentationSchema.schemaVersion)'."
            }
        } catch {
            Add-VerificationError "$documentationSchemaPath is not valid JSON: $($_.Exception.Message)"
            $documentationSchema = $null
        }
    }

    if ($null -ne $documentationSchema) {
        if ($null -eq $documentationSchema.agentAuthoring) {
            Add-VerificationError "$documentationSchemaPath is missing the agentAuthoring contract."
        } else {
            $agentContractPath = ([string]$documentationSchema.agentAuthoring.contractPath).Replace('\', '/')
            if (-not $agentContractPath) {
                Add-VerificationError "$documentationSchemaPath agentAuthoring contractPath is empty."
            } elseif (-not (Test-Path -LiteralPath $agentContractPath) -or
                -not (Test-VersionedCandidatePath $agentContractPath)) {
                Add-VerificationError "$documentationSchemaPath points to absent or non-versioned agent contract '$agentContractPath'."
            } elseif (-not (Test-RepositoryPathCase $agentContractPath)) {
                Add-VerificationError "$documentationSchemaPath uses incorrect casing for agent contract '$agentContractPath'."
            }

            $skillRegistryPath = ([string]$documentationSchema.agentAuthoring.skillRegistryPath).Replace('\', '/')
            $logicalSkillId = [string]$documentationSchema.agentAuthoring.logicalSkillId
            $requiredParityPolicy = [string]$documentationSchema.agentAuthoring.requiredParityPolicy
            $agentSkillRegistry = $null
            if (-not $skillRegistryPath) {
                Add-VerificationError "$documentationSchemaPath agentAuthoring skillRegistryPath is empty."
            } elseif (-not (Test-Path -LiteralPath $skillRegistryPath) -or
                -not (Test-VersionedCandidatePath $skillRegistryPath)) {
                Add-VerificationError "$documentationSchemaPath points to absent or non-versioned skill registry '$skillRegistryPath'."
            } elseif (-not (Test-RepositoryPathCase $skillRegistryPath)) {
                Add-VerificationError "$documentationSchemaPath uses incorrect casing for skill registry '$skillRegistryPath'."
            } else {
                try {
                    $agentSkillRegistry = Read-Utf8Text $skillRegistryPath | ConvertFrom-Json
                    if ($agentSkillRegistry.schemaVersion -ne 1) {
                        Add-VerificationError "$skillRegistryPath has unsupported schemaVersion '$($agentSkillRegistry.schemaVersion)'."
                    }
                } catch {
                    Add-VerificationError "$skillRegistryPath is not valid JSON: $($_.Exception.Message)"
                }
            }

            if ($null -ne $agentSkillRegistry) {
                $logicalSkills = @($agentSkillRegistry.skills | Where-Object { $_.id -ceq $logicalSkillId })
                if ($logicalSkills.Count -ne 1) {
                    Add-VerificationError "$skillRegistryPath must register logical skill '$logicalSkillId' exactly once."
                } else {
                    $logicalSkill = $logicalSkills[0]
                    if ([string]$logicalSkill.parityPolicy -cne $requiredParityPolicy) {
                        Add-VerificationError "$skillRegistryPath logical skill '$logicalSkillId' uses parity '$($logicalSkill.parityPolicy)', expected '$requiredParityPolicy'."
                    }
                    $registeredContract = ([string]$logicalSkill.sharedContract).Replace('\', '/')
                    if ($registeredContract -cne $agentContractPath) {
                        Add-VerificationError "$skillRegistryPath logical skill '$logicalSkillId' uses shared contract '$registeredContract', expected '$agentContractPath'."
                    }

                    $profileProperties = @($logicalSkill.adapters.PSObject.Properties)
                    $profileNames = @($profileProperties | ForEach-Object { $_.Name })
                    Compare-StringSets 'Registered documentation authoring profiles' `
                        @($documentationSchema.agentAuthoring.requiredProfiles) $profileNames
                    Compare-StringSets 'Documentation logical skill required profiles' `
                        @($documentationSchema.agentAuthoring.requiredProfiles) @($logicalSkill.requiredProfiles)

                    $skillPaths = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
                    foreach ($profileProperty in $profileProperties) {
                        $profileName = [string]$profileProperty.Name
                        $skillPath = ([string]$profileProperty.Value.path).Replace('\', '/')
                        $expectedSkillName = [string]$profileProperty.Value.declaredName
                        if (-not $skillPaths.Add($skillPath)) {
                            Add-VerificationError "Documentation authoring profiles reuse skill path '$skillPath'."
                        }
                        if (-not (Test-Path -LiteralPath $skillPath) -or
                            -not (Test-VersionedCandidatePath $skillPath)) {
                            Add-VerificationError "Documentation authoring profile '$profileName' points to absent or non-versioned skill '$skillPath'."
                            continue
                        }
                        if (-not (Test-RepositoryPathCase $skillPath)) {
                            Add-VerificationError "Documentation authoring profile '$profileName' uses incorrect skill casing for '$skillPath'."
                        }

                        $skillText = Read-Utf8Text $skillPath
                        $declaredNameMatch = [regex]::Match($skillText, '(?m)^name:[ \t]*(?<value>[^\r\n]+?)[ \t]*\r?$')
                        if (-not $declaredNameMatch.Success -or
                            $declaredNameMatch.Groups['value'].Value.Trim() -cne $expectedSkillName) {
                            Add-VerificationError "$skillPath does not declare registered skill name '$expectedSkillName'."
                        }
                        foreach ($field in @($documentationSchema.agentAuthoring.requiredSkillFields)) {
                            if (-not (Get-MetadataValue $skillText ([string]$field))) {
                                Add-VerificationError "$skillPath is missing agent authoring field '$field'."
                            } elseif (-not (Test-MetadataValueIsSingleLine $skillText ([string]$field))) {
                                Add-VerificationError "$skillPath agent authoring field '$field' must use one physical line."
                            }
                        }
                        $declaredContract = ((Get-MetadataValue $skillText 'Documentation contract') -replace '`', '').Trim().Replace('\', '/')
                        if ($declaredContract -cne $agentContractPath) {
                            Add-VerificationError "$skillPath declares Documentation contract '$declaredContract', expected '$agentContractPath'."
                        }
                        $declaredProfile = ((Get-MetadataValue $skillText 'Authoring profile') -replace '`', '').Trim()
                        if ($declaredProfile -cne $profileName) {
                            Add-VerificationError "$skillPath declares Authoring profile '$declaredProfile', expected '$profileName'."
                        }
                    }
                }
            }
        }
    }

    $registryFiles = @(
        'docs/README.md',
        'docs/audit/README.md',
        'docs/history/README.md',
        'docs/modules/README.md')
    $registered = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($registryFile in $registryFiles) {
        if (-not (Test-Path -LiteralPath $registryFile)) { continue }
        $registryText = Read-Utf8Text $registryFile
        foreach ($target in Get-MarkdownTargets $registryText) {
            $resolved = Resolve-RepositoryTarget $registryFile $target
            if ($null -ne $resolved -and $resolved.RelativePath.EndsWith('.md')) {
                [void]$registered.Add($resolved.RelativePath)
            }
        }
    }

    $documentIds = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::Ordinal)
    $recordIds = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::Ordinal)
    $manifestDocuments = New-Object System.Collections.Generic.List[object]
    $migrationDocuments = New-Object System.Collections.Generic.List[object]
    $technicalReferencesByBoundary = @{}

    foreach ($markdownFile in $markdownFiles) {
        $text = Read-Utf8Text $markdownFile
        $kind = Get-MetadataValue $text 'Kind'
        $lifecycle = Get-MetadataValue $text 'Lifecycle'
        $subject = Get-MetadataValue $text 'Subject'
        $documentId = Get-MetadataValue $text 'Document ID'
        $schemaVersion = Get-MetadataValue $text 'Schema version'
        $surface = Get-MetadataValue $text 'Surface'
        $boundary = Get-MetadataValue $text 'Boundary'
        $authorityRole = Get-MetadataValue $text 'Authority role'
        $mutation = Get-MetadataValue $text 'Mutation'
        $indexing = Get-MetadataValue $text 'Indexing'
        $usesExtendedSchema = [bool]$schemaVersion -or
            $markdownFile.StartsWith('docs/modules/') -or
            $markdownFile.StartsWith('docs/governance/') -or
            $markdownFile.StartsWith('docs/schemas/') -or
            $markdownFile.StartsWith('docs/templates/') -or
            $markdownFile.StartsWith('.agents/skills/nekolib-documentation/') -or
            $markdownFile.StartsWith('.claude/skills/nekolib-documentation/')

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

        if ($usesExtendedSchema -and $null -ne $documentationSchema) {
            foreach ($field in @($documentationSchema.metadata.requiredForExtendedSchema)) {
                if (-not (Get-MetadataValue $text ([string]$field))) {
                    Add-VerificationError "$markdownFile is missing schema metadata '$field'."
                } elseif (-not (Test-MetadataValueIsSingleLine $text ([string]$field))) {
                    Add-VerificationError "$markdownFile metadata '$field' must use one physical line."
                }
            }

            if ($schemaVersion -ne '1') {
                Add-VerificationError "$markdownFile has unsupported Schema version '$schemaVersion'."
            }
            foreach ($field in @('Kind', 'Lifecycle', 'Surface', 'Authority role', 'Mutation', 'Indexing')) {
                $value = Get-MetadataValue $text $field
                $property = $documentationSchema.metadata.fields.PSObject.Properties[$field]
                if ($value -and $null -ne $property -and $value -notin @($property.Value)) {
                    Add-VerificationError "$markdownFile has unsupported $field '$value'."
                }
            }
            if ($documentId -and $documentId -notmatch ([string]$documentationSchema.metadata.documentIdPattern)) {
                Add-VerificationError "$markdownFile has invalid Document ID '$documentId'."
            } elseif ($documentId) {
                if ($documentIds.ContainsKey($documentId)) {
                    Add-VerificationError "$markdownFile reuses Document ID '$documentId' from '$($documentIds[$documentId])'."
                } else {
                    $documentIds.Add($documentId, $markdownFile)
                }
            }
            if ($boundary -and $boundary -notmatch ([string]$documentationSchema.metadata.boundaryPattern)) {
                Add-VerificationError "$markdownFile has invalid Boundary '$boundary'."
            }

            $surfaceRuleProperty = $null
            if ($surface) {
                $surfaceRuleProperty = $documentationSchema.surfaceRules.PSObject.Properties[$surface]
            }
            if ($null -ne $surfaceRuleProperty) {
                $surfaceRule = $surfaceRuleProperty.Value
                if ($surfaceRule.allowedAuthorityRoles -and
                    $authorityRole -notin @($surfaceRule.allowedAuthorityRoles)) {
                    Add-VerificationError "$markdownFile surface '$surface' does not allow Authority role '$authorityRole'."
                }
                if ($surfaceRule.requiredIndexing -and
                    $indexing -ne [string]$surfaceRule.requiredIndexing) {
                    Add-VerificationError "$markdownFile surface '$surface' requires Indexing '$($surfaceRule.requiredIndexing)'."
                }
                foreach ($field in @($surfaceRule.requiredMetadata | Where-Object { $_ })) {
                    if (-not (Get-MetadataValue $text ([string]$field))) {
                        Add-VerificationError "$markdownFile surface '$surface' requires metadata '$field'."
                    } elseif (-not (Test-MetadataValueIsSingleLine $text ([string]$field))) {
                        Add-VerificationError "$markdownFile metadata '$field' must use one physical line."
                    }
                }
                if ($lifecycle -eq 'current') {
                    foreach ($field in @($surfaceRule.forbiddenMetadataWhenCurrent | Where-Object { $_ })) {
                        if (Get-MetadataValue $text ([string]$field)) {
                            Add-VerificationError "$markdownFile current surface '$surface' must not use metadata '$field'."
                        }
                    }
                }
                if ($lifecycle -eq 'historical' -and $surfaceRule.requiredMutationWhenHistorical -and
                    $mutation -ne [string]$surfaceRule.requiredMutationWhenHistorical) {
                    Add-VerificationError "$markdownFile historical surface '$surface' requires Mutation '$($surfaceRule.requiredMutationWhenHistorical)'."
                }
            }

            $canonical = (Get-MetadataValue $text 'Canonical') -replace '`', ''
            if (($authorityRole -eq 'portal' -or $indexing -eq 'pointer-only' -or $canonical) -and
                ($surface -ne 'portal' -or $authorityRole -ne 'portal' -or
                 $indexing -ne 'pointer-only' -or -not $canonical)) {
                Add-VerificationError "$markdownFile must use Surface portal, Authority role portal, Indexing pointer-only, and Canonical together."
            }
            if ($canonical) {
                $canonical = $canonical.Trim().Replace('\', '/')
                $canonicalFullPath = Join-Path $repoRoot $canonical
                if (-not (Test-Path -LiteralPath $canonicalFullPath)) {
                    Add-VerificationError "$markdownFile points Canonical to absent path '$canonical'."
                } elseif (-not (Test-VersionedCandidatePath $canonical)) {
                    Add-VerificationError "$markdownFile points Canonical to a non-versioned candidate '$canonical'."
                } elseif (-not (Test-RepositoryPathCase $canonical)) {
                    Add-VerificationError "$markdownFile uses incorrect repository casing for Canonical '$canonical'."
                }
            }
            if ($surface -eq 'portal') {
                $portalTargets = @(Get-MarkdownTargets $text)
                $nonEmptyLines = @(Read-Utf8Lines $markdownFile | Where-Object { $_.Trim() }).Count
                if ($portalTargets.Count -ne 1) {
                    Add-VerificationError "$markdownFile portal must contain exactly one Markdown target."
                } elseif ($canonical) {
                    $portalTarget = Resolve-RepositoryTarget $markdownFile $portalTargets[0]
                    if ($null -eq $portalTarget -or $portalTarget.RelativePath -cne $canonical) {
                        Add-VerificationError "$markdownFile portal link does not resolve to Canonical '$canonical'."
                    }
                }
                if ($nonEmptyLines -gt 20) {
                    Add-VerificationError "$markdownFile portal is not minimal ($nonEmptyLines non-empty lines)."
                }
            }

            if ($surface -eq 'manifest') {
                $manifestDocuments.Add([pscustomobject]@{
                    Path = $markdownFile
                    Text = $text
                    Boundary = $boundary
                })
            }
            if ($surface -eq 'technical-reference' -and $authorityRole -eq 'normative') {
                if (-not $technicalReferencesByBoundary.ContainsKey($boundary)) {
                    $technicalReferencesByBoundary[$boundary] = New-Object System.Collections.Generic.List[string]
                }
                $technicalReferencesByBoundary[$boundary].Add($markdownFile)
            }
            if ($surface -eq 'migration') {
                $migrationDocuments.Add([pscustomobject]@{
                    Path = $markdownFile
                    Boundary = $boundary
                })
            }
            if ($surface -ne [string]$documentationSchema.module.profilesOwnerSurface -and
                (Get-MetadataValue $text 'Profiles')) {
                Add-VerificationError "$markdownFile declares Profiles outside the manifest owner surface."
            }

            if ($surface -in @('issues', 'findings', 'backlog', 'validation-requirements', 'validation-evidence') -and
                -not $markdownFile.StartsWith('docs/templates/')) {
                $recordSchema = $documentationSchema.records.PSObject.Properties[$surface].Value
                foreach ($block in Get-HeadingBlocks $text) {
                    if ($block.Heading.StartsWith('Empty state')) { continue }
                    if ($block.Heading -notmatch ([string]$recordSchema.idPattern)) {
                        Add-VerificationError "$markdownFile has non-record level-two heading '$($block.Heading)'."
                        continue
                    }
                    if ($recordIds.ContainsKey($block.Heading)) {
                        Add-VerificationError "$markdownFile redefines record ID '$($block.Heading)' from '$($recordIds[$block.Heading])'."
                    } else {
                        $recordIds.Add($block.Heading, $markdownFile)
                    }
                    foreach ($field in @($recordSchema.requiredFields)) {
                        if (-not (Get-MetadataValue $block.Text ([string]$field))) {
                            Add-VerificationError "$markdownFile record '$($block.Heading)' is missing '$field'."
                        } elseif (-not (Test-MetadataValueIsSingleLine $block.Text ([string]$field))) {
                            Add-VerificationError "$markdownFile record '$($block.Heading)' field '$field' must use one physical line."
                        }
                    }

                    $statusField = if ($surface -eq 'backlog') { 'State' } else { 'Status' }
                    $status = Get-MetadataValue $block.Text $statusField
                    if ($status -and $recordSchema.statuses -and $status -notin @($recordSchema.statuses)) {
                        Add-VerificationError "$markdownFile record '$($block.Heading)' has unsupported $statusField '$status'."
                    }
                    if ($surface -eq 'validation-requirements') {
                        $classification = Get-MetadataValue $block.Text 'Classification'
                        $category = Get-MetadataValue $block.Text 'Category'
                        $level = Get-MetadataValue $block.Text 'Required evidence level'
                        if ($classification -notin @($documentationSchema.validation.requirementStatuses)) {
                            Add-VerificationError "$markdownFile record '$($block.Heading)' has unsupported Classification '$classification'."
                        }
                        if ($category -notin @($documentationSchema.validation.categories)) {
                            Add-VerificationError "$markdownFile record '$($block.Heading)' has unsupported Category '$category'."
                        }
                        if ($level -notin @($documentationSchema.validation.evidenceLevels)) {
                            Add-VerificationError "$markdownFile record '$($block.Heading)' has unsupported Required evidence level '$level'."
                        }
                        if (Get-MetadataValue $block.Text 'Result') {
                            Add-VerificationError "$markdownFile requirement '$($block.Heading)' embeds an evidence Result."
                        }
                    }
                    if ($surface -eq 'validation-evidence') {
                        $result = Get-MetadataValue $block.Text 'Result'
                        $execution = Get-MetadataValue $block.Text 'Execution'
                        $level = Get-MetadataValue $block.Text 'Evidence level'
                        if ($result -notin @($documentationSchema.validation.evidenceStatuses)) {
                            Add-VerificationError "$markdownFile record '$($block.Heading)' has unsupported Result '$result'."
                        }
                        if ($execution -notin @($documentationSchema.validation.execution)) {
                            Add-VerificationError "$markdownFile record '$($block.Heading)' has unsupported Execution '$execution'."
                        }
                        if ($level -notin @($documentationSchema.validation.evidenceLevels)) {
                            Add-VerificationError "$markdownFile record '$($block.Heading)' has unsupported Evidence level '$level'."
                        }
                        if (Get-MetadataValue $block.Text 'Classification') {
                            Add-VerificationError "$markdownFile evidence '$($block.Heading)' embeds a requirement Classification."
                        }
                    }
                }
            }

            if ($surface -eq 'history') {
                if ($mutation -ne 'append-only') {
                    Add-VerificationError "$markdownFile history must use Mutation append-only."
                }
                $historySchema = $documentationSchema.records.history
                $lastDate = $null
                foreach ($block in Get-HeadingBlocks $text) {
                    if ($block.Heading.StartsWith('Empty state')) { continue }
                    if ($block.Heading -notmatch '^(?<date>\d{4}-\d{2}-\d{2})\s+\u2014\s+(?<id>[A-Z][A-Z0-9.]*-HISTORY-[0-9]{3})\s+\u2014\s+') {
                        Add-VerificationError "$markdownFile history heading '$($block.Heading)' must contain an ISO date and stable history ID."
                        continue
                    }
                    $date = [datetime]::ParseExact($matches['date'], 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
                    $historyId = $matches['id']
                    if ($historyId -notmatch ([string]$historySchema.idPattern)) {
                        Add-VerificationError "$markdownFile history uses invalid ID '$historyId'."
                    } elseif ($recordIds.ContainsKey($historyId)) {
                        Add-VerificationError "$markdownFile redefines record ID '$historyId' from '$($recordIds[$historyId])'."
                    } else {
                        $recordIds.Add($historyId, $markdownFile)
                    }
                    foreach ($field in @($historySchema.requiredFields)) {
                        if (-not (Get-MetadataValue $block.Text ([string]$field))) {
                            Add-VerificationError "$markdownFile history '$historyId' is missing '$field'."
                        } elseif (-not (Test-MetadataValueIsSingleLine $block.Text ([string]$field))) {
                            Add-VerificationError "$markdownFile history '$historyId' field '$field' must use one physical line."
                        }
                    }
                    if ($null -ne $lastDate -and $date -lt $lastDate) {
                        Add-VerificationError "$markdownFile history is not in ascending append order at '$($block.Heading)'."
                    }
                    $lastDate = $date
                }
                $headPath = @(& git -C $repoRoot ls-tree -r --name-only HEAD -- $markdownFile)
                if ($markdownFile -in $headPath) {
                    $headText = ((& git -C $repoRoot show ("HEAD:" + $markdownFile)) -join "`n") + "`n"
                    $normalizedCurrent = $text.Replace("`r`n", "`n")
                    if (-not $normalizedCurrent.StartsWith($headText, [StringComparison]::Ordinal)) {
                        Add-VerificationError "$markdownFile changed existing append-only history content."
                    }
                }
            }
            if ($surface -eq 'changelog' -and $authorityRole -ne 'portal') {
                $changelogSchema = $documentationSchema.records.changelog
                foreach ($block in Get-HeadingBlocks $text) {
                    if ($block.Heading.StartsWith('Empty state')) { continue }
                    if ($block.Heading -notmatch ([string]$changelogSchema.idPattern)) {
                        Add-VerificationError "$markdownFile changelog heading '$($block.Heading)' is not a release or Unreleased."
                        continue
                    }
                    foreach ($field in @($changelogSchema.requiredFields)) {
                        if (-not (Get-MetadataValue $block.Text ([string]$field))) {
                            Add-VerificationError "$markdownFile changelog '$($block.Heading)' is missing '$field'."
                        } elseif (-not (Test-MetadataValueIsSingleLine $block.Text ([string]$field))) {
                            Add-VerificationError "$markdownFile changelog '$($block.Heading)' field '$field' must use one physical line."
                        }
                    }
                }
            }
        }

        if (($markdownFile.StartsWith('docs/audit/') -and $markdownFile -ne 'docs/audit/README.md') -or
            $surface -eq 'audit') {
            foreach ($field in @(
                'Kind', 'Lifecycle', 'Subject', 'Reference date',
                'Reference commit', 'Last reconciliation', 'Current state')) {
                if (-not (Get-MetadataValue $text $field)) {
                    Add-VerificationError "$markdownFile is missing audit metadata '$field'."
                } elseif ($usesExtendedSchema -and -not (Test-MetadataValueIsSingleLine $text $field)) {
                    Add-VerificationError "$markdownFile audit metadata '$field' must use one physical line."
                }
            }
            if ($markdownFile.StartsWith('docs/modules/') -and -not (Get-MetadataValue $text 'Original path')) {
                Add-VerificationError "$markdownFile is a moved module audit without Original path."
            }
        }

        $referenceCommit = (Get-MetadataValue $text 'Reference commit') -replace '`', ''
        if ($referenceCommit) { $referenceCommit = $referenceCommit.Trim() }
        $originalPath = Get-MetadataValue $text 'Original path'
        $lineNumber = 0
        $insideFence = $false
        foreach ($line in Read-Utf8Lines $markdownFile) {
            $lineNumber++
            if ($line -match '^\s*(`{3,}|~{3,})') {
                $insideFence = -not $insideFence
                continue
            }
            if ($insideFence) { continue }
            foreach ($target in Get-MarkdownTargets $line) {
                $targetParts = $target -split '#', 2
                $anchor = if ($targetParts.Count -eq 2) { $targetParts[1] } else { $null }
                if ($target.StartsWith('#')) {
                    if ($usesExtendedSchema -and -not (Test-MarkdownAnchor (Join-Path $repoRoot $markdownFile) $anchor)) {
                        Add-VerificationError "$markdownFile`:$lineNumber links to absent anchor '#$anchor'."
                    }
                    continue
                }
                $resolved = Resolve-RepositoryTarget $markdownFile $target
                if ($null -eq $resolved) { continue }

                if (Test-Path -LiteralPath $resolved.FullPath) {
                    if (-not (Test-VersionedCandidatePath $resolved.RelativePath)) {
                        & git -C $repoRoot check-ignore -q -- $resolved.RelativePath
                        $reason = if ($LASTEXITCODE -eq 0) { 'ignored' } else { 'untracked' }
                        Add-VerificationError "$markdownFile`:$lineNumber links to $reason path '$($resolved.RelativePath)'."
                    }
                    if ($usesExtendedSchema -and -not (Test-RepositoryPathCase $resolved.RelativePath)) {
                        Add-VerificationError "$markdownFile`:$lineNumber uses incorrect repository casing for '$($resolved.RelativePath)'."
                    }
                    if ($usesExtendedSchema -and $anchor -and $resolved.RelativePath.EndsWith('.md') -and
                        -not (Test-MarkdownAnchor $resolved.FullPath $anchor)) {
                        Add-VerificationError "$markdownFile`:$lineNumber links to absent anchor '#$anchor' in '$($resolved.RelativePath)'."
                    }
                    continue
                }

                if ($kind -eq 'audit' -and (Test-PathAtCommit $referenceCommit $resolved.RelativePath)) {
                    continue
                }
                if ($kind -eq 'audit' -and $originalPath) {
                    $historicalTarget = Resolve-RepositoryTarget $originalPath $target
                    if ($null -ne $historicalTarget -and
                        (Test-PathAtCommit $referenceCommit $historicalTarget.RelativePath)) {
                        continue
                    }
                }

                Add-VerificationError "$markdownFile`:$lineNumber links to absent path '$($resolved.RelativePath)'."
            }
        }
    }

    $readme = Read-Utf8Text 'README.md'
    $moduleRows = @{}
    foreach ($line in Read-Utf8Lines 'README.md') {
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
        [xml]$xml = Read-Utf8Text $project.FullName
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

    if ($null -ne $documentationSchema) {
        $manifestBoundaries = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::Ordinal)
        $profileNames = @($documentationSchema.validation.profiles.PSObject.Properties.Name)
        foreach ($manifest in $manifestDocuments) {
            if ($manifestBoundaries.ContainsKey($manifest.Boundary)) {
                Add-VerificationError "$($manifest.Path) duplicates boundary '$($manifest.Boundary)' from '$($manifestBoundaries[$manifest.Boundary])'."
            } else {
                $manifestBoundaries.Add($manifest.Boundary, $manifest.Path)
            }
            if (-not $registered.Contains($manifest.Path)) {
                Add-VerificationError "$($manifest.Path) is absent from the module index."
            }

            foreach ($field in @($documentationSchema.manifest.requiredFields)) {
                if (-not (Get-MetadataValue $manifest.Text ([string]$field))) {
                    Add-VerificationError "$($manifest.Path) is missing manifest field '$field'."
                } elseif (-not (Test-MetadataValueIsSingleLine $manifest.Text ([string]$field))) {
                    Add-VerificationError "$($manifest.Path) manifest field '$field' must use one physical line."
                }
            }

            $moduleRoot = (Split-Path -Parent $manifest.Path).Replace('\', '/')
            foreach ($requiredFile in @($documentationSchema.module.requiredSurfaces)) {
                $requiredPath = "$moduleRoot/$requiredFile"
                if (-not (Test-Path -LiteralPath $requiredPath) -or
                    -not (Test-VersionedCandidatePath $requiredPath)) {
                    Add-VerificationError "$($manifest.Path) is missing required module surface '$requiredPath'."
                }
            }
            foreach ($requiredDirectory in @($documentationSchema.module.requiredDirectories)) {
                $requiredPath = "$moduleRoot/$requiredDirectory"
                if (-not (Test-Path -LiteralPath $requiredPath -PathType Container) -or
                    -not (Test-VersionedCandidatePath $requiredPath)) {
                    Add-VerificationError "$($manifest.Path) is missing versioned module directory '$requiredPath'."
                }
            }

            $normativeReferences = if ($technicalReferencesByBoundary.ContainsKey($manifest.Boundary)) {
                @($technicalReferencesByBoundary[$manifest.Boundary])
            } else { @() }
            if ($normativeReferences.Count -ne [int]$documentationSchema.module.normativeTechnicalReferenceCount) {
                Add-VerificationError "$($manifest.Path) boundary '$($manifest.Boundary)' has $($normativeReferences.Count) normative technical references."
            }

            $technicalReference = ((Get-MetadataValue $manifest.Text 'Technical reference') -replace '`', '').Trim()
            $expectedReference = "$moduleRoot/REFERENCE.md"
            if ($technicalReference -ne $expectedReference) {
                Add-VerificationError "$($manifest.Path) routes Technical reference to '$technicalReference', expected '$expectedReference'."
            }

            $manifestProjects = Split-MetadataList (Get-MetadataValue $manifest.Text 'Projects')
            $manifestPackages = Split-MetadataList (Get-MetadataValue $manifest.Text 'Packages')
            $manifestTargets = Split-MetadataList (Get-MetadataValue $manifest.Text 'Targets')
            $manifestDependencies = Split-MetadataList (Get-MetadataValue $manifest.Text 'Project dependencies')
            $manifestPackageDependencies = Split-MetadataList (Get-MetadataValue $manifest.Text 'Package dependencies')
            $manifestBaselines = Split-MetadataList (Get-MetadataValue $manifest.Text 'API baselines')
            $manifestExperimentalApis = Split-MetadataList (Get-MetadataValue $manifest.Text 'Experimental APIs')
            $manifestProfiles = Split-MetadataList (Get-MetadataValue $manifest.Text 'Profiles')
            $manifestRelatedBoundaries = Split-MetadataList (Get-MetadataValue $manifest.Text 'Related boundaries')
            $manifestSolutionMembership = ((Get-MetadataValue $manifest.Text 'Solution membership') -replace '`', '').Trim()
            $manifestDistribution = ((Get-MetadataValue $manifest.Text 'Distribution') -replace '`', '').Trim()
            $manifestStability = ((Get-MetadataValue $manifest.Text 'Stability') -replace '`', '').Trim()

            if ($manifestDistribution -notin @($documentationSchema.manifest.distribution)) {
                Add-VerificationError "$($manifest.Path) has unsupported Distribution '$manifestDistribution'."
            }
            if ($manifestStability -notin @($documentationSchema.manifest.stability)) {
                Add-VerificationError "$($manifest.Path) has unsupported Stability '$manifestStability'."
            }
            foreach ($pathField in @('Source', 'Tests', 'Runtime scenarios', 'Package evidence')) {
                foreach ($listedPath in Split-MetadataList (Get-MetadataValue $manifest.Text $pathField)) {
                    if (-not (Test-Path -LiteralPath $listedPath) -or
                        -not (Test-VersionedCandidatePath $listedPath)) {
                        Add-VerificationError "$($manifest.Path) lists absent or non-versioned $pathField path '$listedPath'."
                    } elseif (-not (Test-RepositoryPathCase $listedPath)) {
                        Add-VerificationError "$($manifest.Path) uses incorrect casing for $pathField path '$listedPath'."
                    }
                }
            }

            foreach ($profile in $manifestProfiles) {
                if ($profile -notin $profileNames) {
                    Add-VerificationError "$($manifest.Path) uses unknown validation profile '$profile'."
                }
            }
            foreach ($relatedBoundary in $manifestRelatedBoundaries) {
                if ($relatedBoundary -notmatch ([string]$documentationSchema.metadata.boundaryPattern)) {
                    Add-VerificationError "$($manifest.Path) uses invalid related boundary '$relatedBoundary'."
                } elseif ($relatedBoundary -eq $manifest.Boundary) {
                    Add-VerificationError "$($manifest.Path) lists its own boundary '$relatedBoundary' as related."
                }
            }

            $actualPackages = New-Object System.Collections.Generic.List[string]
            $actualTargets = New-Object System.Collections.Generic.List[string]
            $actualDependencies = New-Object System.Collections.Generic.List[string]
            $actualPackageDependencies = New-Object System.Collections.Generic.List[string]
            $membershipStates = New-Object System.Collections.Generic.List[bool]
            $packableStates = New-Object System.Collections.Generic.List[bool]
            foreach ($projectPath in $manifestProjects) {
                if (-not (Test-Path -LiteralPath $projectPath) -or
                    -not (Test-VersionedCandidatePath $projectPath)) {
                    Add-VerificationError "$($manifest.Path) lists absent or non-versioned project '$projectPath'."
                    continue
                }
                [xml]$projectXml = Read-Utf8Text $projectPath
                $projectFile = Get-Item -LiteralPath $projectPath
                $package = @($projectXml.Project.PropertyGroup.PackageId | Where-Object { $_ } | Select-Object -First 1)
                if (-not $package) { $package = $projectFile.BaseName }
                $actualPackages.Add([string]$package)

                $targetNode = @($projectXml.Project.PropertyGroup.TargetFrameworks | Where-Object { $_ } | Select-Object -First 1)
                if (-not $targetNode) {
                    $targetNode = @($projectXml.Project.PropertyGroup.TargetFramework | Where-Object { $_ } | Select-Object -First 1)
                }
                foreach ($target in ([string]$targetNode -split ';')) {
                    if ($target) { $actualTargets.Add($target) }
                }

                foreach ($reference in @($projectXml.Project.ItemGroup.ProjectReference.Include | Where-Object { $_ })) {
                    $referencePath = [IO.Path]::GetFullPath((Join-Path $projectFile.Directory.FullName $reference))
                    [xml]$referenceXml = Read-Utf8Text $referencePath
                    $dependency = @($referenceXml.Project.PropertyGroup.PackageId | Where-Object { $_ } | Select-Object -First 1)
                    if (-not $dependency) { $dependency = [IO.Path]::GetFileNameWithoutExtension($referencePath) }
                    $actualDependencies.Add([string]$dependency)
                }
                foreach ($packageReference in @($projectXml.Project.ItemGroup.PackageReference.Include | Where-Object { $_ })) {
                    $actualPackageDependencies.Add([string]$packageReference)
                }
                $membershipStates.Add((Get-RepoRelativePath $projectFile.FullName) -in $solutionProjects)
                $isPackable = @($projectXml.Project.PropertyGroup.IsPackable | Where-Object { $_ } | Select-Object -First 1)
                $packableStates.Add(([string]$isPackable).Equals('true', [StringComparison]::OrdinalIgnoreCase))
            }

            Compare-StringSets "$($manifest.Path) packages" @($actualPackages) $manifestPackages
            Compare-StringSets "$($manifest.Path) targets" @($actualTargets) $manifestTargets
            Compare-StringSets "$($manifest.Path) project dependencies" @($actualDependencies) $manifestDependencies
            Compare-StringSets "$($manifest.Path) package dependencies" @($actualPackageDependencies) $manifestPackageDependencies

            $actualMembership = if ($membershipStates.Count -gt 0 -and $false -notin @($membershipStates)) {
                'included'
            } elseif ($membershipStates.Count -gt 0 -and $true -notin @($membershipStates)) {
                'excluded'
            } else {
                'mixed'
            }
            if ($manifestSolutionMembership -ne $actualMembership) {
                Add-VerificationError "$($manifest.Path) says Solution membership '$manifestSolutionMembership', actual state is '$actualMembership'."
            }

            $actualDistribution = if ('NekoLib.Watchdog.Host' -in @($actualPackages)) {
                'deployment-package'
            } elseif ($packableStates.Count -gt 0 -and $false -notin @($packableStates)) {
                'shipped-library'
            } else {
                'unshipped'
            }
            if ($manifestDistribution -ne $actualDistribution) {
                Add-VerificationError "$($manifest.Path) says Distribution '$manifestDistribution', actual project state is '$actualDistribution'."
            }

            $actualBaselines = @()
            foreach ($package in $actualPackages) {
                $prefix = "eng/public-api/$package/"
                $actualBaselines += @($trackedPaths | Where-Object {
                    $_.StartsWith($prefix, [StringComparison]::Ordinal) -and $_.EndsWith('.approved.txt')
                })
            }
            Compare-StringSets "$($manifest.Path) API baselines" $actualBaselines $manifestBaselines

            $actualExperimentalApis = @()
            foreach ($baseline in $actualBaselines) {
                $actualExperimentalApis += @([regex]::Matches(
                    (Read-Utf8Text $baseline),
                    '\bNEKOEXP\d{4}\b') | ForEach-Object { $_.Value })
            }
            Compare-StringSets "$($manifest.Path) experimental API IDs" $actualExperimentalApis $manifestExperimentalApis
        }

        foreach ($migration in $migrationDocuments) {
            if (-not $manifestBoundaries.ContainsKey($migration.Boundary)) {
                Add-VerificationError "$($migration.Path) migration boundary '$($migration.Boundary)' has no module manifest."
            }
        }
        foreach ($referenceBoundary in @($technicalReferencesByBoundary.Keys)) {
            if (-not $manifestBoundaries.ContainsKey($referenceBoundary)) {
                foreach ($referencePath in @($technicalReferencesByBoundary[$referenceBoundary])) {
                    Add-VerificationError "$referencePath is an orphan normative technical reference for boundary '$referenceBoundary'."
                }
            }
        }
    }

    $packText = Read-Utf8Text 'eng/pack-local.ps1'
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
