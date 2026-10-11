# Read-only checks for an installed, running widget. Run after Install.cmd with startup enabled.
[CmdletBinding()]
param([string]$Executable = (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'ThisWidget\app\ThisWidget.exe'))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../packaging/Paths.ps1')
$physical = [ThisWidgetInstallPaths]::Physical($Executable)
$startup = (Get-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run').GetValue('ThisWidget')
if ($startup -ne ('"' + $physical + '" --background')) { throw 'Windows startup does not point to the physical installed executable.' }
Write-Host 'PASS Windows startup points to the physical installed executable'
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class ThisWidgetDesktopCheck {
    delegate bool Callback(IntPtr h, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumWindows(Callback callback, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr h, Callback callback, IntPtr data);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder text, int size);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int index);
    public static bool Visible(uint process) {
        bool found = false;
        Callback visit = (h, data) => {
            uint owner; GetWindowThreadProcessId(h, out owner);
            var text = new StringBuilder(256); GetWindowText(h, text, 256);
            if (owner == process && text.ToString() == "ThisWidget \u00b7 Desktop Widget")
                found = IsWindowVisible(h) && IsWindowVisible(GetParent(h)) && (GetWindowLong(h, -16) & 0x40000000) != 0;
            return true;
        };
        EnumWindows((h, data) => { visit(h, data); EnumChildWindows(h, visit, data); return true; }, IntPtr.Zero);
        return found;
    }
}
'@
$deadline = [DateTime]::UtcNow.AddSeconds(10)
$visible = $false
do {
    foreach ($process in @(Get-Process ThisWidget -ErrorAction SilentlyContinue)) {
        try {
            if ([string]::Equals([ThisWidgetInstallPaths]::Physical($process.Path), $physical, [StringComparison]::OrdinalIgnoreCase)) {
                $visible = [ThisWidgetDesktopCheck]::Visible([uint32]$process.Id)
                if ($visible) { break }
            }
        } catch { }
    }
    if (-not $visible) { [Threading.Thread]::Sleep(100) }
} while (-not $visible -and [DateTime]::UtcNow -lt $deadline)
if (-not $visible) { throw 'Installed desktop widget is not running as a visible child of a visible desktop host.' }
Write-Host 'PASS Installed widget is running and natively visible on the desktop'
