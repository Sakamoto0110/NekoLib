[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$repoPrefix = $repoRoot.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$registryRelativePath = "docs/schemas/agent-skill-registry.json"
$registryPath = Join-Path $repoRoot $registryRelativePath
$errors = New-Object System.Collections.Generic.List[string]
$notes = New-Object System.Collections.Generic.List[string]
$utf8Encoding = New-Object System.Text.UTF8Encoding -ArgumentList $false, $true

function Add-VerificationError([string]$message) {
    $script:errors.Add($message)
}

function Read-Utf8Text([string]$path) {
    return [IO.File]::ReadAllText([IO.Path]::GetFullPath($path), $script:utf8Encoding)
}

function Get-ObjectProperties($value) {
    if ($null -eq $value) { return @() }
    return @($value.PSObject.Properties)
}

function Get-StringValues($value) {
    if ($null -eq $value) { return @() }
    return @($value | ForEach-Object { [string]$_ })
}

function Test-NonEmptyString($value) {
    return $null -ne $value -and -not [string]::IsNullOrWhiteSpace([string]$value)
}

function Test-SafeRelativePath([string]$relativePath) {
    if ([string]::IsNullOrWhiteSpace($relativePath)) { return $false }
    if ($relativePath.Contains('\')) { return $false }
    if ([IO.Path]::IsPathRooted($relativePath)) { return $false }
    return -not @($relativePath.Split('/')).Contains('..')
}

function Get-RepositoryPath([string]$relativePath) {
    if (-not (Test-SafeRelativePath $relativePath)) { return $null }
    $absolute = [IO.Path]::GetFullPath((Join-Path $script:repoRoot $relativePath))
    if (-not $absolute.StartsWith($script:repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        return $null
    }
    return $absolute
}

function Test-VersionedCandidatePath([string]$relativePath) {
    $normalized = $relativePath.Replace('\', '/').TrimEnd('/')
    if ($script:versionedPathSet.Contains($normalized)) { return $true }

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
        if ($match.Count -ne 1) { return $false }
        $current = $match[0].FullName
    }
    return $true
}

function Test-RegisteredFile([string]$relativePath, [string]$label) {
    if (-not (Test-SafeRelativePath $relativePath)) {
        Add-VerificationError "$label must be a safe repository-relative path with forward slashes: $relativePath"
        return $false
    }

    $fullPath = Get-RepositoryPath $relativePath
    if ($null -eq $fullPath -or -not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        Add-VerificationError "$label does not exist: $relativePath"
        return $false
    }
    if (-not (Test-VersionedCandidatePath $relativePath)) {
        Add-VerificationError "$label is ignored or outside the tracked/untracked versioned candidate set: $relativePath"
    }
    if (-not (Test-RepositoryPathCase $relativePath)) {
        Add-VerificationError "$label does not use exact repository casing: $relativePath"
    }
    return $true
}

function Get-FrontmatterValue([string]$text, [string]$fieldName, [string]$adapterPath) {
    $frontmatter = [regex]::Match(
        $text,
        '\A---\r?\n(?<body>.*?)\r?\n---(?:\r?\n|\z)',
        [Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $frontmatter.Success) {
        Add-VerificationError "Adapter has no valid YAML frontmatter: $adapterPath"
        return $null
    }

    $pattern = '(?m)^' + [regex]::Escape($fieldName) + ':[ \t]*(?<value>[^\r\n]*?)[ \t]*\r?$'
    $fieldMatches = [regex]::Matches($frontmatter.Groups['body'].Value, $pattern)
    if ($fieldMatches.Count -ne 1) {
        Add-VerificationError "Adapter must declare exactly one frontmatter '$fieldName': $adapterPath"
        return $null
    }

    $value = $fieldMatches[0].Groups['value'].Value.Trim()
    if ($value.Length -eq 1 -and ($value -eq '"' -or $value -eq "'")) {
        Add-VerificationError "Adapter frontmatter '$fieldName' has a malformed quoted value: $adapterPath"
        return $null
    }
    if ($value.Length -ge 2 -and
        (($value.StartsWith('"') -and $value.EndsWith('"')) -or
        ($value.StartsWith("'") -and $value.EndsWith("'")))) {
        $value = $value.Substring(1, $value.Length - 2)
    }
    return $value
}

if (-not (Test-Path -LiteralPath $registryPath -PathType Leaf)) {
    Write-Error "Skill registry not found: $registryRelativePath"
    exit 1
}

$gitPaths = @(& git -C $repoRoot ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) {
    Write-Error "Unable to enumerate tracked and untracked versioned candidates."
    exit 1
}
$versionedPaths = @($gitPaths | ForEach-Object { $_.Replace('\', '/') } | Sort-Object -Unique)
$versionedPathSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
foreach ($path in $versionedPaths) { [void]$versionedPathSet.Add($path) }

try {
    $registry = Read-Utf8Text $registryPath | ConvertFrom-Json
} catch {
    Write-Error "Skill registry is not valid strict UTF-8 JSON: $($_.Exception.Message)"
    exit 1
}

if ($registry.schemaVersion -ne 1) {
    Add-VerificationError "Registry schemaVersion must be 1."
}
if (-not (Test-NonEmptyString $registry.schemaId)) {
    Add-VerificationError "Registry schemaId must be non-empty."
}
if (-not (Test-NonEmptyString $registry.description) -or
    -not (Test-NonEmptyString $registry.authority)) {
    Add-VerificationError "Registry description and authority must be non-empty."
}
if ($registry.verifierPath -ne 'eng/verify-skills.ps1') {
    Add-VerificationError "Registry verifierPath must be eng/verify-skills.ps1."
} else {
    [void](Test-RegisteredFile $registry.verifierPath 'Registry verifier')
}

$profileProperties = @(Get-ObjectProperties $registry.agentProfiles)
$policyProperties = @(Get-ObjectProperties $registry.parityPolicies)
$skills = @($registry.skills)
$validationRules = @(Get-StringValues $registry.validationRules)

if ($profileProperties.Count -eq 0) { Add-VerificationError "Registry must define agentProfiles." }
if ($policyProperties.Count -eq 0) { Add-VerificationError "Registry must define parityPolicies." }
if ($skills.Count -eq 0) { Add-VerificationError "Registry must define skills." }
if ($validationRules.Count -eq 0 -or @($validationRules | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0) {
    Add-VerificationError "Registry validationRules must contain only non-empty rules."
}

foreach ($field in @('changedAdapters', 'fullRegistry', 'mutationBoundary')) {
    if (-not (Test-NonEmptyString $registry.reviewPolicy.$field)) {
        Add-VerificationError "Registry reviewPolicy.$field must be non-empty."
    }
}

$profiles = @{}
foreach ($property in $profileProperties) {
    $profileName = [string]$property.Name
    $profile = $property.Value
    if ($profiles.ContainsKey($profileName)) {
        Add-VerificationError "Duplicate agent profile: $profileName"
        continue
    }
    $profiles[$profileName] = $profile

    if (-not (Test-SafeRelativePath ([string]$profile.root))) {
        Add-VerificationError "Agent profile '$profileName' has an invalid root: $($profile.root)"
    } else {
        $rootPath = Get-RepositoryPath ([string]$profile.root)
        if ($null -eq $rootPath -or -not (Test-Path -LiteralPath $rootPath -PathType Container)) {
            Add-VerificationError "Agent profile '$profileName' root does not exist: $($profile.root)"
        }
        if (-not (Test-VersionedCandidatePath ([string]$profile.root))) {
            Add-VerificationError "Agent profile '$profileName' root is outside the versioned candidate set: $($profile.root)"
        }
        if (-not (Test-RepositoryPathCase ([string]$profile.root))) {
            Add-VerificationError "Agent profile '$profileName' root has incorrect casing: $($profile.root)"
        }
    }
    if (-not (Test-NonEmptyString $profile.entryPoint) -or
        ([string]$profile.entryPoint).Contains('/') -or
        ([string]$profile.entryPoint).Contains('\')) {
        Add-VerificationError "Agent profile '$profileName' must define a filename-only entryPoint."
    }
}

$policies = @{}
foreach ($property in $policyProperties) {
    $policyName = [string]$property.Name
    $policy = $property.Value
    $policies[$policyName] = $policy
    if (-not (Test-NonEmptyString $policy.meaning)) {
        Add-VerificationError "Parity policy '$policyName' must define a meaning."
    }
    $requiredCommonality = @(Get-StringValues $policy.requiredCommonality)
    if ($policyName -ne 'single-profile' -and $requiredCommonality.Count -eq 0) {
        Add-VerificationError "Paired parity policy '$policyName' must define requiredCommonality."
    }
}

$logicalSkillIds = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
$registeredAdapterPaths = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
$adapterCount = 0

foreach ($skill in $skills) {
    $skillId = [string]$skill.id
    if ($skillId -notmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$') {
        Add-VerificationError "Logical skill id must be lowercase kebab-case: $skillId"
    }
    if (-not $logicalSkillIds.Add($skillId)) {
        Add-VerificationError "Duplicate logical skill id: $skillId"
    }
    if (-not (Test-NonEmptyString $skill.description)) {
        Add-VerificationError "Logical skill '$skillId' must define a description."
    }

    $policyName = [string]$skill.parityPolicy
    if (-not $policies.ContainsKey($policyName)) {
        Add-VerificationError "Logical skill '$skillId' uses unknown parity policy '$policyName'."
    }

    $adapterProperties = Get-ObjectProperties $skill.adapters
    $requiredProfiles = @(Get-StringValues $skill.requiredProfiles)
    $adapterProfiles = @($adapterProperties | ForEach-Object { [string]$_.Name })
    if ($requiredProfiles.Count -ne @($requiredProfiles | Sort-Object -Unique).Count) {
        Add-VerificationError "Logical skill '$skillId' repeats a required profile."
    }
    if ($requiredProfiles.Count -ne $adapterProfiles.Count -or
        @($requiredProfiles | Where-Object { $adapterProfiles -notcontains $_ }).Count -gt 0 -or
        @($adapterProfiles | Where-Object { $requiredProfiles -notcontains $_ }).Count -gt 0) {
        Add-VerificationError "Logical skill '$skillId' requiredProfiles must exactly match adapter profiles."
    }
    foreach ($profileName in @(@($requiredProfiles) + @($adapterProfiles) | Sort-Object -Unique)) {
        if (-not $profiles.ContainsKey($profileName)) {
            Add-VerificationError "Logical skill '$skillId' refers to unregistered profile '$profileName'."
        }
    }

    if ($policyName -eq 'single-profile' -and $adapterProperties.Count -ne 1) {
        Add-VerificationError "Single-profile skill '$skillId' must have exactly one adapter."
    }
    if ($policyName -ne 'single-profile' -and $adapterProperties.Count -lt 2) {
        Add-VerificationError "Paired skill '$skillId' must have at least two adapters."
    }

    $sharedContract = [string]$skill.sharedContract
    if ($policyName -eq 'single-profile' -and -not [string]::IsNullOrWhiteSpace($sharedContract)) {
        Add-VerificationError "Single-profile skill '$skillId' must not declare a sharedContract."
    }
    if ($policyName -ne 'single-profile') {
        if ([string]::IsNullOrWhiteSpace($sharedContract)) {
            Add-VerificationError "Paired skill '$skillId' must declare a sharedContract."
        } else {
            [void](Test-RegisteredFile $sharedContract "Shared contract for '$skillId'")
        }
    }

    $commonModes = @(Get-StringValues $skill.commonModes)
    if ($policyName -eq 'near-mirror' -and $commonModes.Count -eq 0) {
        Add-VerificationError "Near-mirror skill '$skillId' must declare commonModes."
    }
    if ($commonModes.Count -ne @($commonModes | Sort-Object -Unique).Count -or
        @($commonModes | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0) {
        Add-VerificationError "Logical skill '$skillId' commonModes must be unique and non-empty."
    }

    foreach ($adapterProperty in $adapterProperties) {
        $adapterCount++
        $profileName = [string]$adapterProperty.Name
        $adapter = $adapterProperty.Value
        $adapterPath = [string]$adapter.path
        $declaredName = [string]$adapter.declaredName

        if (-not $registeredAdapterPaths.Add($adapterPath)) {
            Add-VerificationError "Adapter path is registered more than once: $adapterPath"
        }
        if (-not $profiles.ContainsKey($profileName)) { continue }

        $profile = $profiles[$profileName]
        $root = ([string]$profile.root).TrimEnd('/')
        $expectedSuffix = '/' + [string]$profile.entryPoint
        if (-not $adapterPath.StartsWith($root + '/', [StringComparison]::Ordinal) -or
            -not $adapterPath.EndsWith($expectedSuffix, [StringComparison]::Ordinal)) {
            Add-VerificationError "Adapter '$adapterPath' does not match profile '$profileName' root and entryPoint."
        }

        $adapterExists = Test-RegisteredFile $adapterPath "Adapter for '$skillId'/$profileName"
        if (-not $adapterExists) { continue }

        $adapterText = Read-Utf8Text (Get-RepositoryPath $adapterPath)
        $actualName = Get-FrontmatterValue $adapterText 'name' $adapterPath
        $description = Get-FrontmatterValue $adapterText 'description' $adapterPath
        if ($actualName -cne $declaredName) {
            Add-VerificationError "Adapter '$adapterPath' declares name '$actualName'; registry expects '$declaredName'."
        }
        if ([string]::IsNullOrWhiteSpace($description)) {
            Add-VerificationError "Adapter '$adapterPath' must declare a non-empty description."
        }

        if ($policyName -eq 'near-mirror') {
            foreach ($mode in $commonModes) {
                if ($adapterText.IndexOf($mode, [StringComparison]::Ordinal) -lt 0) {
                    Add-VerificationError "Near-mirror adapter '$adapterPath' does not expose common mode '$mode'."
                }
            }
        }

        if (Test-NonEmptyString $profile.optionalMetadata) {
            $adapterDirectory = $adapterPath.Substring(0, $adapterPath.Length - $expectedSuffix.Length)
            $metadataPath = $adapterDirectory + '/' + [string]$profile.optionalMetadata
            $metadataFullPath = Get-RepositoryPath $metadataPath
            if ($null -ne $metadataFullPath -and (Test-Path -LiteralPath $metadataFullPath -PathType Leaf)) {
                if (Test-RegisteredFile $metadataPath "Optional metadata for '$adapterPath'") {
                    try { [void](Read-Utf8Text $metadataFullPath) }
                    catch { Add-VerificationError "Optional metadata is not strict UTF-8: $metadataPath" }
                }
            }
        }
    }
}

foreach ($profileName in $profiles.Keys) {
    $profile = $profiles[$profileName]
    $rootPrefix = ([string]$profile.root).TrimEnd('/') + '/'
    $entryPointSuffix = '/' + [string]$profile.entryPoint
    $actualEntrypoints = @($versionedPaths | Where-Object {
        $_.StartsWith($rootPrefix, [StringComparison]::Ordinal) -and
        $_.EndsWith($entryPointSuffix, [StringComparison]::Ordinal)
    })
    foreach ($entrypoint in $actualEntrypoints) {
        if (-not $registeredAdapterPaths.Contains($entrypoint)) {
            Add-VerificationError "Repository-owned skill entrypoint is not registered: $entrypoint"
        }
    }
    foreach ($registeredPath in $registeredAdapterPaths) {
        if ($registeredPath.StartsWith($rootPrefix, [StringComparison]::Ordinal) -and
            $registeredPath.EndsWith($entryPointSuffix, [StringComparison]::Ordinal) -and
            $actualEntrypoints -notcontains $registeredPath) {
            Add-VerificationError "Registered adapter is not an eligible versioned entrypoint: $registeredPath"
        }
    }
}

if ($errors.Count -gt 0) {
    Write-Host "Skill registry verification failed with $($errors.Count) error(s):" -ForegroundColor Red
    foreach ($message in $errors) { Write-Host "- $message" -ForegroundColor Red }
    exit 1
}

Write-Host "Skill registry verification passed." -ForegroundColor Green
Write-Host "Validated $($skills.Count) logical skill(s), $adapterCount adapter(s), $($profiles.Count) agent profile(s), and $($policies.Count) parity policy/policies."
Write-Host "Semantic parity and authored technical truth remain review concerns; this verifier proves deterministic registry topology and declared common modes only."
