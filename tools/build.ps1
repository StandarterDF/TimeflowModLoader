<#
.SYNOPSIS
    One-shot developer workflow: fetch dependencies, build, deploy to the game.
#>
[CmdletBinding()]
param(
    [string]$GameRoot,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$SkipSetup,
    [switch]$SkipDeploy
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

if (-not $SkipSetup) {
    Write-Host '--- dependencies ---'
    & (Join-Path $PSScriptRoot 'setup.ps1')
}

Write-Host '--- build ---'
& dotnet build (Join-Path $repo 'TimeflowModLoader.sln') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

if (-not $SkipDeploy) {
    Write-Host '--- deploy ---'
    $deployArgs = @{ Configuration = $Configuration }
    if ($GameRoot) { $deployArgs.GameRoot = $GameRoot }
    & (Join-Path $PSScriptRoot 'deploy.ps1') @deployArgs
}

Write-Host ''
Write-Host 'Done. Launch Timeflow.exe, then check TimeflowModLoader\Logs\ModLoader.log.'
