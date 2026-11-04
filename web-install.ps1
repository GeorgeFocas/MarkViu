<#
.SYNOPSIS
    Installs MarkViu straight from GitHub, with nothing left behind but the installed app.

.DESCRIPTION
    Clones the repository to a temporary folder, runs install.ps1 from it and deletes the
    folder, whether or not the install succeeded. Needs Git and the .NET 10 SDK.

        irm https://raw.githubusercontent.com/GeorgeFocas/MarkViu/main/web-install.ps1 | iex

    To pass parameters:

        & ([scriptblock]::Create((irm https://raw.githubusercontent.com/GeorgeFocas/MarkViu/main/web-install.ps1))) -SelfContained

.PARAMETER InstallDir
    Where to install. Defaults to the default of install.ps1.

.PARAMETER SelfContained
    Passed on to install.ps1: bundle the .NET runtime in the install folder.

.PARAMETER Branch
    The branch to install from.
#>
param(
    [string]$InstallDir,
    [switch]$SelfContained,
    [string]$Branch = 'main'
)

# A child scope, because under "irm | iex" this runs inside the caller's session: settings
# must not leak into it, and failures must throw, since "exit" would close the caller's window.
& {
    $ErrorActionPreference = 'Stop'

    $repoUrl = 'https://github.com/GeorgeFocas/MarkViu.git'
    $sdkUrl = 'https://dotnet.microsoft.com/download'

    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        throw 'MarkViu needs Git to install. Get it from https://git-scm.com/downloads and open a new terminal.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "MarkViu needs the .NET 10 SDK to install. Get it from $sdkUrl and open a new terminal."
    }

    # A short name, because Windows PowerShell can't delete paths over 260 characters and the build goes deep.
    $tempDir = Join-Path ([IO.Path]::GetTempPath()) "MarkViu-$([guid]::NewGuid().ToString('N').Substring(0, 8))"

    # Build servers that stay running after the publish could keep files in the temp folder locked.
    $buildSettings = @{ MSBUILDDISABLENODEREUSE = '1'; UseSharedCompilation = 'false' }
    $savedSettings = @{}
    foreach ($name in $buildSettings.Keys) {
        $savedSettings[$name] = [Environment]::GetEnvironmentVariable($name)
        [Environment]::SetEnvironmentVariable($name, $buildSettings[$name])
    }

    Push-Location ([IO.Path]::GetTempPath())
    try {
        Write-Host "Downloading MarkViu ($Branch)..."
        git clone --quiet --depth 1 --branch $Branch $repoUrl $tempDir
        if ($LASTEXITCODE -ne 0) { throw "Could not clone branch '$Branch' of $repoUrl." }

        # Run from the clone, so that the SDK version pinned in its global.json is the one checked.
        Set-Location $tempDir
        $null = dotnet --version
        if ($LASTEXITCODE -ne 0) { throw "The installed .NET SDK can't build MarkViu. Get the latest .NET 10 SDK from $sdkUrl." }

        $installArgs = @()
        if ($InstallDir) { $installArgs += '-InstallDir', $InstallDir }
        if ($SelfContained) { $installArgs += '-SelfContained' }

        # A new process, because the execution policy may block running a script file from this one.
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $tempDir 'install.ps1') @installArgs
        if ($LASTEXITCODE -ne 0) { throw "Installing MarkViu failed (exit code $LASTEXITCODE)." }
    }
    finally {
        Pop-Location
        foreach ($name in $savedSettings.Keys) {
            [Environment]::SetEnvironmentVariable($name, $savedSettings[$name])
        }
        if (Test-Path $tempDir) {
            Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
            if (Test-Path $tempDir) { Write-Warning "Could not delete the temporary folder $tempDir. You can delete it yourself." }
        }
    }
}
