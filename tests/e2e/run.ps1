# Modot end-to-end runner: builds the host and the mod, assembles the mod directory,
# then runs the Godot host headless once per scenario and asserts the exit codes.
#
# NOTE: This file is deliberately ASCII-only. The repository mandates UTF-8 without BOM,
# and Windows PowerShell 5.1 misreads non-ASCII characters in BOM-less .ps1 files as ANSI,
# which breaks the parser. Keep it ASCII or it will not run.
#
# The engine is located through the MODOT_GODOT environment variable, falling back to the
# default install path below. Override for another machine or engine version.
#
# Usage:
#   ./tests/e2e/run.ps1
#   ./tests/e2e/run.ps1 -Scenario alpha
#   ./tests/e2e/run.ps1 -Godot 'D:\path\to\Godot_..._console.exe'
[CmdletBinding()]
param(
    [string]$Godot = $env:MODOT_GODOT,
    [string]$Configuration = 'Debug',
    [string]$Scenario = ''
)

$ErrorActionPreference = 'Stop'

$defaultGodot = 'D:\APP\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
if (-not $Godot) {
    $Godot = $defaultGodot
}
if (-not (Test-Path -LiteralPath $Godot)) {
    throw "Godot console executable not found: $Godot"
}

# $PSScriptRoot is tests/e2e
$e2eDir = $PSScriptRoot
$hostDir = Join-Path $e2eDir 'Host'
$modDir = Join-Path $e2eDir 'Mods\AlphaMod'
$fixturesDir = Join-Path $e2eDir 'fixtures'

Write-Host "Godot: $Godot"
Write-Host "Configuration: $Configuration"

# Build the mod assembly and refresh the copy Modot loads. The pack is deliberately left as committed
# here, so a test run does not rewrite it; run assemble.ps1 on its own after editing assets.
& (Join-Path $PSScriptRoot 'assemble.ps1') -Godot $Godot -Configuration $Configuration -SkipPack

dotnet build (Join-Path $hostDir 'Host.csproj') -c $Configuration -v:q
if ($LASTEXITCODE -ne 0) {
    throw "Building Host failed with exit code $LASTEXITCODE"
}

$scenarios = @(
    @{ Name = 'alpha'; Dirs = @($modDir) },
    @{ Name = 'order'; Dirs = @((Join-Path $fixturesDir 'after-a'), (Join-Path $fixturesDir 'after-b')) },
    @{ Name = 'duplicate'; Dirs = @((Join-Path $fixturesDir 'duplicate-a'), (Join-Path $fixturesDir 'duplicate-b')) },
    @{ Name = 'missing-dep'; Dirs = @((Join-Path $fixturesDir 'missing-dep')) },
    @{ Name = 'patches'; Dirs = @((Join-Path $fixturesDir 'patches')) },
    @{ Name = 'pack'; Dirs = @($modDir) },
    @{ Name = 'cycle'; Dirs = @((Join-Path $fixturesDir 'cycle-a'), (Join-Path $fixturesDir 'cycle-b')) },
    @{ Name = 'incompatible'; Dirs = @((Join-Path $fixturesDir 'incompatible-a'), (Join-Path $fixturesDir 'incompatible-b')) }
)

if ($Scenario) {
    $scenarios = $scenarios | Where-Object { $_.Name -eq $Scenario }
    if (-not $scenarios) {
        throw "Unknown scenario: $Scenario"
    }
}

$failed = 0
foreach ($item in $scenarios) {
    Write-Host ""
    Write-Host "=== scenario: $($item.Name) ==="
    & $Godot --headless --path $hostDir -- $item.Name @($item.Dirs)
    $code = $LASTEXITCODE
    if ($code -ne 0) {
        $failed += 1
        Write-Host "SCENARIO FAILED: $($item.Name) (exit $code)"
    }
}

Write-Host ""
if ($failed -gt 0) {
    throw "$failed scenario(s) failed"
}
Write-Host "All scenarios passed."
