using System;
using System.Windows;
using System.Windows.Media;

namespace ThisWidget;

public static class Theme
{
    public static void Initialize()
    {
        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/ThisWidget;component/Ui.xaml", UriKind.Relative)
        });
    }

    public static Brush Brush(string name) => (Brush)Application.Current.FindResource(name);

    public static void Apply(Appearance settings)
    {
        string[] keys = { "Surface", "Card", "Cell", "Line", "Ink", "Muted" };
        string[] colors = settings.Theme == "Paper"
            ? new[] { "#F3F3EA", "#EAECE0", "#DDE1D3", "#CCD1BF", "#20251C", "#64705B" }
            : new[] { "#151816", "#1D211E", "#292E29", "#343B33", "#F0F3E9", "#939F8E" };
        for (int i = 0; i < keys.Length; i++)
            Application.Current.Resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
        var accent = (Color)ColorConverter.ConvertFromString(settings.Accent);
        Application.Current.Resources["Accent"] = new SolidColorBrush(accent);
        Application.Current.Resources["AccentInk"] = new SolidColorBrush(
            .2126 * accent.R + .7152 * accent.G + .0722 * accent.B > 145
                ? Color.FromRgb(23, 33, 12) : Colors.White);
    }
}
