# Per-user installation. Journals are outside the app folder and are never removed.
[CmdletBinding()]
param([switch]$NoLaunch)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Paths.ps1')
$source = [ThisWidgetInstallPaths]::Physical($PSScriptRoot)
$root = Get-ThisWidgetInstallRoot
$target = [IO.Path]::GetFullPath((Join-Path $root 'app'))
$exe = Join-Path $target 'ThisWidget.exe'
$legacyExe = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Yearform\app\Yearform.exe'

function Get-AppProcess([string]$Path) {
    $physical = if (Test-Path -LiteralPath $Path) { [ThisWidgetInstallPaths]::Physical($Path) } else { $Path }
    @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
        try { $_.Path -and [string]::Equals([ThisWidgetInstallPaths]::Physical($_.Path), $physical, [StringComparison]::OrdinalIgnoreCase) } catch { $false }
    })
}

# Ask the original calendar to close, including when it is an Explorer child.
function Stop-LegacyApp {
    $processes = @(Get-AppProcess $legacyExe)
    if ($processes.Count -eq 0) { return }
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class ThisWidgetLegacyClose {
    public delegate bool Callback(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumWindows(Callback callback, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, Callback callback, IntPtr data);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint id);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
    public static void Close(uint process) {
        Callback visit = (window, data) => {
            uint id; GetWindowThreadProcessId(window, out id);
            if (id == process) PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
            return true;
        };
        EnumWindows((window, data) => { visit(window, data); EnumChildWindows(window, visit, data); return true; }, IntPtr.Zero);
    }
}
'@
    foreach ($process in $processes) { [ThisWidgetLegacyClose]::Close([uint32]$process.Id) }
    foreach ($process in $processes) {
        if (-not $process.WaitForExit(8000)) { throw 'Please exit Yearform from its tray menu, then run Install.cmd again. Its saved journal will be preserved.' }
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $source 'ThisWidget.exe') -PathType Leaf)) {
    throw 'Extract the complete release zip before running Install.cmd.'
}
if (-not [string]::Equals($source.TrimEnd('\'), $target.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
    if (Test-Path -LiteralPath $exe) {
        $running = @(Get-AppProcess $exe)
        if ($running.Count -gt 0) {
            & $exe --exit
            foreach ($process in $running) {
                if (-not $process.WaitForExit(8000)) { throw 'Please exit ThisWidget from its tray menu before updating.' }
            }
        }
    }
    Stop-LegacyApp
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    # Copy only application deliverables, never a journal or arbitrary extracted files.
    foreach ($name in @('ThisWidget.exe', 'Paths.ps1', 'Install.cmd', 'Install.ps1', 'Uninstall.cmd', 'Uninstall.ps1', 'README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'CONTRIBUTING.md', 'RELEASE_NOTES.md')) {
        $file = Join-Path $source $name
        if (Test-Path -LiteralPath $file -PathType Leaf) { Copy-Item -LiteralPath $file -Destination (Join-Path $target $name) -Force }
    }
    foreach ($directory in @('licenses', 'docs')) {
        $folder = Join-Path $source $directory
        if (Test-Path -LiteralPath $folder -PathType Container) { Copy-Item -LiteralPath $folder -Destination $target -Recurse -Force }
    }
}

$root = [ThisWidgetInstallPaths]::Physical($root)
$target = [ThisWidgetInstallPaths]::Physical($target)
$exe = [ThisWidgetInstallPaths]::Physical($exe)
$shell = New-Object -ComObject WScript.Shell
foreach ($directory in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))) {
    $shortcut = $shell.CreateShortcut((Join-Path $directory 'ThisWidget.lnk'))
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $target
    $shortcut.Description = 'ThisWidget workout calendar and desktop widget'
    $shortcut.IconLocation = "$exe,0"
    $shortcut.Save()
    $old = Join-Path $directory 'Yearform.lnk'
    if ((Test-Path -LiteralPath $old) -and [string]::Equals($shell.CreateShortcut($old).TargetPath, $legacyExe, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $old
    }
}
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$oldStartup = if (Test-Path -LiteralPath $runKey) { (Get-Item -LiteralPath $runKey).GetValue('Yearform') } else { $null }
if ($oldStartup -and ($oldStartup -eq ('"' + $legacyExe + '" --background') -or $oldStartup -eq ('"' + $legacyExe + '"'))) {
    Remove-ItemProperty -LiteralPath $runKey -Name Yearform
}
Write-Host "Installed: $exe"
Write-Host 'Your journal is preserved. Open Settings to choose Windows startup and widget preferences.'
if (-not $NoLaunch) { Start-Process -FilePath $exe -WorkingDirectory $target -WindowStyle Hidden }
