<#
.SYNOPSIS
    Removes MarkViu for the current user: the context-menu entry, the installed files
    and MarkViu's browser data in %LOCALAPPDATA%\MarkViu.
#>
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\MarkViu')
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path $InstallDir 'MarkViu.exe'

if (Test-Path $exe) {
    Get-Process MarkViu -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $exe } |
        ForEach-Object { Write-Host "Closing running MarkViu (pid $($_.Id))..."; $_ | Stop-Process; $_.WaitForExit(5000) | Out-Null }

    $unregister = Start-Process -FilePath $exe -ArgumentList '--unregister', '--quiet' -Wait -PassThru
    if ($unregister.ExitCode -ne 0) { Write-Warning "Unregister exited with code $($unregister.ExitCode)." }
}

# Also clean up directly, in case the exe is missing or failed.
foreach ($ext in '.md', '.markdown') {
    $key = "HKCU:\Software\Classes\SystemFileAssociations\$ext\shell\MarkViu"
    if (Test-Path $key) { Remove-Item $key -Recurse -Force }
}

if (Test-Path $exe) {
    Remove-Item $InstallDir -Recurse -Force
    Write-Host "Removed $InstallDir"
}

$dataDir = Join-Path $env:LOCALAPPDATA 'MarkViu'
if (Test-Path $dataDir) {
    Remove-Item $dataDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Removed $dataDir"
}

Write-Host 'MarkViu uninstalled.'
