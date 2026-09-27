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
#   ./tests/e2e/Invoke-E2E.ps1
#   ./tests/e2e/Invoke-E2E.ps1 -Scenario alpha
#   ./tests/e2e/Invoke-E2E.ps1 -Godot 'D:\path\to\Godot_..._console.exe'
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
$loadOrderDir = Join-Path $fixturesDir 'load-order'
$failuresDir = Join-Path $fixturesDir 'failures'
$invalidDir = Join-Path $fixturesDir 'invalid'
$crossPatchDir = Join-Path $fixturesDir 'cross-patch'

Write-Host "Godot: $Godot"
Write-Host "Configuration: $Configuration"

# Build the mod assembly, rebuild its resource pack, and refresh the copy Modot loads. The pack is not
# committed (see .gitignore), so it is rebuilt here on every run - which also keeps the export pipeline
# itself under test rather than only the artifact it produces.
& (Join-Path $PSScriptRoot 'Update-ModAssets.ps1') -Godot $Godot -Configuration $Configuration

dotnet build (Join-Path $hostDir 'Host.csproj') -c $Configuration -v:q
if ($LASTEXITCODE -ne 0) {
    throw "Building Host failed with exit code $LASTEXITCODE"
}

$scenarios = @(
    @{ Name = 'alpha'; Dirs = @($modDir) },
    @{ Name = 'order'; Dirs = @((Join-Path $loadOrderDir 'after-b'), (Join-Path $loadOrderDir 'after-a')) },
    # Input order is deliberately the reverse of what the declaration asks for, so a no-op sort cannot pass.
    @{ Name = 'order-before'; Dirs = @((Join-Path $loadOrderDir 'order-a'), (Join-Path $loadOrderDir 'order-b')) },
    @{ Name = 'duplicate'; Dirs = @((Join-Path $failuresDir 'duplicate-a'), (Join-Path $failuresDir 'duplicate-b')) },
    @{ Name = 'missing-dep'; Dirs = @((Join-Path $failuresDir 'missing-dep')) },
    @{ Name = 'patches'; Dirs = @((Join-Path $fixturesDir 'patches')) },
    @{ Name = 'pack'; Dirs = @($modDir) },
    @{ Name = 'cycle'; Dirs = @((Join-Path $failuresDir 'cycle-a'), (Join-Path $failuresDir 'cycle-b')) },
    @{ Name = 'incompatible'; Dirs = @((Join-Path $failuresDir 'incompatible-a'), (Join-Path $failuresDir 'incompatible-b')) },
    # This fixture cannot load by design, so a nonzero host exit code is the pass condition here.
    @{ Name = 'invalid-root'; Dirs = @((Join-Path $invalidDir 'invalid-root')); ExpectFailure = $true },
    # One mod's patch reaches another's data: LoadMods applies every patch to every root loaded so far.
    @{ Name = 'cross-patch'; Dirs = @((Join-Path $crossPatchDir 'base'), (Join-Path $crossPatchDir 'overlay')) },
    # executeAssemblies: false must still apply patches while not running [ModStartup].
    @{ Name = 'no-assemblies'; Dirs = @($modDir) },
    # LoadMod ignores dependencies and load order, so a missing dependency must not stop it.
    @{ Name = 'single-load'; Dirs = @((Join-Path $failuresDir 'missing-dep')) },
    # Neither of these can finish loading: a pack that is not a pack, and a patch that is not a patch.
    @{ Name = 'broken-pack'; Dirs = @((Join-Path $failuresDir 'broken-pack')); ExpectFailure = $true },
    @{ Name = 'bad-patch'; Dirs = @((Join-Path $failuresDir 'bad-patch')); ExpectFailure = $true },
    @{ Name = 'bad-patch-type'; Dirs = @((Join-Path $failuresDir 'bad-patch-type')); ExpectFailure = $true },
    # The same mod loaded twice in one process: measures what the second load does to the first load's data.
    @{ Name = 'reload'; Dirs = @($modDir) }
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

    # A scenario flagged ExpectFailure passes only when the host actually failed: that is how the suite
    # proves the host surfaces a load error instead of swallowing it and reporting success.
    $expectFailure = $item.ContainsKey('ExpectFailure') -and $item.ExpectFailure
    $passes = if ($expectFailure) { $code -ne 0 } else { $code -eq 0 }
    if (-not $passes) {
        $failed += 1
        $wanted = if ($expectFailure) { 'nonzero' } else { '0' }
        Write-Host "SCENARIO FAILED: $($item.Name) (exit $code, expected $wanted)"
    }
}

Write-Host ""
if ($failed -gt 0) {
    throw "$failed scenario(s) failed"
}
Write-Host "All scenarios passed."
