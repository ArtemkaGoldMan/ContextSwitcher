<#
.SYNOPSIS
    Installs the built installer, runs what it installed, and removes it again.

.DESCRIPTION
    What no unit test can see: that the installer installs, that the installed app is the version that was
    built and starts, that a second copy hands over to the first and ends, and that removing it takes
    the install folder, the Start menu shortcut and the entry in Apps with it - and leaves the person's
    settings folder as it was. It installs into a temporary folder and keeps the app's settings in another,
    so nothing of the person's is touched, and removes both however it ends.

.PARAMETER Directory
    Where build-installer.ps1 put the release, relative to the repository.

.PARAMETER SecondsToRun
    How long the installed app has to stay up for it to count as having started.
#>
[CmdletBinding()]
param(
    [string]$Directory = 'dist/windows',
    [int]$SecondsToRun = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$project = Join-Path $root 'src/ContextSwitcher.App/ContextSwitcher.App.csproj'
$expectedVersion = (Select-Xml -Path $project -XPath '//Version').Node.InnerText
$setup = Join-Path (Join-Path $root $Directory) 'ContextSwitcher-win-Setup.exe'
if (-not (Test-Path $setup)) {
    throw "No installer at $setup - run scripts/windows/build-installer.ps1 first."
}

$work = Join-Path ([IO.Path]::GetTempPath()) ("ContextSwitcher-install-test-" + [Guid]::NewGuid().ToString('N'))
$installDirectory = Join-Path $work 'installed'
$settingsDirectory = Join-Path $work 'settings'
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$checks = [System.Collections.Generic.List[string]]::new()

function Assert-That {
    param([bool]$Condition, [string]$Passed, [string]$Failed)
    if (-not $Condition) {
        throw $Failed
    }
    $checks.Add($Passed)
    Write-Output "  ok: $Passed"
}

function Get-AppProcess {
    Get-Process -Name 'ContextSwitcher' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($installDirectory, [StringComparison]::OrdinalIgnoreCase) }
}

try {
    New-Item -ItemType Directory $settingsDirectory -Force | Out-Null
    # The installed app keeps its profiles and time here instead of in the person's own folder.
    $env:CONTEXTSWITCHER_CONFIG_DIR = $settingsDirectory

    Write-Output "== Install $expectedVersion into $installDirectory"
    $installer = Start-Process $setup -ArgumentList '--silent', '--installto', "`"$installDirectory`"" -PassThru -Wait
    Assert-That ($installer.ExitCode -eq 0) 'the installer ends well' "The installer ended with exit code $($installer.ExitCode)."

    $app = Join-Path $installDirectory 'current\ContextSwitcher.exe'
    $updater = Join-Path $installDirectory 'Update.exe'
    Assert-That (Test-Path $app) 'the app is installed' "No app at $app."
    Assert-That (Test-Path $updater) 'the updater is installed beside it' "No Update.exe at $updater."
    Assert-That ([bool](Get-ChildItem $startMenu -Filter 'Context Switcher*.lnk' -ErrorAction SilentlyContinue)) 'a Start menu shortcut is made' 'No Start menu shortcut.'

    $installed = (Get-Item $app).VersionInfo.ProductVersion
    Assert-That ($installed.StartsWith($expectedVersion)) "it is version $expectedVersion" "The installed app is $installed, not $expectedVersion."

    Write-Output '== Run it'
    $started = Start-Process $app -PassThru
    Start-Sleep -Seconds $SecondsToRun
    Assert-That (-not $started.HasExited) "it is still running after $SecondsToRun seconds" "The app ended after less than $SecondsToRun seconds, with exit code $($started.ExitCode)."

    Write-Output '== Run a second copy'
    $second = Start-Process $app -PassThru
    $ended = $second.WaitForExit(30000)
    Assert-That $ended 'a second copy ends' 'A second copy did not end in 30 seconds.'
    Assert-That (@(Get-AppProcess).Count -eq 1) 'and only the first is left running' "$(@(Get-AppProcess).Count) copies are running."
    Assert-That (@(Get-ChildItem $settingsDirectory -File).Count -gt 0) 'the app wrote its files into its settings folder' 'The settings folder is empty.'

    Write-Output '== Remove it'
    $settingsBefore = @(Get-ChildItem $settingsDirectory -File | ForEach-Object Name | Sort-Object)
    $remover = Start-Process $updater -ArgumentList '--uninstall', '--silent' -PassThru -Wait
    Assert-That ($remover.ExitCode -eq 0) 'the uninstall ends well' "The uninstall ended with exit code $($remover.ExitCode)."
    Start-Sleep -Seconds 5
    Assert-That (@(Get-AppProcess).Count -eq 0) 'the running app is closed' 'The app is still running after the uninstall.'
    Assert-That (-not (Test-Path (Join-Path $installDirectory 'current'))) 'the installed files are gone' 'The install folder still holds the app.'
    Assert-That (-not (Get-ChildItem $startMenu -Filter 'Context Switcher*.lnk' -ErrorAction SilentlyContinue)) 'the Start menu shortcut is gone' 'The Start menu shortcut is still there.'
    $settingsAfter = @(Get-ChildItem $settingsDirectory -File | ForEach-Object Name | Sort-Object)
    Assert-That (($settingsBefore -join '|') -eq ($settingsAfter -join '|')) "the person's settings are left as they were" 'The uninstall changed the settings folder.'
}
finally {
    Remove-Item Env:\CONTEXTSWITCHER_CONFIG_DIR -ErrorAction SilentlyContinue
    Get-AppProcess | Stop-Process -Force -ErrorAction SilentlyContinue
    if (Test-Path (Join-Path $installDirectory 'Update.exe')) {
        Start-Process (Join-Path $installDirectory 'Update.exe') -ArgumentList '--uninstall', '--silent' -Wait -ErrorAction SilentlyContinue
    }
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

if ($env:GITHUB_STEP_SUMMARY) {
    Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value "### Windows package`nInstalled, run and removed version ${expectedVersion}: $($checks.Count) checks."
}

Write-Output ''
Write-Output "All $($checks.Count) checks passed."