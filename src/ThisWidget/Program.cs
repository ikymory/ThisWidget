using System;
using System.IO;
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
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThisWidget", "journal.json");
        Integrate = true;
        bool background = Array.Exists(args, x => x == "--background") || !Array.Exists(args, x => x == "--editor");
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--data" && i + 1 < args.Length)
            {
                path = Path.GetFullPath(args[++i]);
                Integrate = false;
            }
        DataFolder = Path.GetDirectoryName(path)!;
        var name = "Local\\ThisWidget-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(path.ToUpperInvariant())))[..24];
        using var mutex = new Mutex(true, name, out bool first);
        Native.ProfileKey = name;
        bool exitRequested = Array.Exists(args, x => x == "--exit");
        if (!first)
        {
            Native.ActivateExisting(exitRequested, editor: !background);
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
            if (background && !Array.Exists(args, x => x == "--background"))
                controller.AddWidget();
            app.Run();
            return 0;
        }
        catch (Exception e)
        {
            Log(e);
            MessageBox.Show(e.Message, "ThisWidget could not start", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
        finally { mutex.ReleaseMutex(); }
    }

    public static void Log(Exception error)
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            File.AppendAllText(Path.Combine(DataFolder, "errors.log"), DateTime.Now + " " + error + Environment.NewLine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
