<#
.SYNOPSIS
    Deploys the built ModLoader (and its dependencies) into the game folder.
.DESCRIPTION
    Copies:
      vendor\doorstop\winhttp.dll                 -> <GameRoot>\winhttp.dll
      doorstop_config.ini                         -> <GameRoot>\doorstop_config.ini
      src\Timeflow.ModLoader\bin\<Cfg>\*.dll      -> <GameRoot>\TimeflowModLoader\Core\
      vendor\harmonyx\0Harmony.dll                -> <GameRoot>\TimeflowModLoader\Core\
    and creates Mods\, TimeflowModLoader\Config\, TimeflowModLoader\Logs\.

    Use -Uninstall to remove the injected files (your Mods and Config are kept).
#>
[CmdletBinding()]
param(
    [string]$GameRoot,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $GameRoot) { $GameRoot = Join-Path $repo '..\Timeflow' }
$GameRoot = [IO.Path]::GetFullPath($GameRoot)

if (-not (Test-Path (Join-Path $GameRoot 'Timeflow.exe'))) {
    throw "Timeflow.exe was not found in '$GameRoot'. Pass -GameRoot <path>."
}

$coreDir = Join-Path $GameRoot 'TimeflowModLoader\Core'
$dataDir = Join-Path $GameRoot 'TimeflowModLoader'

if ($Uninstall) {
    foreach ($f in @('winhttp.dll', 'doorstop_config.ini', 'doorstop_config.ini.bak')) {
        $p = Join-Path $GameRoot $f
        if (Test-Path $p) { Remove-Item $p -Force; Write-Host "removed $f" }
    }
    if (Test-Path $coreDir) { Remove-Item $coreDir -Recurse -Force; Write-Host 'removed TimeflowModLoader\Core' }
    Write-Host 'ModLoader uninstalled. Mods\ and TimeflowModLoader\Config\ were kept.'
    return
}

if (-not (Test-Path (Join-Path $repo 'vendor\doorstop\winhttp.dll'))) {
    throw "Dependencies are missing. Run tools\setup.ps1 first."
}

$loaderDll = Join-Path $repo "src\Timeflow.ModLoader\bin\$Configuration\Timeflow.ModLoader.dll"
if (-not (Test-Path $loaderDll)) {
    throw "Loader not built. Run tools\build.ps1 (or: dotnet build -c $Configuration)."
}

Write-Host "Deploying to $GameRoot"
New-Item -ItemType Directory -Force -Path $coreDir, (Join-Path $dataDir 'Config'), (Join-Path $dataDir 'Logs'), (Join-Path $GameRoot 'Mods') | Out-Null

# Doorstop native injector (keep a backup of any pre-existing DLL).
$winhttp = Join-Path $GameRoot 'winhttp.dll'
if ((Test-Path $winhttp) -and -not (Test-Path "$winhttp.modloader_backup")) {
    Copy-Item $winhttp "$winhttp.modloader_backup"
}
Copy-Item (Join-Path $repo 'vendor\doorstop\winhttp.dll') $winhttp -Force
Copy-Item (Join-Path $repo 'doorstop_config.ini') (Join-Path $GameRoot 'doorstop_config.ini') -Force

# Managed loader + Harmony runtime (0Harmony.dll and its MonoMod/Cecil dependencies).
Copy-Item $loaderDll (Join-Path $coreDir 'Timeflow.ModLoader.dll') -Force
Get-ChildItem (Join-Path $repo 'vendor\harmonyx') -Filter *.dll |
    ForEach-Object { Copy-Item $_.FullName (Join-Path $coreDir $_.Name) -Force }

# pdb for nicer stack traces, when present.
$pdb = Join-Path $repo "src\Timeflow.ModLoader\bin\$Configuration\Timeflow.ModLoader.pdb"
if (Test-Path $pdb) { Copy-Item $pdb (Join-Path $coreDir 'Timeflow.ModLoader.pdb') -Force }

Write-Host 'Deployed:'
Write-Host "  $winhttp"
Write-Host "  $(Join-Path $GameRoot 'doorstop_config.ini')"
Write-Host "  $(Join-Path $coreDir 'Timeflow.ModLoader.dll')"
Write-Host "  $(Join-Path $coreDir '0Harmony.dll')"
Write-Host "  $(Join-Path $GameRoot 'Mods')  (drop mods here)"
