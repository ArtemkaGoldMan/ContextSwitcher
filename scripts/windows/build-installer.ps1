<#
.SYNOPSIS
    Builds the Windows installer: the app published for win-x64 and packed with Velopack.

.DESCRIPTION
    Publishes the app self-contained, so that nothing needs installing first, takes out the debugging
    symbols of the native libraries, and packs the result with Velopack's vpk (pinned in
    .config/dotnet-tools.json). What comes out, in dist/windows:

      ContextSwitcher-win-Setup.exe        what a person downloads and runs
      ContextSwitcher-<version>-full.nupkg  what the updater installs from
      releases.win.json                     the list of releases the updater reads
      ContextSwitcher-win-Portable.zip, RELEASES, assets.win.json

    The version is <Version> in ContextSwitcher.App.csproj. Not signed: Windows asks once before the
    first run (docs/windows-plan.md).

.PARAMETER OutputDirectory
    Where the packed release goes, relative to the repository. Emptied first.

.PARAMETER Vpk
    The path of a vpk to use instead of the one the tool manifest restores - for a machine whose .NET SDK
    is not the one global.json pins.
#>
[CmdletBinding()]
param(
    [string]$OutputDirectory = 'dist/windows',
    [string]$Vpk
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$project = Join-Path $root 'src/ContextSwitcher.App/ContextSwitcher.App.csproj'
$framework = 'net10.0-windows10.0.19041.0'
$runtime = 'win-x64'
$publishDirectory = Join-Path $root "artifacts/publish/$runtime"
$output = Join-Path $root $OutputDirectory

$version = (Select-Xml -Path $project -XPath '//Version').Node.InnerText
if (-not $version) {
    throw "No <Version> in $project."
}

function Invoke-Checked {
    param([string]$Description, [scriptblock]$Command)
    Write-Output "== $Description"
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

Remove-Item $publishDirectory, $output -Recurse -Force -ErrorAction SilentlyContinue

Invoke-Checked "Publish $version for $runtime" {
    dotnet publish $project --configuration Release --framework $framework --runtime $runtime --self-contained `
        --output $publishDirectory --nologo -p:DebugType=None -p:DebugSymbols=false
}

# The symbols of SkiaSharp and HarfBuzzSharp come with their NuGet packages, not from the build, and
# are a hundred megabytes nobody installing the app needs. vpk leaves .pdb files out of the package as
# well; this keeps them out of what it has to read.
Get-ChildItem $publishDirectory -Recurse -Filter '*.pdb' | Remove-Item -Force

if ($Vpk) {
    $vpkCommand = { & $Vpk @args }
}
else {
    Invoke-Checked 'Restore vpk' { dotnet tool restore --tool-manifest (Join-Path $root '.config/dotnet-tools.json') }
    $vpkCommand = { dotnet vpk @args }
}

Invoke-Checked "Pack $version" {
    # No shortcut on the desktop: this is an app that lives in the tray. No deltas: each release holds
    # everything it needs, so an update never depends on the releases before it still being there.
    & $vpkCommand pack --packId ContextSwitcher --packVersion $version --packDir $publishDirectory `
        --mainExe ContextSwitcher.exe --packTitle 'Context Switcher' --packAuthors 'Artem Omelchenko' `
        --runtime $runtime --shortcuts StartMenuRoot --delta none --outputDir $output
}

$expected = @('ContextSwitcher-win-Setup.exe', "ContextSwitcher-$version-full.nupkg", 'releases.win.json')
foreach ($name in $expected) {
    if (-not (Test-Path (Join-Path $output $name))) {
        throw "vpk did not make $name in $output."
    }
}

Write-Output ''
Write-Output "Built $version in $output :"
Get-ChildItem $output -File | ForEach-Object { Write-Output ('  {0,8:N1} MB  {1}' -f ($_.Length / 1MB), $_.Name) }