[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidencePath)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$providers = @(
    foreach ($view in @([Microsoft.Win32.RegistryView]::Registry32, [Microsoft.Win32.RegistryView]::Registry64)) {
        $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::ClassesRoot, $view)
        try {
            foreach ($provider in @('Microsoft.ACE.OLEDB.12.0', 'Microsoft.ACE.OLEDB.16.0')) {
                $key = $base.OpenSubKey("$provider\CLSID")
                try {
                    [pscustomobject]@{ provider = $provider; registryView = $view.ToString(); registered = $null -ne $key; hive = 'merged user and machine ClassesRoot' }
                }
                finally { if ($key) { $key.Dispose() } }
            }
        }
        finally { $base.Dispose() }
    }
)
$localDb = Get-Command SqlLocalDB.exe -ErrorAction SilentlyContinue
$evidence = [ordered]@{
    capturedUtc = [DateTime]::UtcNow.ToString('O')
    userInteractive = [Environment]::UserInteractive
    probeProcessBitness = [IntPtr]::Size * 8
    sdks = @(& dotnet --list-sdks)
    windowsDesktopRuntimes = @(& dotnet --list-runtimes | Where-Object { $_ -like 'Microsoft.WindowsDesktop.App *' })
    aceRegistration = $providers
    sqlServerServices = @(Get-Service -Name 'MSSQL*' -ErrorAction SilentlyContinue | Select-Object Name, Status)
    localDbCommandAvailable = $null -ne $localDb
    boundary = 'Registration and runtime inventory only; no database or external API connection was attempted.'
}
$absolute = [IO.Path]::GetFullPath($EvidencePath)
New-Item -ItemType Directory -Force -Path (Split-Path $absolute -Parent) | Out-Null
$evidence | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $absolute -Encoding utf8
$evidence | ConvertTo-Json -Depth 6
