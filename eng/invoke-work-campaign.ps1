[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Campaign,

    [ValidateSet("pre-commit", "post-commit")]
    [string]$Phase = "pre-commit",

    [switch]$ValidateOnly,

    [switch]$Execute,

    [switch]$Force
)

$ErrorActionPreference = "Stop"

if ($ValidateOnly -and ($Execute -or $Force)) {
    throw "-ValidateOnly cannot be combined with -Execute or -Force."
}
if ($Force -and -not $Execute) {
    throw "-Force requires -Execute."
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$repoPrefix = $repoRoot.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$localCampaignRoot = [IO.Path]::GetFullPath(
    (Join-Path $repoRoot ".local\work-campaigns"))
$localCampaignPrefix = $localCampaignRoot.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$schemaPath = Join-Path $repoRoot "docs\schemas\work-campaign-schema.json"
$strictUtf8 = New-Object System.Text.UTF8Encoding -ArgumentList $false, $true
$writeUtf8 = New-Object System.Text.UTF8Encoding -ArgumentList $false

function Test-NonEmptyString($value) {
    return $null -ne $value -and -not [string]::IsNullOrWhiteSpace([string]$value)
}

function Read-StrictUtf8Json([string]$path, [string]$label) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "$label does not exist: $path"
    }

    try {
        $text = [IO.File]::ReadAllText([IO.Path]::GetFullPath($path), $script:strictUtf8)
        return $text | ConvertFrom-Json
    } catch {
        throw "$label is not valid strict UTF-8 JSON: $($_.Exception.Message)"
    }
}

function Assert-AllowedProperties($value, [string[]]$allowed, [string]$label) {
    if ($null -eq $value) {
        throw "$label is required."
    }

    foreach ($property in @($value.PSObject.Properties)) {
        if ($property.Name -notin $allowed) {
            throw "$label contains unsupported property '$($property.Name)'."
        }
    }
}

function Assert-RequiredProperties($value, [string[]]$required, [string]$label) {
    foreach ($propertyName in $required) {
        $property = $value.PSObject.Properties[$propertyName]
        if ($null -eq $property) {
            throw "$label is missing required property '$propertyName'."
        }
    }
}

function Test-SafeRelativePath([string]$value, [switch]$AllowDot) {
    if ([string]::IsNullOrWhiteSpace($value)) { return $false }
    if ($AllowDot -and $value -eq ".") { return $true }
    if ($value -eq ".") { return $false }
    if ($value.Contains('\')) { return $false }
    if ($value.Contains("`r") -or $value.Contains("`n") -or $value.Contains([char]0)) {
        return $false
    }
    if ([IO.Path]::IsPathRooted($value)) { return $false }
    if ($value -match '^[A-Za-z]:') { return $false }
    if (@($value.Split('/')) -contains '..') { return $false }
    return $value.IndexOfAny([char[]]'*?[]') -lt 0
}

function Test-SafePattern([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value) -or $value -eq '.') { return $false }
    if ($value.Contains('\')) { return $false }
    if ($value.Contains("`r") -or $value.Contains("`n") -or $value.Contains([char]0)) {
        return $false
    }
    if ([IO.Path]::IsPathRooted($value) -or $value -match '^[A-Za-z]:') {
        return $false
    }
    return @($value.Split('/')) -notcontains '..'
}

function Get-RepositoryPath([string]$relativePath, [switch]$AllowDot) {
    if (-not (Test-SafeRelativePath $relativePath -AllowDot:$AllowDot)) {
        throw "Unsafe repository-relative path: $relativePath"
    }

    $absolute = if ($relativePath -eq ".") {
        $script:repoRoot
    } else {
        [IO.Path]::GetFullPath((Join-Path $script:repoRoot $relativePath))
    }
    if (-not $absolute.Equals($script:repoRoot, [StringComparison]::OrdinalIgnoreCase) -and
        -not $absolute.StartsWith($script:repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Repository-relative path escaped the repository: $relativePath"
    }
    return $absolute
}

function Assert-UniqueStrings($values, [string]$label) {
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($value in @($values)) {
        $text = [string]$value
        if (-not $seen.Add($text)) {
            throw "$label repeats '$text'."
        }
    }
}

function Assert-SafeArguments($arguments, [string]$label) {
    foreach ($argument in @($arguments)) {
        $text = [string]$argument
        if ($text.Contains("`r") -or $text.Contains("`n") -or $text.Contains([char]0)) {
            throw "$label contains an argument with a newline or NUL character."
        }
    }
}

function Assert-ExecutableContract($finalizer, [string]$label) {
    $executable = [string]$finalizer.executable
    $arguments = @($finalizer.arguments | ForEach-Object { [string]$_ })
    if ($executable.Contains('/') -or $executable.Contains('\')) {
        $normalized = $executable.Replace('\', '/')
        if (-not (Test-SafeRelativePath $normalized) -or
            -not $normalized.StartsWith('eng/', [StringComparison]::Ordinal) -or
            -not $normalized.EndsWith('.ps1', [StringComparison]::OrdinalIgnoreCase)) {
            throw "$label executable must be a repository eng/*.ps1 script or an allowed executable name."
        }
        return
    }

    switch -CaseSensitive ($executable) {
        "git" {
            if ($arguments.Count -eq 0 -or
                $arguments[0] -notin @('diff', 'status', 'rev-parse', 'ls-files')) {
                throw "$label may use git only for diff, status, rev-parse, or ls-files."
            }
        }
        "dotnet" {
            if ($arguments.Count -eq 0 -or
                $arguments[0] -notin @('build', 'test', 'msbuild')) {
                throw "$label may use dotnet only for build, test, or msbuild."
            }
        }
        "python" {
            if ($arguments.Count -eq 0 -or
                -not (Test-SafeRelativePath $arguments[0]) -or
                -not $arguments[0].EndsWith('.py', [StringComparison]::OrdinalIgnoreCase)) {
                throw "$label python command must name a repository-relative .py script as its first argument."
            }
        }
        default {
            throw "$label uses unsupported executable '$executable'."
        }
    }
}

function Assert-Manifest($manifest) {
    Assert-AllowedProperties $manifest @(
        'schemaVersion', 'campaignId', 'title', 'kind', 'authority',
        'baseline', 'scope', 'stages', 'finalizers', 'notes') 'Campaign manifest'
    Assert-RequiredProperties $manifest @(
        'schemaVersion', 'campaignId', 'title', 'kind', 'authority',
        'baseline', 'scope', 'stages', 'finalizers') 'Campaign manifest'

    if ($manifest.schemaVersion -ne 1) {
        throw "Campaign schemaVersion must be 1."
    }
    if ([string]$manifest.campaignId -notmatch '^[A-Z][A-Z0-9-]*$') {
        throw "campaignId must use uppercase letters, digits, and hyphens."
    }
    if (-not (Test-NonEmptyString $manifest.title)) {
        throw "Campaign title must be non-empty."
    }
    if ([string]$manifest.kind -notin @(
            'documentation', 'implementation', 'validation', 'release',
            'repository-maintenance', 'mixed')) {
        throw "Campaign kind is unsupported: $($manifest.kind)"
    }

    Assert-AllowedProperties $manifest.authority @('kind', 'source', 'references') 'authority'
    Assert-RequiredProperties $manifest.authority @('kind', 'source', 'references') 'authority'
    if ([string]$manifest.authority.kind -notin @(
            'todo', 'owner-decision', 'accepted-review', 'release-policy')) {
        throw "authority.kind is unsupported: $($manifest.authority.kind)"
    }
    if (-not (Test-NonEmptyString $manifest.authority.source)) {
        throw "authority.source must be non-empty."
    }
    Assert-UniqueStrings @($manifest.authority.references) 'authority.references'
    foreach ($path in @($manifest.authority.references)) {
        if (-not (Test-SafeRelativePath ([string]$path))) {
            throw "authority.references contains an unsafe path: $path"
        }
        $authorityPath = Get-RepositoryPath ([string]$path)
        if (-not (Test-Path -LiteralPath $authorityPath)) {
            throw "authority.references points to a missing repository path: $path"
        }
    }

    Assert-AllowedProperties $manifest.baseline @(
        'branch', 'commit', 'treeState', 'preExistingPaths') 'baseline'
    Assert-RequiredProperties $manifest.baseline @(
        'branch', 'commit', 'treeState', 'preExistingPaths') 'baseline'
    if (-not (Test-NonEmptyString $manifest.baseline.branch)) {
        throw "baseline.branch must be non-empty."
    }
    if ([string]$manifest.baseline.commit -notmatch '^[0-9a-f]{40}$') {
        throw "baseline.commit must be a full lowercase Git commit."
    }
    if ([string]$manifest.baseline.treeState -notin @('clean', 'dirty')) {
        throw "baseline.treeState must be clean or dirty."
    }
    Assert-UniqueStrings @($manifest.baseline.preExistingPaths) 'baseline.preExistingPaths'
    foreach ($path in @($manifest.baseline.preExistingPaths)) {
        if (-not (Test-SafeRelativePath ([string]$path))) {
            throw "baseline.preExistingPaths contains an unsafe path: $path"
        }
    }
    if ([string]$manifest.baseline.treeState -eq 'clean' -and
        @($manifest.baseline.preExistingPaths).Count -gt 0) {
        throw "A clean baseline cannot declare preExistingPaths."
    }
    if ([string]$manifest.baseline.treeState -eq 'dirty' -and
        @($manifest.baseline.preExistingPaths).Count -eq 0) {
        throw "A dirty baseline must declare at least one preExistingPaths entry."
    }

    Assert-AllowedProperties $manifest.scope @(
        'includePaths', 'excludePaths', 'taskIds') 'scope'
    Assert-RequiredProperties $manifest.scope @(
        'includePaths', 'excludePaths', 'taskIds') 'scope'
    if (@($manifest.scope.includePaths).Count -eq 0) {
        throw "scope.includePaths must contain at least one pattern."
    }
    foreach ($field in @('includePaths', 'excludePaths')) {
        Assert-UniqueStrings @($manifest.scope.$field) "scope.$field"
        foreach ($pattern in @($manifest.scope.$field)) {
            if (-not (Test-SafePattern ([string]$pattern))) {
                throw "scope.$field contains an unsafe pattern: $pattern"
            }
        }
    }
    Assert-UniqueStrings @($manifest.scope.taskIds) 'scope.taskIds'
    foreach ($taskId in @($manifest.scope.taskIds)) {
        if (-not (Test-NonEmptyString $taskId)) {
            throw "scope.taskIds must contain only non-empty values."
        }
    }
    foreach ($path in @($manifest.baseline.preExistingPaths)) {
        if ((Test-AnyPattern ([string]$path) @($manifest.scope.includePaths)) -and
            -not (Test-AnyPattern ([string]$path) @($manifest.scope.excludePaths))) {
            throw "Pre-existing path '$path' overlaps campaign-owned scope."
        }
    }

    $stages = @($manifest.stages)
    if ($stages.Count -lt 2) {
        throw "A work campaign must define at least two stages."
    }
    $stageIds = @()
    for ($index = 0; $index -lt $stages.Count; $index++) {
        $stage = $stages[$index]
        $label = "stages[$index]"
        Assert-AllowedProperties $stage @('id', 'description', 'completionCriteria') $label
        Assert-RequiredProperties $stage @('id', 'description', 'completionCriteria') $label
        if ([string]$stage.id -notmatch '^[a-z][a-z0-9-]*$') {
            throw "$label.id must use lowercase letters, digits, and hyphens."
        }
        if (-not (Test-NonEmptyString $stage.description)) {
            throw "$label.description must be non-empty."
        }
        if (@($stage.completionCriteria).Count -eq 0 -or
            @($stage.completionCriteria | Where-Object {
                    -not (Test-NonEmptyString $_) }).Count -gt 0) {
            throw "$label.completionCriteria must contain non-empty values."
        }
        $stageIds += [string]$stage.id
    }
    Assert-UniqueStrings $stageIds 'stage IDs'

    $finalizers = @($manifest.finalizers)
    if ($finalizers.Count -eq 0) {
        throw "A work campaign must define at least one finalizer."
    }
    $finalizerIds = @()
    for ($index = 0; $index -lt $finalizers.Count; $index++) {
        $finalizer = $finalizers[$index]
        $label = "finalizers[$index]"
        Assert-AllowedProperties $finalizer @(
            'id', 'phase', 'description', 'executable', 'arguments',
            'workingDirectory', 'whenChanged', 'requiredPaths', 'required',
            'requiresCleanTree', 'expectedExitCodes') $label
        Assert-RequiredProperties $finalizer @(
            'id', 'phase', 'description', 'executable', 'arguments',
            'workingDirectory', 'whenChanged', 'requiredPaths', 'required',
            'requiresCleanTree', 'expectedExitCodes') $label

        if ([string]$finalizer.id -notmatch '^[a-z][a-z0-9-]*$') {
            throw "$label.id must use lowercase letters, digits, and hyphens."
        }
        if ([string]$finalizer.phase -notin @('pre-commit', 'post-commit')) {
            throw "$label.phase is unsupported: $($finalizer.phase)"
        }
        if (-not (Test-NonEmptyString $finalizer.description) -or
            -not (Test-NonEmptyString $finalizer.executable)) {
            throw "$label description and executable must be non-empty."
        }
        Assert-SafeArguments @($finalizer.arguments) $label
        if (-not (Test-SafeRelativePath ([string]$finalizer.workingDirectory) -AllowDot)) {
            throw "$label.workingDirectory must be a safe repository-relative directory."
        }
        if (@($finalizer.whenChanged).Count -eq 0) {
            throw "$label.whenChanged must contain at least one pattern."
        }
        foreach ($field in @('whenChanged', 'requiredPaths')) {
            Assert-UniqueStrings @($finalizer.$field) "$label.$field"
            foreach ($value in @($finalizer.$field)) {
                $safe = if ($field -eq 'whenChanged') {
                    Test-SafePattern ([string]$value)
                } else {
                    Test-SafeRelativePath ([string]$value)
                }
                if (-not $safe) {
                    throw "$label.$field contains an unsafe value: $value"
                }
            }
        }
        if ($finalizer.required -isnot [bool] -or
            $finalizer.requiresCleanTree -isnot [bool]) {
            throw "$label required and requiresCleanTree must be Boolean."
        }
        if (@($finalizer.expectedExitCodes).Count -eq 0) {
            throw "$label.expectedExitCodes must not be empty."
        }
        foreach ($exitCode in @($finalizer.expectedExitCodes)) {
            if ($exitCode -isnot [int] -and $exitCode -isnot [long]) {
                throw "$label.expectedExitCodes must contain only integers."
            }
        }
        Assert-UniqueStrings @($finalizer.expectedExitCodes | ForEach-Object { [string]$_ }) `
            "$label.expectedExitCodes"
        Assert-ExecutableContract $finalizer $label
        $finalizerIds += [string]$finalizer.id
    }
    Assert-UniqueStrings $finalizerIds 'finalizer IDs'
}

function Invoke-Git([string[]]$arguments, [switch]$AllowFailure) {
    $output = @(& git -c core.safecrlf=false -C $script:repoRoot @arguments)
    $exitCode = $LASTEXITCODE
    if (-not $AllowFailure -and $exitCode -ne 0) {
        throw "git $($arguments -join ' ') failed with exit code $exitCode."
    }
    return [pscustomobject]@{
        Output = $output
        ExitCode = $exitCode
    }
}

function Test-PathPattern([string]$path, [string]$pattern) {
    $wildcard = New-Object System.Management.Automation.WildcardPattern(
        $pattern,
        [System.Management.Automation.WildcardOptions]::IgnoreCase)
    return $wildcard.IsMatch($path)
}

function Test-AnyPattern([string]$path, $patterns) {
    foreach ($pattern in @($patterns)) {
        if (Test-PathPattern $path ([string]$pattern)) { return $true }
    }
    return $false
}

function Get-TreeState() {
    $status = Invoke-Git @('status', '--porcelain=v1', '--untracked-files=all')
    if ($status.Output.Count -eq 0) { return 'clean' }
    return 'dirty'
}

function Get-CampaignChangedPaths($manifest) {
    $baseline = [string]$manifest.baseline.commit
    $tracked = (Invoke-Git @(
            'diff', '--name-only', '--diff-filter=ACDMRTUXB', $baseline, '--')).Output
    $untracked = (Invoke-Git @(
            'ls-files', '--others', '--exclude-standard')).Output
    $preExisting = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($path in @($manifest.baseline.preExistingPaths)) {
        [void]$preExisting.Add(([string]$path).Replace('\', '/'))
    }

    return @(
        $tracked + $untracked |
            ForEach-Object { ([string]$_).Replace('\', '/') } |
            Where-Object { $_ -and -not $preExisting.Contains($_) } |
            Sort-Object -Unique)
}

function Assert-CampaignScope($manifest, [string[]]$changedPaths) {
    $outside = @(
        $changedPaths | Where-Object {
            -not (Test-AnyPattern $_ @($manifest.scope.includePaths)) -or
            (Test-AnyPattern $_ @($manifest.scope.excludePaths))
        })
    if ($outside.Count -gt 0) {
        throw "Campaign-owned changes fall outside scope: $($outside -join ', ')"
    }
}

function Get-ChangeFingerprint($manifest, [string[]]$changedPaths, [string]$head) {
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("campaign=$($manifest.campaignId)")
    $lines.Add("baseline=$($manifest.baseline.commit)")
    $lines.Add("head=$head")

    foreach ($path in @($changedPaths | Sort-Object)) {
        $status = (Invoke-Git @('status', '--porcelain=v1', '--', $path)).Output -join "`n"
        $indexIdentity = (Invoke-Git @('ls-files', '-s', '--', $path)).Output -join "`n"
        $absolutePath = Get-RepositoryPath $path
        $contentHash = if (Test-Path -LiteralPath $absolutePath -PathType Leaf) {
            (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash
        } else {
            'MISSING'
        }
        $lines.Add("path=$path")
        $lines.Add("status=$status")
        $lines.Add("index=$indexIdentity")
        $lines.Add("content=$contentHash")
    }

    $bytes = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '')
    } finally {
        $sha.Dispose()
    }
}

function Expand-CampaignToken(
    [string]$value,
    $manifest,
    [string]$head) {
    $expanded = $value.Replace('{baseline}', [string]$manifest.baseline.commit)
    $expanded = $expanded.Replace('{head}', $head)
    $expanded = $expanded.Replace('{campaignId}', [string]$manifest.campaignId)
    return $expanded.Replace('{repoRoot}', $script:repoRoot)
}

function Get-DisplayCommand([string]$executable, [string[]]$arguments) {
    $rendered = @($executable)
    foreach ($argument in $arguments) {
        if ($argument -match '[\s"]') {
            $rendered += '"' + $argument.Replace('"', '\"') + '"'
        } else {
            $rendered += $argument
        }
    }
    return $rendered -join ' '
}

function Read-State([string]$statePath, [string]$campaignId) {
    if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) {
        return [ordered]@{
            schemaVersion = 1
            campaignId = $campaignId
            runs = @()
        }
    }

    $state = Read-StrictUtf8Json $statePath 'Campaign state'
    if ($state.schemaVersion -ne 1 -or [string]$state.campaignId -cne $campaignId) {
        throw "Campaign state does not match schema version 1 and campaign '$campaignId'."
    }
    return $state
}

function Save-State([string]$statePath, $state) {
    $directory = Split-Path -Parent $statePath
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    $json = $state | ConvertTo-Json -Depth 8
    [IO.File]::WriteAllText($statePath, $json + "`n", $script:writeUtf8)
}

function Add-StateRun(
    $state,
    [string]$finalizerId,
    [string]$phase,
    [string]$fingerprint,
    [string]$head,
    [string]$treeState,
    [string]$result,
    [int]$exitCode) {
    $runs = @($state.runs)
    $runs += [ordered]@{
        finalizerId = $finalizerId
        phase = $phase
        fingerprint = $fingerprint
        headCommit = $head
        treeState = $treeState
        recordedAtUtc = [DateTime]::UtcNow.ToString('o')
        result = $result
        exitCode = $exitCode
    }
    $state.runs = $runs
}

function Invoke-FinalizerCommand(
    $finalizer,
    [string[]]$arguments,
    [string]$workingDirectory) {
    $executable = [string]$finalizer.executable
    Push-Location $workingDirectory
    try {
        if ($executable.Contains('/') -or $executable.Contains('\')) {
            $scriptPath = Get-RepositoryPath ($executable.Replace('\', '/'))
            if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
                throw "Finalizer script does not exist: $executable"
            }
            $hostName = if ($PSVersionTable.PSEdition -eq 'Core') { 'pwsh.exe' } else { 'powershell.exe' }
            $hostPath = Join-Path $PSHOME $hostName
            if (-not (Test-Path -LiteralPath $hostPath -PathType Leaf)) {
                throw "Unable to resolve the current PowerShell host: $hostPath"
            }
            & $hostPath -NoProfile -ExecutionPolicy Bypass -File $scriptPath @arguments |
                Out-Host
        } else {
            $command = Get-Command $executable -CommandType Application -ErrorAction Stop |
                Select-Object -First 1
            & $command.Source @arguments | Out-Host
        }
        return [int]$LASTEXITCODE
    } finally {
        Pop-Location
    }
}

$schema = Read-StrictUtf8Json $schemaPath 'Work-campaign schema'
if ($schema.schemaVersion -ne 1) {
    throw "Work-campaign schema must declare schemaVersion 1."
}

$campaignPath = [IO.Path]::GetFullPath(
    $(if ([IO.Path]::IsPathRooted($Campaign)) {
        $Campaign
    } else {
        Join-Path $repoRoot $Campaign
    }))
$manifest = Read-StrictUtf8Json $campaignPath 'Campaign manifest'
$testJsonCommand = Get-Command Test-Json -ErrorAction SilentlyContinue
if ($null -ne $testJsonCommand -and $testJsonCommand.Parameters.ContainsKey('SchemaFile')) {
    try {
        $manifestText = [IO.File]::ReadAllText($campaignPath, $strictUtf8)
        $schemaValid = $manifestText | Test-Json -SchemaFile $schemaPath -ErrorAction Stop
        if (-not $schemaValid) {
            throw 'JSON Schema validation returned false.'
        }
    } catch {
        throw "Campaign manifest failed work-campaign JSON Schema validation: $($_.Exception.Message)"
    }
}
Assert-Manifest $manifest

if ($ValidateOnly) {
    Write-Host "Work campaign manifest is valid: $campaignPath"
    return
}

$campaignIsLocal = $campaignPath.StartsWith(
    $localCampaignPrefix,
    [StringComparison]::OrdinalIgnoreCase)
if ($Execute -and -not $campaignIsLocal) {
    throw "Execution requires a manifest under .local/work-campaigns/. Use the versioned example only with -ValidateOnly."
}

$branch = ((Invoke-Git @('branch', '--show-current')).Output -join '').Trim()
if ($branch -cne [string]$manifest.baseline.branch) {
    throw "Campaign branch '$($manifest.baseline.branch)' does not match actual branch '$branch'."
}

$commitCheck = Invoke-Git @('cat-file', '-e', "$($manifest.baseline.commit)^{commit}") -AllowFailure
if ($commitCheck.ExitCode -ne 0) {
    throw "Campaign baseline commit does not resolve: $($manifest.baseline.commit)"
}
$ancestorCheck = Invoke-Git @(
    'merge-base', '--is-ancestor', [string]$manifest.baseline.commit, 'HEAD') -AllowFailure
if ($ancestorCheck.ExitCode -ne 0) {
    throw "Campaign baseline is not an ancestor of HEAD: $($manifest.baseline.commit)"
}

$head = ((Invoke-Git @('rev-parse', 'HEAD')).Output -join '').Trim()
$changedPaths = @(Get-CampaignChangedPaths $manifest)
Assert-CampaignScope $manifest $changedPaths
$fingerprint = Get-ChangeFingerprint $manifest $changedPaths $head
$treeState = Get-TreeState

$statePath = Join-Path (Split-Path -Parent $campaignPath) 'state.json'
$state = if ($campaignIsLocal) {
    Read-State $statePath ([string]$manifest.campaignId)
} else {
    [ordered]@{
        schemaVersion = 1
        campaignId = [string]$manifest.campaignId
        runs = @()
    }
}

$selected = @(
    @($manifest.finalizers) | Where-Object {
        $candidate = $_
        [string]$candidate.phase -ceq $Phase -and
        @($changedPaths | Where-Object {
                Test-AnyPattern $_ @($candidate.whenChanged)
            }).Count -gt 0
    })

Write-Host "Campaign:    $($manifest.campaignId)"
Write-Host "Phase:       $Phase"
Write-Host "Mode:        $(if ($Execute) { 'execute' } else { 'plan' })"
Write-Host "Baseline:    $($manifest.baseline.commit)"
Write-Host "HEAD:        $head"
Write-Host "Tree:        $treeState"
Write-Host "Fingerprint: $fingerprint"
Write-Host "Changes:     $($changedPaths.Count) campaign-owned path(s)"

if ($selected.Count -eq 0) {
    Write-Host "No finalizers were selected for this phase and change set."
    return
}

foreach ($finalizer in $selected) {
    $id = [string]$finalizer.id
    $missingPaths = @(
        @($finalizer.requiredPaths) | Where-Object {
            $requiredPath = Get-RepositoryPath ([string]$_)
            -not (Test-Path -LiteralPath $requiredPath)
        })
    if ($missingPaths.Count -gt 0) {
        $message = "[$id] missing prerequisite path(s): $($missingPaths -join ', ')"
        if ([bool]$finalizer.required) {
            throw $message
        }
        Write-Host "SKIP $message"
        continue
    }

    $priorPass = @($state.runs | Where-Object {
            [string]$_.finalizerId -ceq $id -and
            [string]$_.phase -ceq $Phase -and
            [string]$_.fingerprint -ceq $fingerprint -and
            [string]$_.result -ceq 'PASS'
        }).Count -gt 0
    if ($priorPass -and -not $Force) {
        Write-Host "SKIP [$id] already passed for this fingerprint."
        continue
    }

    $arguments = @($finalizer.arguments | ForEach-Object {
            Expand-CampaignToken ([string]$_) $manifest $head
        })
    $workingDirectory = Get-RepositoryPath (
        Expand-CampaignToken ([string]$finalizer.workingDirectory) $manifest $head) -AllowDot
    $displayExecutable = Expand-CampaignToken ([string]$finalizer.executable) $manifest $head
    $display = Get-DisplayCommand $displayExecutable $arguments

    if ([bool]$finalizer.requiresCleanTree -and $treeState -ne 'clean') {
        if ($Execute) {
            throw "[$id] requires a clean tree."
        }
        Write-Host "BLOCKED [$id] requires a clean tree: $display"
        continue
    }

    if (-not $Execute) {
        Write-Host "PLAN [$id] $display"
        continue
    }

    Write-Host "RUN  [$id] $display"
    $exitCode = 1
    try {
        $exitCode = Invoke-FinalizerCommand $finalizer $arguments $workingDirectory
    } catch {
        Add-StateRun $state $id $Phase $fingerprint $head $treeState 'FAIL' $exitCode
        Save-State $statePath $state
        $message = "[$id] failed before reporting an accepted exit code: $($_.Exception.Message)"
        if ([bool]$finalizer.required) {
            throw $message
        }
        Write-Warning "OPTIONAL $message"
        continue
    }

    if ([int]$exitCode -notin @($finalizer.expectedExitCodes | ForEach-Object { [int]$_ })) {
        Add-StateRun $state $id $Phase $fingerprint $head $treeState 'FAIL' $exitCode
        Save-State $statePath $state
        $message = "[$id] failed with exit code $exitCode."
        if ([bool]$finalizer.required) {
            throw $message
        }
        Write-Warning "OPTIONAL $message"
        continue
    }

    Add-StateRun $state $id $Phase $fingerprint $head $treeState 'PASS' $exitCode
    Save-State $statePath $state
    Write-Host "PASS [$id]"
}

Write-Host "Work campaign phase completed. Local state is coordination only, not durable evidence."
