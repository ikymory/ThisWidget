[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Paths.ps1')
$root = [IO.Path]::GetFullPath((Get-ThisWidgetInstallRoot))
if (Test-Path -LiteralPath $root -PathType Container) { $root = [ThisWidgetInstallPaths]::Physical($root) }
$target = [IO.Path]::GetFullPath((Join-Path $root 'app'))
$exe = Join-Path $target 'ThisWidget.exe'
if ((Split-Path $target -Parent) -ne $root -or (Split-Path $target -Leaf) -ne 'app') {
    throw 'Unexpected installation path; no files were removed.'
}
$running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
    try { $_.Path -and [string]::Equals([ThisWidgetInstallPaths]::Physical($_.Path), $exe, [StringComparison]::OrdinalIgnoreCase) } catch { $false }
})
if ($running.Count -gt 0) {
    & $exe --exit
    foreach ($process in $running) {
        if (-not $process.WaitForExit(8000)) { throw 'Please exit ThisWidget from its tray menu before uninstalling.' }
    }
}
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$startup = if (Test-Path -LiteralPath $runKey) { (Get-Item -LiteralPath $runKey).GetValue('ThisWidget') } else { $null }
if ($startup -and ($startup -eq ('"' + $exe + '" --background') -or $startup -eq ('"' + $exe + '"'))) {
    Remove-ItemProperty -LiteralPath $runKey -Name ThisWidget
}
$shell = New-Object -ComObject WScript.Shell
foreach ($directory in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))) {
    $path = Join-Path $directory 'ThisWidget.lnk'
    if ((Test-Path -LiteralPath $path) -and [string]::Equals($shell.CreateShortcut($path).TargetPath, $exe, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $path
    }
}
if (Test-Path -LiteralPath $target -PathType Container) { Remove-Item -LiteralPath $target -Recurse -Force }
Write-Host "ThisWidget is uninstalled. Your journal remains at $root\journal.json"
Write-Host 'For a portable copy, remove its extracted folder separately.'
