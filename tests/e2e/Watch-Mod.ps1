# Runs the host's "watch" scenario against one or more mod directories, so a mod can be inspected by hand:
# it loads the mods, prints the loaded data (which is where the patches show up), attaches the packed scene to
# the tree, and stays alive so the window can be looked at.
#
# NOTE: This file is deliberately ASCII-only (see tests/README.md).
#
# Usage:
#   ./tests/e2e/Watch-Mod.ps1
#   ./tests/e2e/Watch-Mod.ps1 -ModDirectory tests/e2e/Mods/SampleMod
#   ./tests/e2e/Watch-Mod.ps1 -ModDirectory 'tests/e2e/Mods/SampleMod,tests/e2e/Mods/AlphaMod'
[CmdletBinding()]
param(
    [string]$Godot = $env:MODOT_GODOT,
    [string]$ModDirectory = 'tests\e2e\Mods\SampleMod',
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$e2eDir = $PSScriptRoot
$hostDir = Join-Path $e2eDir 'Host'
$defaultGodot = 'D:\APP\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'

if (-not $Godot) {
    $Godot = $defaultGodot
}
if (-not (Test-Path -LiteralPath $Godot)) {
    throw "Godot console executable not found: $Godot"
}

# The mod assembly and the resource pack have to exist first, exactly as they must for the suite. The pack is
# not skipped: watch attaches the packed scene, so a missing pack would fail the scenario.
& (Join-Path $e2eDir 'Update-ModAssets.ps1') -Godot $Godot -Configuration $Configuration

dotnet build (Join-Path $hostDir 'Host.csproj') -c $Configuration -v:q
if ($LASTEXITCODE -ne 0) {
    throw "Building Host failed with exit code $LASTEXITCODE"
}

$directories = @()
foreach ($relative in $ModDirectory -split ',') {
    $resolved = Resolve-Path -LiteralPath $relative.Trim() -ErrorAction Stop
    $directories += $resolved.Path
}

Write-Host "Watching: $($directories -join ', ')"
Write-Host "A window opens and stays open; close it when you are done."
& $Godot --path $hostDir -- watch @directories
