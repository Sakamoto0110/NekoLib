[CmdletBinding()]
param(
    [string]$PremiseId
)

$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$repoPrefix = $repoRoot.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$schemaPath = Join-Path $repoRoot "docs\schemas\premise-schema.json"
$examplePath = Join-Path $repoRoot "docs\templates\premise.example.json"
$registryPath = Join-Path $repoRoot "docs\premises"
$strictUtf8 = New-Object System.Text.UTF8Encoding -ArgumentList $false, $true
$errors = New-Object System.Collections.Generic.List[string]
$notes = New-Object System.Collections.Generic.List[string]

function Add-PremiseError([string]$message) {
    $script:errors.Add($message)
}

function Read-StrictUtf8Json([string]$path, [string]$label) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "$label does not exist: $path"
    }

    try {
        $text = [IO.File]::ReadAllText([IO.Path]::GetFullPath($path), $script:strictUtf8)
        return [pscustomobject]@{
            Text = $text
            Value = $text | ConvertFrom-Json
        }
    } catch {
        throw "$label is not valid strict UTF-8 JSON: $($_.Exception.Message)"
    }
}

function Test-SafeRelativePath([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value) -or $value -eq '.') { return $false }
    if ($value.Contains('\') -or $value.Contains("`r") -or
        $value.Contains("`n") -or $value.Contains([char]0)) {
        return $false
    }
    if ([IO.Path]::IsPathRooted($value) -or $value -match '^[A-Za-z]:') {
        return $false
    }
    if (@($value.Split('/')) -contains '..') { return $false }
    return $value.IndexOfAny([char[]]'*?[]') -lt 0
}

function Get-RepositoryPath([string]$relativePath) {
    if (-not (Test-SafeRelativePath $relativePath)) {
        throw "Unsafe repository-relative path: $relativePath"
    }
    $absolute = [IO.Path]::GetFullPath((Join-Path $script:repoRoot $relativePath))
    if (-not $absolute.StartsWith($script:repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Repository-relative path escaped the repository: $relativePath"
    }
    return $absolute
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

function Get-ChangedPathsSince([string]$commit) {
    $tracked = (Invoke-Git @(
            'diff', '--name-only', '--diff-filter=ACDMRTUXB', $commit, '--')).Output
    $untracked = (Invoke-Git @(
            'ls-files', '--others', '--exclude-standard')).Output
    return @(
        $tracked + $untracked |
            ForEach-Object { ([string]$_).Replace('\', '/') } |
            Where-Object { $_ } |
            Sort-Object -Unique)
}

function Assert-ReferencePaths($paths, [string]$label) {
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($pathValue in @($paths)) {
        $path = [string]$pathValue
        if (-not $seen.Add($path)) {
            Add-PremiseError "$label repeats '$path'."
            continue
        }
        if (-not (Test-SafeRelativePath $path)) {
            Add-PremiseError "$label contains an unsafe repository path: $path"
            continue
        }
        if (-not (Test-Path -LiteralPath (Get-RepositoryPath $path))) {
            Add-PremiseError "$label points to a missing repository path: $path"
        }
    }
}

function Get-EffectiveStatus($premise, [string[]]$changedPaths, [bool]$anchorIsUsable) {
    $declared = [string]$premise.status
    if ($declared -in @('broken', 'retired', 'superseded')) {
        return $declared
    }
    if ($declared -ceq 'draft') {
        return 'draft'
    }

    $qualifying = @($premise.contradictions | Where-Object {
            [string]$_.classification -ceq 'qualifying'
        })
    $critical = @($qualifying | Where-Object {
            [string]$_.severity -ceq 'critical'
        }).Count -gt 0
    $distinctIdentities = @($qualifying |
            ForEach-Object { [string]$_.identity } |
            Sort-Object -Unique)

    if ($critical -or
        $distinctIdentities.Count -ge [int]$premise.invalidation.distinctFailureThreshold) {
        return 'broken'
    }
    if ($qualifying.Count -gt 0) {
        return 'challenged'
    }

    if ($null -ne $premise.freshness.expiresAtUtc) {
        try {
            $expiry = [DateTimeOffset]::Parse([string]$premise.freshness.expiresAtUtc)
            if ([DateTimeOffset]::UtcNow -ge $expiry.ToUniversalTime()) {
                return 'expired'
            }
        } catch {
            Add-PremiseError "$($premise.premiseId) has an invalid freshness.expiresAtUtc value."
            return 'expired'
        }
    }

    if (-not $anchorIsUsable) {
        return 'stale'
    }
    $freshnessChanges = @($changedPaths | Where-Object {
            Test-AnyPattern $_ @($premise.freshness.invalidateWhenChanged)
        })
    if ($freshnessChanges.Count -gt 0) {
        return 'stale'
    }

    return $declared
}

function Invoke-EffectiveStatusSelfTest($example) {
    $makePremise = {
        return ($example | ConvertTo-Json -Depth 20 | ConvertFrom-Json)
    }
    $makeContradiction = {
        param([string]$identity, [string]$severity)
        return [pscustomobject]@{
            identity = $identity
            severity = $severity
            classification = 'qualifying'
        }
    }

    $active = & $makePremise
    $active.contradictions = @()
    $activeResult = Get-EffectiveStatus $active @() $true

    $challenged = & $makePremise
    $challenged.contradictions = @(& $makeContradiction 'test-one' 'ordinary')
    $challengedResult = Get-EffectiveStatus $challenged @() $true

    $broken = & $makePremise
    $broken.contradictions = @(
        & $makeContradiction 'test-one' 'ordinary'
        & $makeContradiction 'test-two' 'ordinary')
    $brokenResult = Get-EffectiveStatus $broken @() $true

    $critical = & $makePremise
    $critical.contradictions = @(& $makeContradiction 'security-check' 'critical')
    $criticalResult = Get-EffectiveStatus $critical @() $true

    $stale = & $makePremise
    $stale.contradictions = @()
    $staleResult = Get-EffectiveStatus $stale @('src/Core/Changed.cs') $true

    $received = @($activeResult, $challengedResult, $brokenResult, $criticalResult, $staleResult)
    $expected = @('active', 'challenged', 'broken', 'broken', 'stale')
    for ($index = 0; $index -lt $expected.Count; $index++) {
        if ([string]$received[$index] -cne $expected[$index]) {
            throw "Effective-status self-test $index expected '$($expected[$index])' but received '$($received[$index])'."
        }
    }
}

function Test-PremiseRecord($premise, [string]$path) {
    $id = [string]$premise.premiseId
    $fileName = [IO.Path]::GetFileNameWithoutExtension($path)
    if ($fileName -cne $id) {
        Add-PremiseError "Premise file '$path' must be named '$id.json'."
    }

    Assert-ReferencePaths @($premise.authority.references) "$id authority.references"
    Assert-ReferencePaths @($premise.basis.evidenceReferences) "$id basis.evidenceReferences"
    foreach ($contradiction in @($premise.contradictions)) {
        Assert-ReferencePaths @($contradiction.evidenceReferences) `
            "$id contradiction '$($contradiction.id)' evidenceReferences"
    }
    foreach ($transition in @($premise.statusHistory)) {
        Assert-ReferencePaths @($transition.evidenceReferences) `
            "$id statusHistory evidenceReferences"
    }

    if (@($premise.statusHistory).Count -eq 0) {
        Add-PremiseError "$id must retain at least one statusHistory entry."
    } else {
        $history = @($premise.statusHistory)
        $lastStatus = [string]$history[-1].status
        if ($lastStatus -cne [string]$premise.status) {
            Add-PremiseError "$id declared status '$($premise.status)' does not match its last statusHistory entry '$lastStatus'."
        }
    }

    if (@($premise.supersedes) -contains $id) {
        Add-PremiseError "$id cannot supersede itself."
    }

    $contradictionIds = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    $qualifyingIdentities = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($contradiction in @($premise.contradictions)) {
        if (-not $contradictionIds.Add([string]$contradiction.id)) {
            Add-PremiseError "$id repeats contradiction ID '$($contradiction.id)'."
        }
        if ([string]$contradiction.classification -ceq 'qualifying' -and
            -not $qualifyingIdentities.Add([string]$contradiction.identity)) {
            Add-PremiseError "$id repeats qualifying identity '$($contradiction.identity)'; classify repeats as duplicate."
        }
    }

    $anchor = [string]$premise.basis.validatedAtCommit
    $commitCheck = Invoke-Git @('cat-file', '-e', "$anchor^{commit}") -AllowFailure
    $ancestorCheck = if ($commitCheck.ExitCode -eq 0) {
        Invoke-Git @('merge-base', '--is-ancestor', $anchor, 'HEAD') -AllowFailure
    } else {
        [pscustomobject]@{ ExitCode = 1 }
    }
    $anchorIsUsable = $commitCheck.ExitCode -eq 0 -and $ancestorCheck.ExitCode -eq 0
    $changedPaths = if ($anchorIsUsable) { @(Get-ChangedPathsSince $anchor) } else { @() }
    $effective = Get-EffectiveStatus $premise $changedPaths $anchorIsUsable

    if ([string]$premise.status -cne $effective) {
        Add-PremiseError "$id is declared $($premise.status) but is effectively $effective; reconcile its durable status before relying on it."
    }

    Write-Host "Premise ${id}: declared=$($premise.status), effective=$effective"
}

Push-Location $repoRoot
try {
    $schemaDocument = Read-StrictUtf8Json $schemaPath 'Premise schema'
    if ($schemaDocument.Value.schemaVersion -ne 1) {
        Add-PremiseError "Premise schema must declare schemaVersion 1."
    }

    $testJsonCommand = Get-Command Test-Json -ErrorAction SilentlyContinue
    $canValidateSchema = $null -ne $testJsonCommand -and
        $testJsonCommand.Parameters.ContainsKey('SchemaFile')
    if (-not $canValidateSchema) {
        $notes.Add('This PowerShell host lacks Test-Json -SchemaFile; only operational premise checks ran.')
    }

    $exampleDocument = Read-StrictUtf8Json $examplePath 'Premise example'
    if ($canValidateSchema) {
        try {
            $valid = $exampleDocument.Text |
                Test-Json -SchemaFile $schemaPath -ErrorAction Stop
            if (-not $valid) { throw 'JSON Schema validation returned false.' }
        } catch {
            Add-PremiseError "Premise example failed JSON Schema validation: $($_.Exception.Message)"
        }
    }
    try {
        Invoke-EffectiveStatusSelfTest $exampleDocument.Value
        $notes.Add('Effective-status self-tests passed for active, challenged, broken, critical, and stale derivation.')
    } catch {
        Add-PremiseError "Premise effective-status self-test failed: $($_.Exception.Message)"
    }

    $recordPaths = @(Get-ChildItem -LiteralPath $registryPath -Filter '*.json' -File |
            Sort-Object Name |
            ForEach-Object { $_.FullName })
    if ($PremiseId) {
        $recordPaths = @($recordPaths | Where-Object {
                [IO.Path]::GetFileNameWithoutExtension($_) -ceq $PremiseId
            })
        if ($recordPaths.Count -eq 0) {
            Add-PremiseError "Premise record was not found: $PremiseId"
        }
    }

    foreach ($recordPath in $recordPaths) {
        $recordDocument = Read-StrictUtf8Json $recordPath "Premise record '$recordPath'"
        if ($canValidateSchema) {
            try {
                $valid = $recordDocument.Text |
                    Test-Json -SchemaFile $schemaPath -ErrorAction Stop
                if (-not $valid) { throw 'JSON Schema validation returned false.' }
            } catch {
                Add-PremiseError "Premise record '$recordPath' failed JSON Schema validation: $($_.Exception.Message)"
                continue
            }
        }
        Test-PremiseRecord $recordDocument.Value $recordPath
    }

    foreach ($note in $notes) {
        Write-Host "NOTE: $note"
    }
    if ($errors.Count -gt 0) {
        foreach ($verificationError in $errors) {
            Write-Error $verificationError -ErrorAction Continue
        }
        throw "Premise verification failed with $($errors.Count) error(s)."
    }

    Write-Host "Premise verification passed. Validated the schema/example and $($recordPaths.Count) shared record(s)."
} finally {
    Pop-Location
}
