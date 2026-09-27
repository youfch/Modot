# Verifies the packaged NuGet package the way a consumer sees it: restores Modot from the local drop folder
# and runs the whole e2e suite inside a Godot project that references the package instead of the source
# projects.
#
# Why this exists: every other test references src/Modot as a project, so nothing exercised the artifact that
# actually ships. This is the only check that the package restores, that its dependency graph resolves, and
# that its assembly behaves the same as the working tree.
#
# NOTE: This file is deliberately ASCII-only (see tests/README.md).
#
# Usage:
#   ./tests/e2e/Test-PackageConsumption.ps1
#   ./tests/e2e/Test-PackageConsumption.ps1 -Version 3.0.1 -DropDirectory 'D:/GNuget'
[CmdletBinding()]
param(
    [string]$DropDirectory = $(if ($env:MODOT_NUGET_DROP) { $env:MODOT_NUGET_DROP } else { 'D:/GNuget' }),
    [string]$Version = '3.0.1',
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$e2eDir = $PSScriptRoot
$projectPath = Join-Path $e2eDir 'PackageConsumer\Host.csproj'
$feed = 'https://api.nuget.org/v3/index.json'

if (-not (Test-Path -LiteralPath $DropDirectory)) {
    throw "NuGet drop folder not found: $DropDirectory"
}
$package = Join-Path $DropDirectory "Modot.$Version.nupkg"
if (-not (Test-Path -LiteralPath $package)) {
    throw "Package not found: $package - run scripts/pack-local.ps1 first"
}

Write-Host "Package: $package"
Write-Host "Feeds:   $DropDirectory (+ $feed)"

# The sources go into a generated config rather than onto the command line. Passing the feed URL as an
# argument does not survive native-argument handling here: dotnet receives it with its slashes rewritten,
# treats it as a local folder, and reports a missing source under the project directory - a confusing way to
# fail. The config lives in TEMP, because a checked-in one would have to hardcode the machine's drop path.
$configPath = Join-Path $env:TEMP 'modot-package-consumer-nuget.config'
$configXml = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-drop" value="$($DropDirectory -replace '\\', '/')" />
    <add key="nuget.org" value="$feed" />
  </packageSources>
</configuration>
"@
[System.IO.File]::WriteAllText($configPath, $configXml, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Config:  $configPath"

# Two sources on purpose: the drop folder holds Modot and its vendored dependencies, while Godot.NET.Sdk and
# the rest come from nuget.org.
dotnet restore $projectPath --configfile $configPath -p:ModotVersion=$Version -v:q
if ($LASTEXITCODE -ne 0) {
    throw "Restore failed with exit code $LASTEXITCODE"
}

# Prove the restore really resolved the packaged Modot and pulled its dependencies along, rather than quietly
# resolving something else.
# Not at obj\project.assets.json: Godot.NET.Sdk redirects the intermediate output under .godot\mono\temp, so
# the file is located rather than assumed. (That same redirection is why the harness has to build Debug.)
$assetsFile = Get-ChildItem (Join-Path $e2eDir 'PackageConsumer') -Recurse -Filter 'project.assets.json' -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($null -eq $assetsFile) {
    throw "No project.assets.json under PackageConsumer - the restore did not produce one"
}
Write-Host "Assets:  $($assetsFile.FullName)"
$assets = [System.IO.File]::ReadAllText($assetsFile.FullName, [System.Text.Encoding]::UTF8)
foreach ($expected in @("Modot/$Version", 'Modot.GDLogger/2.0.0', 'Modot.GDSerializer/4.0.0', 'GodotSharp/4.7.2')) {
    if ($assets -notmatch [regex]::Escape($expected)) {
        throw "Restored assets do not contain $expected - the resolved graph is not what was packed"
    }
    Write-Host "Resolved: $expected"
}
# The package's own dependency list is read from the .nupkg rather than from the consumer's resolved graph.
# A Godot.NET.Sdk project references GodotSharpEditor itself in the Debug configuration, so finding that name
# in the assets says nothing about the package - an earlier version of this check did exactly that and
# reported a healthy package as broken. Reading the nuspec is what actually verifies the packaging ran
# Release, which is what keeps GodotSharpEditor out of the published dependencies.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($package)
try {
    $nuspecEntry = $zip.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1
    $reader = New-Object System.IO.StreamReader($nuspecEntry.Open())
    $nuspec = $reader.ReadToEnd()
    $reader.Close()
}
finally {
    $zip.Dispose()
}
if ($nuspec -match 'GodotSharpEditor') {
    throw "The package declares GodotSharpEditor - it was packed in the wrong configuration"
}
if ($nuspec -notmatch 'GodotSharp') {
    throw "The package does not declare GodotSharp - its dependency list is not what a consumer needs"
}
Write-Host "Nuspec:  declares GodotSharp and not GodotSharpEditor (packed Release)"

# Run the ordinary suite, but inside the consumer project.
& (Join-Path $e2eDir 'Invoke-E2E.ps1') -HostProjectDirectory 'PackageConsumer' -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "The suite failed against the packaged Modot (exit code $LASTEXITCODE)"
}

Write-Host ""
Write-Host "Modot $Version passed the same scenarios from the package as it does from source."