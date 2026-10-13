<#
.SYNOPSIS
    Downloads the third-party dependencies (UnityDoorstop, Harmony) into vendor/.
.DESCRIPTION
    Idempotent: downloads only when the target file is missing (use -Force to refresh).
    Everything stays inside the repository (vendor/); nothing global is installed.

    Harmony is sourced from the BepInEx 5 x64 release. BepInEx repackages HarmonyX together
    with Unity-compatible MonoMod.Cecil/Utils/RuntimeDetour builds. The raw HarmonyX NuGet
    package targets a newer MonoMod that is not compatible with Unity's Mono runtime.
#>
[CmdletBinding()]
param(
    [string]$DoorstopVersion = '4.5.0',
    [string]$BepInExVersion   = '5.4.23.5',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$vendor = Join-Path $root 'vendor'
$doorstopDir = Join-Path $vendor 'doorstop'
$harmonyDir  = Join-Path $vendor 'harmonyx'
New-Item -ItemType Directory -Force -Path $doorstopDir, $harmonyDir | Out-Null

function Get-File($url, $dest) {
    if ((Test-Path $dest) -and -not $Force) {
        Write-Host "  [skip] $([IO.Path]::GetFileName($dest)) already present"
        return
    }
    Write-Host "  [get ] $url"
    Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing
}

# --- UnityDoorstop (winhttp.dll proxy that injects our managed entrypoint) ---
$doorstopZip = Join-Path $vendor "doorstop_win_$DoorstopVersion.zip"
Get-File "https://github.com/NeighTools/UnityDoorstop/releases/download/v$DoorstopVersion/doorstop_win_release_$DoorstopVersion.zip" $doorstopZip
$extract = Join-Path $vendor 'doorstop\_extract'
if (Test-Path $extract) { Remove-Item -Recurse -Force $extract }
Expand-Archive -Path $doorstopZip -DestinationPath $extract -Force
Get-ChildItem -Path $extract -Recurse -Filter 'winhttp.dll' | Select-Object -First 1 |
    ForEach-Object { Copy-Item $_.FullName (Join-Path $doorstopDir 'winhttp.dll') -Force }
if (-not (Test-Path (Join-Path $doorstopDir 'winhttp.dll'))) {
    throw "winhttp.dll was not found inside $doorstopZip"
}

# --- Harmony (0Harmony.dll) + its runtime dependencies ---
$bepZip = Join-Path $vendor "BepInEx_win_x64_$BepInExVersion.zip"
Get-File "https://github.com/BepInEx/BepInEx/releases/download/v$BepInExVersion/BepInEx_win_x64_$BepInExVersion.zip" $bepZip
$bep = Join-Path $vendor 'bepinex\_extract'
if (Test-Path $bep) { Remove-Item -Recurse -Force $bep }
Expand-Archive -Path $bepZip -DestinationPath $bep -Force
$core = Join-Path $bep 'BepInEx\core'
foreach ($name in @('0Harmony.dll', 'Mono.Cecil.dll', 'MonoMod.Utils.dll', 'MonoMod.RuntimeDetour.dll')) {
    $src = Join-Path $core $name
    if (-not (Test-Path $src)) { throw "$name was not found inside $bepZip" }
    Copy-Item $src (Join-Path $harmonyDir $name) -Force
}

Write-Host ''
Write-Host 'Dependencies ready:'
Write-Host "  Doorstop : $(Join-Path $doorstopDir 'winhttp.dll')"
Write-Host "  Harmony  : $harmonyDir"
Get-ChildItem $harmonyDir -Filter *.dll | ForEach-Object { Write-Host "             $($_.Name)" }
