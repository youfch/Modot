# Modot e2e assembly refresh: rebuilds the mod assembly and re-exports its resource pack, so that what
# Modot actually loads (Assemblies/AlphaMod.dll and Resources/assets.pck) matches the current sources.
#
# Use this before running the Host from the editor, because the editor builds the C# solution into
# .godot/mono/temp/bin/ but does NOT refresh Assemblies/ - Modot reads that copy, so without this step
# a code change would be loaded stale. run.ps1 calls this same script with -SkipPack.
#
# NOTE: This file is deliberately ASCII-only. The repository mandates UTF-8 without BOM, and Windows
# PowerShell 5.1 misreads non-ASCII characters in BOM-less .ps1 files as ANSI, which breaks the parser.
#
# Usage:
#   ./tests/e2e/assemble.ps1                    # code + pack
#   ./tests/e2e/assemble.ps1 -SkipPack          # code only
[CmdletBinding()]
param(
    [string]$Godot = $env:MODOT_GODOT,
    [string]$Configuration = 'Debug',
    [switch]$SkipPack
)

$ErrorActionPreference = 'Stop'

$defaultGodot = 'D:\APP\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
if (-not $Godot) {
    $Godot = $defaultGodot
}

# $PSScriptRoot is tests/e2e
$modDir = Join-Path $PSScriptRoot 'Mods\AlphaMod'

Write-Host "Building the mod assembly"
dotnet build (Join-Path $modDir 'AlphaMod.csproj') -c $Configuration -v:q
if ($LASTEXITCODE -ne 0) {
    throw "Building AlphaMod failed with exit code $LASTEXITCODE"
}

# Only the mod's own assembly may be copied here: Mod.LoadAssemblies loads every *.dll under
# Assemblies/ into the same load context as Modot, so copying the whole build output would collide
# with the already loaded Modot/GDSerializer/GDLogger/GodotSharp.
$assemblies = Join-Path $modDir 'Assemblies'
New-Item -ItemType Directory -Path $assemblies -Force | Out-Null
Get-ChildItem -LiteralPath $assemblies -File -ErrorAction SilentlyContinue | Remove-Item -Force
Copy-Item (Join-Path $modDir ".godot\mono\temp\bin\$Configuration\AlphaMod.dll") (Join-Path $assemblies 'AlphaMod.dll') -Force

if (-not $SkipPack) {
    if (-not (Test-Path -LiteralPath $Godot)) {
        throw "Godot console executable not found: $Godot"
    }

    $resources = Join-Path $modDir 'Resources'
    New-Item -ItemType Directory -Path $resources -Force | Out-Null

    Write-Host "Exporting the resource pack"
    # --export-pack takes an existing preset and exports DATA only; the preset's platform is incidental.
    # "PCK" is not a valid platform value - Godot rejects the whole preset with it.
    & $Godot --headless --path $modDir --export-pack assets (Join-Path $resources 'assets.pck') | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Exporting the resource pack failed with exit code $LASTEXITCODE"
    }
}

Write-Host ""
Write-Host "Assembled:"
Get-ChildItem -LiteralPath $assemblies -File -ErrorAction SilentlyContinue |
    ForEach-Object { Write-Host ("  Assemblies\" + $_.Name + "  " + $_.LastWriteTime) }
if (-not $SkipPack) {
    Get-ChildItem -LiteralPath (Join-Path $modDir 'Resources') -File -ErrorAction SilentlyContinue |
        ForEach-Object { Write-Host ("  Resources\" + $_.Name + "  " + $_.LastWriteTime) }
}
