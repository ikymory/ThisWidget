using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.IO;

namespace ThisWidget;

public static class Native
{
    public const int WakeMessage = 0x8000 + 88;
    public const int ExitMessage = WakeMessage + 1;
    public const int WidgetMessage = WakeMessage + 2;
    public static string ProfileKey { get; set; } = "";
    public delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string? name, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? name, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder b, int max);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr child);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int index, int value);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT rect);
    [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SetProp(IntPtr h, string name, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetProp(IntPtr h, string name);
    [StructLayout(LayoutKind.Sequential)]
    struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct POINT
    {
        public int X, Y;
    }
    public static void ActivateExisting(bool exit = false, bool editor = true)
    {
        bool Scan(IntPtr h, IntPtr p)
        {
            var b = new StringBuilder(256);
            GetWindowText(h, b, 256);
            if (b.ToString() == "ThisWidget · Desktop Widget" && (ProfileKey.Length == 0 || GetProp(h, ProfileKey) != IntPtr.Zero))
                PostMessage(h, (uint)(exit ? ExitMessage : editor ? WakeMessage : WidgetMessage), IntPtr.Zero, IntPtr.Zero);
            return true;
        }
        EnumWindows((h, p) => { Scan(h, p); EnumChildWindows(h, Scan, p); return true; }, IntPtr.Zero);
    }
    public static void MarkProfile(IntPtr h)
    {
        if (ProfileKey.Length > 0)
            SetProp(h, ProfileKey, new IntPtr(1));
    }
    public static void RegisterWake(IntPtr h) => RegisterHotKey(h, 71, 0x4000 | 0x0002 | 0x0001, 0x57); // Ctrl+Alt+W
    public static void UnregisterWake(IntPtr h) => UnregisterHotKey(h, 71);
    public static IntPtr DesktopHost()
    {
        var prog = FindWindow("Progman", null);
        if (prog == IntPtr.Zero)
            return IntPtr.Zero;
        IntPtr host = IntPtr.Zero;
        // Share the desktop icon host, above DefView, so widget buttons receive input.
        // A wallpaper WorkerW behind DefView cannot support an interactive widget.
        EnumWindows((h, p) => { if (FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) { host = h; return false; } return true; }, IntPtr.Zero);
        if (host == IntPtr.Zero)
            EnumChildWindows(prog, (h, p) => { if (FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) host = h; return true; }, IntPtr.Zero);
        return host;
    }
    public static bool Attach(IntPtr h, IntPtr host)
    {
        if (host == IntPtr.Zero || !IsWindow(host))
            return false;
        GetWindowRect(h, out var r);
        int old = GetWindowLong(h, -16);
        SetWindowLong(h, -16, (old & ~unchecked((int)0x80000000)) | 0x40000000);
        SetParent(h, host);
        if (GetParent(h) != host)
        {
            SetWindowLong(h, -16, old);
            return false;
        }
        var p = new POINT { X = r.Left, Y = r.Top };
        ScreenToClient(host, ref p);
        SetWindowPos(h, IntPtr.Zero, p.X, p.Y, r.Right - r.Left, r.Bottom - r.Top, 0x0020 | 0x0010);
        return true;
    }
    public static void Detach(IntPtr h)
    {
        GetWindowRect(h, out var r);
        SetParent(h, IntPtr.Zero);
        int style = GetWindowLong(h, -16);
        SetWindowLong(h, -16, (style & ~0x40000000) | unchecked((int)0x80000000));
        SetWindowPos(h, IntPtr.Zero, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, 0x0020 | 0x0010);
    }
    public static bool HasParent(IntPtr h) => (GetWindowLong(h, -16) & 0x40000000) != 0 && GetParent(h) != IntPtr.Zero;
    public static bool HostAlive(IntPtr host) => IsWindow(host);
    public static Point Position(IntPtr h)
    {
        GetWindowRect(h, out var r);
        return new Point(r.Left, r.Top);
    }
    public static void Move(IntPtr h, IntPtr host, Point screenPosition)
    {
        var p = new POINT { X = (int)screenPosition.X, Y = (int)screenPosition.Y };
        if (host != IntPtr.Zero)
            ScreenToClient(host, ref p);
        SetWindowPos(h, IntPtr.Zero, p.X, p.Y, 0, 0, 0x0001 | 0x0004 | 0x0010);
    }
    public static void Startup(bool enabled)
    {
        if (!Program.Integrate)
            return;
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
        if (enabled)
            key.SetValue("ThisWidget", "\"" + AppPaths.PhysicalPath(Environment.ProcessPath!) + "\" --background");
        else
            key.DeleteValue("ThisWidget", false);
    }
}
