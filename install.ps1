<#
.SYNOPSIS
    Installs ContextSwitcher - or updates it - from the latest GitHub release that has Windows' files.

.DESCRIPTION
    Run it from PowerShell:

        irm https://raw.githubusercontent.com/ArtemkaGoldMan/ContextSwitcher/main/install.ps1 | iex

    Why this is one command: Windows marks what a browser downloads, and SmartScreen warns about an
    installer that isn't signed. A file PowerShell downloads isn't marked, so the installer just runs.
    It is still checked first when the release publishes checksums for it.

    It installs for the current user only, with no administrator rights, then opens the app, which lives
    in the tray. Everything is inside one function so that PowerShell has read the whole script before
    running any of it.
#>

function Install-ContextSwitcher {
    [CmdletBinding()]
    param(
        # Where the installer puts the app, instead of under %LOCALAPPDATA%; for trying it out.
        [string]$InstallTo,
        # Leaves the app closed once installed; for trying it out.
        [switch]$NoStart
    )

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'

    $repository = 'ArtemkaGoldMan/ContextSwitcher'
    $setupName = 'ContextSwitcher-win-Setup.exe'
    $sumsName = 'SHA256SUMS-windows.txt'
    $work = Join-Path ([IO.Path]::GetTempPath()) ('ContextSwitcher-install-' + [Guid]::NewGuid().ToString('N'))

    try {
        if ([Environment]::OSVersion.Version.Build -lt 19041) {
            throw 'Windows 10 (version 2004 or later) or Windows 11 is needed.'
        }
        $architecture = if ($env:PROCESSOR_ARCHITEW6432) { $env:PROCESSOR_ARCHITEW6432 } else { $env:PROCESSOR_ARCHITECTURE }
        if ($architecture -ne 'AMD64') {
            throw "this build is for Intel and AMD PCs (x64); this one is $architecture."
        }

        # Windows PowerShell 5.1 doesn't ask for TLS 1.2, which GitHub needs, unless told to.
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

        Write-Output 'Looking for the latest ContextSwitcher for Windows...'
        $headers = @{ 'User-Agent' = 'ContextSwitcher-install'; 'Accept' = 'application/vnd.github+json' }
        $releases = Invoke-RestMethod -UseBasicParsing -Headers $headers -Uri "https://api.github.com/repos/$repository/releases?per_page=30"
        $release = $releases |
            Where-Object { -not $_.draft -and -not $_.prerelease -and ($_.assets | Where-Object { $_.name -eq $setupName }) } |
            Select-Object -First 1
        if (-not $release) {
            throw "no release has $setupName yet. https://github.com/$repository/releases"
        }

        $setupAsset = $release.assets | Where-Object { $_.name -eq $setupName }
        $sumsAsset = $release.assets | Where-Object { $_.name -eq $sumsName }
        New-Item -ItemType Directory -Path $work -Force | Out-Null
        $setup = Join-Path $work $setupName

        Write-Output "Downloading ContextSwitcher $($release.tag_name.TrimStart('v')) (about $([math]::Round($setupAsset.size / 1MB)) MB)..."
        Invoke-WebRequest -UseBasicParsing -Headers $headers -Uri $setupAsset.browser_download_url -OutFile $setup

        if ($sumsAsset) {
            $sums = (Invoke-WebRequest -UseBasicParsing -Headers $headers -Uri $sumsAsset.browser_download_url).Content
            $expected = $sums -split "`r?`n" |
                Where-Object { $_ -match ('\s\*?' + [regex]::Escape($setupName) + '$') } |
                ForEach-Object { ($_ -split '\s+')[0] } |
                Select-Object -First 1
            if (-not $expected) {
                throw "the release's checksums don't list $setupName, so it wasn't installed."
            }
            if ((Get-FileHash -Algorithm SHA256 -Path $setup).Hash -ne $expected.ToUpperInvariant()) {
                throw 'the download is damaged (its checksum is wrong), so it wasn''t installed.'
            }
            Write-Output 'Checked that it is not damaged.'
        }
        else {
            Write-Output 'This release publishes no checksum for the installer, so it was not checked.'
        }

        $root = if ($InstallTo) { $InstallTo } else { Join-Path $env:LOCALAPPDATA 'ContextSwitcher' }

        # Only the copy that is about to be replaced has to be closed.
        $running = @(Get-Process -Name 'ContextSwitcher' -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -and $_.Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) })
        if ($running.Count -gt 0) {
            Write-Output 'Quitting the running copy...'
            foreach ($process in $running) {
                [void]$process.CloseMainWindow()
            }
            $running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
            if (@($running | Where-Object { -not $_.HasExited }).Count -gt 0) {
                throw 'ContextSwitcher is still running. Quit it from its tray icon (right-click it, then Quit) and run this again.'
            }
        }

        Write-Output 'Installing...'
        $arguments = @('--silent')
        if ($InstallTo) {
            $arguments += @('--installto', "`"$InstallTo`"")
        }
        $installer = Start-Process -FilePath $setup -ArgumentList $arguments -PassThru -Wait
        if ($installer.ExitCode -ne 0) {
            throw "the installer ended with exit code $($installer.ExitCode)."
        }

        $app = Join-Path $root 'current\ContextSwitcher.exe'
        if (-not (Test-Path $app)) {
            throw "the installer ended well, but there is no app at $app."
        }

        Write-Output "Installed ContextSwitcher $($release.tag_name.TrimStart('v'))."
        if (-not $NoStart) {
            Start-Process -FilePath $app
            Write-Output 'It lives in the tray, by the clock - look for the two-squares icon; Windows 11 hides a new'
            Write-Output 'one behind the ^ arrow, so drag it out to keep it in sight. Later versions install from'
            Write-Output 'Settings > Updates, or by running this command again.'
        }
    }
    catch {
        Write-Error "ContextSwitcher: $($_.Exception.Message)" -ErrorAction Continue
    }
    finally {
        Remove-Item -Path $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Install-ContextSwitcher
