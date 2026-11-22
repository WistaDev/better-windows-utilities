using System.Windows;

namespace BWU.Services;

public static class ThemeService
{
    /// <summary>Swaps the colour dictionary (always the first merged dictionary).</summary>
    public static void Apply(string theme)
    {
        var file = theme == "Light" ? "ColorsLight.xaml" : "Colors.xaml";
        var dict = new ResourceDictionary { Source = new Uri($"pack://application:,,,/BWU;component/Themes/{file}") };
        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count > 0) merged[0] = dict; else merged.Insert(0, dict);
    }
}
