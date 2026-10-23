<#
.SYNOPSIS
    Builds MarkViu and installs it for the current user.

.DESCRIPTION
    Publishes a build, copies it to the install folder and adds
    "Open in MarkViu" to the Explorer context menu of .md and .markdown files.
    No administrator rights are needed. Run uninstall.ps1 to remove it.

.PARAMETER SelfContained
    Bundle the .NET runtime (~150 MB) so the install folder can be copied to a machine
    without .NET 10. By default the build uses the installed .NET 10 Desktop Runtime.
#>
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\MarkViu'),
    [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'src\MarkViu\MarkViu.csproj'
$publishDir = Join-Path $PSScriptRoot 'publish'
$exe = Join-Path $InstallDir 'MarkViu.exe'

Write-Host 'Publishing MarkViu...'
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $project -c Release -r win-x64 --self-contained:$($SelfContained.IsPresent) `
    -p:SatelliteResourceLanguages=en -o $publishDir --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

if (Test-Path $InstallDir) {
    # Never wipe a folder that isn't an existing MarkViu install.
    $isEmpty = -not (Get-ChildItem $InstallDir -Force | Select-Object -First 1)
    if (-not $isEmpty -and -not (Test-Path $exe)) {
        throw "$InstallDir exists and does not look like a MarkViu install. Choose another -InstallDir."
    }
    Get-Process MarkViu -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $exe } |
        ForEach-Object { Write-Host "Closing running MarkViu (pid $($_.Id))..."; $_ | Stop-Process; $_.WaitForExit(5000) | Out-Null }
    Remove-Item $InstallDir -Recurse -Force
}

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item (Join-Path $publishDir '*') $InstallDir -Recurse

$register = Start-Process -FilePath $exe -ArgumentList '--register', '--quiet' -Wait -PassThru
if ($register.ExitCode -ne 0) { throw "Registering the context menu failed (exit code $($register.ExitCode))." }

Write-Host ''
Write-Host "MarkViu installed to $InstallDir"
Write-Host 'Right-click a .md file and choose "Open in MarkViu".'
Write-Host 'On Windows 11 it is under "Show more options" (or Shift+Right-click).'
