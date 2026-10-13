using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;

namespace ThisWidget;

public static class Program
{
    public static bool Integrate
    {
        get; private set;
    }
    public static string DataFolder { get; private set; } = "";

    [STAThread]
    public static int Main(string[] args)
    {
        try { return Run(args); }
        catch (Exception e)
        {
            Log(e);
            MessageBox.Show(e.Message, "ThisWidget could not start", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        string? path = null;
        Integrate = true;
        bool background = Array.Exists(args, x => x == "--background") || !Array.Exists(args, x => x == "--editor");
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--data" && i + 1 < args.Length)
            {
                path = AppPaths.JournalPath(args[++i]);
                Integrate = false;
            }
        path ??= AppPaths.DefaultJournalPath(Environment.ProcessPath!,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        DataFolder = Path.GetDirectoryName(path)!;
        var name = "Local\\ThisWidget-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())))[..24];
        using var mutex = new Mutex(true, name, out bool first);
        Native.ProfileKey = name;
        bool exitRequested = Array.Exists(args, x => x == "--exit");
        bool homeRequested = Array.Exists(args, x => x == "--home");
        if (!first)
        {
            if (exitRequested || homeRequested || !Array.Exists(args, x => x == "--background"))
            {
                // The mutex exists before WPF creates its message window. Wait for startup readiness.
                for (int attempt = 0; attempt < 100; attempt++)
                {
                    if (Native.ActivateExisting(exitRequested, editor: !background, home: homeRequested))
                        return 0;
                    Thread.Sleep(50);
                }
                Log(new TimeoutException("The existing ThisWidget instance did not become ready for activation."));
                return 2;
            }
            return 0;
        }
        if (exitRequested)
        {
            mutex.ReleaseMutex();
            return 0;
        }
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            Theme.Initialize();
            app.DispatcherUnhandledException += (_, e) =>
            {
                Log(e.Exception);
                MessageBox.Show("That action could not be completed. Your saved journal is preserved.\n\n" + e.Exception.Message,
                    "ThisWidget", MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Handled = true;
            };
            if (Integrate && !File.Exists(path))
            {
                var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Yearform", "journal.json");
                Store.MigrateLegacy(legacy, path);
            }
            var store = new Store(path);
            using var controller = new AppController(store, store.Load(), Integrate);
            controller.Start(background);
            if (homeRequested) controller.AddHomeWidget();
            else if (background && !Array.Exists(args, x => x == "--background"))
                controller.AddWidget();
            app.Run();
            return 0;
        }
        finally { mutex.ReleaseMutex(); }
    }

    public static void Log(Exception error)
    {
        if (DataFolder.Length == 0)
            return;
        try
        {
            Directory.CreateDirectory(DataFolder);
            File.AppendAllText(Path.Combine(DataFolder, "errors.log"), DateTime.Now + " " + error + Environment.NewLine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
