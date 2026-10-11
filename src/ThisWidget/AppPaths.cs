using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ThisWidget;

/// <summary>Canonical paths work both inside a packaged launcher and from Explorer at sign-in.</summary>
public static class AppPaths
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint capacity, uint flags);

    public static string PhysicalPath(string path)
    {
        using var handle = CreateFile(Path.GetFullPath(path), 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        var result = new StringBuilder(32768);
        uint length = GetFinalPathNameByHandle(handle, result, (uint)result.Capacity, 0);
        if (length == 0 || length >= result.Capacity)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        string final = result.ToString();
        return final.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase) ? @"\\" + final[8..] :
            final.StartsWith(@"\\?\", StringComparison.Ordinal) ? final[4..] : final;
    }

    public static string DefaultJournalPath(string executable, string localData)
    {
        var app = new DirectoryInfo(Path.GetDirectoryName(PhysicalPath(executable))!);
        // Install.cmd keeps the journal beside its app folder, including a redirected installation.
        string root = app.Name.Equals("app", StringComparison.OrdinalIgnoreCase) &&
            app.Parent?.Name.Equals("ThisWidget", StringComparison.OrdinalIgnoreCase) == true
            ? app.Parent.FullName : Path.Combine(localData, "ThisWidget");
        return JournalPath(Path.Combine(root, "journal.json"));
    }

    public static string JournalPath(string path)
    {
        path = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        return Path.Combine(PhysicalPath(directory), Path.GetFileName(path));
    }
}
