# Resolve MSIX/AppData redirection so shortcuts, updates and uninstall work from Explorer too.
if (-not ('ThisWidgetInstallPaths' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Text;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class ThisWidgetInstallPaths {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder path, uint size, uint flags);
    public static string Physical(string path) {
        using (var file = CreateFile(Path.GetFullPath(path), 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero)) {
            if (file.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            var result = new StringBuilder(32768);
            uint length = GetFinalPathNameByHandle(file, result, 32768, 0);
            if (length == 0 || length >= 32768) throw new Win32Exception(Marshal.GetLastWin32Error());
            string final = result.ToString();
            return final.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase) ? @"\\" + final.Substring(8) :
                final.StartsWith(@"\\?\", StringComparison.Ordinal) ? final.Substring(4) : final;
        }
    }
}
'@
}

function Find-ThisWidgetInstallRoot([string]$Executable) {
    if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) { return $null }
    try {
        $physical = [ThisWidgetInstallPaths]::Physical($Executable)
        if ([IO.Path]::GetFileName($physical) -ne 'ThisWidget.exe') { return $null }
        $app = [IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName($physical))
        if ($app.Name -eq 'app' -and $app.Parent -and $app.Parent.Name -eq 'ThisWidget') {
            return $app.Parent.FullName
        }
    } catch { }
    return $null
}

function Select-ThisWidgetInstallRoot([string]$SourceDirectory, [string]$LocalData, [string[]]$RegisteredExecutables) {
    $installedSource = Find-ThisWidgetInstallRoot (Join-Path $SourceDirectory 'ThisWidget.exe')
    if ($installedSource) { return $installedSource }
    foreach ($executable in $RegisteredExecutables) {
        $registeredRoot = Find-ThisWidgetInstallRoot $executable
        if ($registeredRoot) { return $registeredRoot }
    }
    return Join-Path $LocalData 'ThisWidget'
}

function Get-ThisWidgetInstallRoot {
    $registered = @()
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    $command = if (Test-Path -LiteralPath $runKey) { (Get-Item -LiteralPath $runKey).GetValue('ThisWidget') } else { $null }
    if ($command -and $command -match '^"([^"]+)"(?: --background)?$') { $registered += $Matches[1] }
    $shell = New-Object -ComObject WScript.Shell
    foreach ($directory in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))) {
        $shortcut = Join-Path $directory 'ThisWidget.lnk'
        if (Test-Path -LiteralPath $shortcut -PathType Leaf) { $registered += $shell.CreateShortcut($shortcut).TargetPath }
    }
    return Select-ThisWidgetInstallRoot $PSScriptRoot ([Environment]::GetFolderPath('LocalApplicationData')) $registered
}
