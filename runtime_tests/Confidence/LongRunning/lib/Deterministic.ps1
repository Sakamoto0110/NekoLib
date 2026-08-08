<#
    Deterministic arithmetic for schedule generation.

    Two values have to be reproducible: the random sequence that places faults,
    and the hash that proves two runs planned the same thing. Neither may depend
    on the host.

    That rules out the obvious tools. Get-Random seeds a .NET Random whose
    algorithm differs between .NET Framework and modern .NET, so the same seed
    would produce different schedules under Windows PowerShell and PowerShell 7.
    GetHashCode is randomised per process. Both are written out here instead.

    BigInteger with an explicit 64-bit mask is used rather than [uint64] because
    PowerShell promotes an overflowing unsigned multiply to [double] instead of
    wrapping, which silently destroys the low bits these algorithms depend on.
#>

Set-StrictMode -Version 2.0

$script:Mask64 = [System.Numerics.BigInteger]::Pow(2, 64) - 1

function ConvertTo-UInt64Wrapped {
    param([Parameter(Mandatory = $true)] [System.Numerics.BigInteger] $Value)
    return ($Value -band $script:Mask64)
}

<#
    SplitMix64. Small, well distributed, and short enough to be obviously the
    same algorithm in PowerShell and in the C# copy the scenarios carry.
#>
function New-DeterministicRandom {
    param([Parameter(Mandatory = $true)][int] $Seed)

    $state = ConvertTo-UInt64Wrapped ([System.Numerics.BigInteger]::Abs([System.Numerics.BigInteger] $Seed))
    return [pscustomobject]@{ State = $state }
}

function Get-NextUInt64 {
    param([Parameter(Mandatory = $true)] $Random)

    $golden = [System.Numerics.BigInteger]::Parse('11400714819323198485')
    $mixA   = [System.Numerics.BigInteger]::Parse('13787848793156543929')
    $mixB   = [System.Numerics.BigInteger]::Parse('10723151780598845931')

    $Random.State = ConvertTo-UInt64Wrapped ($Random.State + $golden)
    $z = $Random.State

    $z = ConvertTo-UInt64Wrapped (($z -bxor ($z -shr 30)) * $mixA)
    $z = ConvertTo-UInt64Wrapped (($z -bxor ($z -shr 27)) * $mixB)
    $z = ConvertTo-UInt64Wrapped ($z -bxor ($z -shr 31))

    return $z
}

<#
    A value in [0, 1), built from the top 53 bits so it is exactly
    representable as a double and cannot round to 1.
#>
function Get-NextDouble {
    param([Parameter(Mandatory = $true)] $Random)

    $value = Get-NextUInt64 -Random $Random
    $fiftyThree = [double] ($value -shr 11)
    return $fiftyThree / 9007199254740992.0
}

<#
    FNV-1a over UTF-16 code units, matching the scenarios' own implementation so
    a hash printed by the orchestrator and one printed by a worker mean the same
    thing.
#>
function Get-Fnv1a64 {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string] $Text)

    $hash = [System.Numerics.BigInteger]::Parse('14695981039346656037')
    $prime = [System.Numerics.BigInteger]::Parse('1099511628211')

    foreach ($char in $Text.ToCharArray()) {
        $hash = ConvertTo-UInt64Wrapped ($hash -bxor [System.Numerics.BigInteger] [int] $char)
        $hash = ConvertTo-UInt64Wrapped ($hash * $prime)
    }

    # Cast down before formatting. BigInteger's hex formatter prefixes a zero
    # whenever the top bit is set, to keep the value unsigned, which would make
    # this print seventeen digits where the scenarios' ulong version prints
    # sixteen - the same number, formatted differently, which is exactly the
    # kind of difference that makes two hashes look unequal.
    return 'fnv1a64:' + ([uint64] $hash).ToString('x16')
}

function Get-DeterministicShuffle {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]] $Items,
        [Parameter(Mandatory = $true)] $Random
    )

    $copy = @($Items)
    for ($i = $copy.Count - 1; $i -gt 0; $i--) {
        $j = [int]([Math]::Floor((Get-NextDouble -Random $Random) * ($i + 1)))
        if ($j -gt $i) { $j = $i }

        $swap = $copy[$i]
        $copy[$i] = $copy[$j]
        $copy[$j] = $swap
    }

    return $copy
}
