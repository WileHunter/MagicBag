using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace WpfApp3.Services
{
    /// <param name="Swatch">菜单里的色块：底色、强调色</param>
    /// <param name="Light">浅色主题由 <see cref="LightPalette"/> 生成；为 null 时使用 Themes/Palette.{Key}.xaml</param>
    public sealed record ThemeOption(string Key, string Name, string Description, string[] Swatch, LightPalette? Light = null);

    /// <summary>
    /// 运行时切换配色：替换 App.xaml 中的配色字典，并把选择写入 settings.json。
    /// </summary>
    public static class ThemeManager
    {
        public static IReadOnlyList<ThemeOption> Themes { get; } = new[]
        {
            new ThemeOption("Tech", "科技蓝", "深海蓝底 · 青紫霓虹", new[] { "#0B1120", "#22D3EE" }),
            new ThemeOption("Mono", "墨白", "黑 · 白 · 灰 极简", new[] { "#0A0A0A", "#FAFAFA" }),

            new ThemeOption("Light", "素白", "柔白纸感 · 墨色点睛", new[] { "#F7F7F5", "#2A2A2E" },
                new LightPalette("#F7F7F5", "#FFFFFF", "#F3F3F1", "#EBEBE8", "#E7E7E3", "#D6D6D1",
                                 "#1C1C1E", "#5E5E62", "#A3A3A6", "#3A3A3F", "#1C1C1E", "#FBFBFA", 0.018)),
            new ThemeOption("Cloud", "云白", "Apple 式浅灰 · 天空蓝", new[] { "#F5F5F7", "#0A7CFF" },
                new LightPalette("#F5F5F7", "#FFFFFF", "#F2F2F5", "#E8E8ED", "#E3E3E8", "#D1D1D6",
                                 "#1D1D1F", "#6E6E73", "#AEAEB2", "#1A84FF", "#0066D6", "#FAFAFC", 0)),
            new ThemeOption("Oat", "燕麦", "暖米白 · 陶土橙", new[] { "#F5F2EC", "#C4704F" },
                new LightPalette("#F5F2EC", "#FFFDF9", "#F1EDE5", "#E9E3D8", "#E7E1D5", "#D7CEBF",
                                 "#2E2A25", "#6B645A", "#A59D90", "#C4704F", "#A6573A", "#FBF9F4", 0)),
            new ThemeOption("Iris", "雾紫", "Linear 式冷白 · 靛紫", new[] { "#F7F7FA", "#5E6AD2" },
                new LightPalette("#F7F7FA", "#FFFFFF", "#F2F2F7", "#E9E9F1", "#E5E5EE", "#D2D2DE",
                                 "#1B1B26", "#5D5D72", "#A1A1B3", "#6B76DB", "#4F5BC4", "#FAFAFD", 0.02)),
            new ThemeOption("Sage", "鼠尾草", "雾绿白 · 莫兰迪绿", new[] { "#F3F5F1", "#5E8A6E" },
                new LightPalette("#F3F5F1", "#FFFFFF", "#EEF1EB", "#E4E9E0", "#E0E6DC", "#CCD5C6",
                                 "#1E2A22", "#56645A", "#9AA69D", "#5E8A6E", "#476F57", "#F9FAF7", 0)),
        };

        public static string Current { get; private set; } = "Tech";

        public static event Action? ThemeChanged;

        private static ResourceDictionary? _palette;

        public static void LoadSaved() => Apply(AppSettings.Get("Theme", "Tech"), save: false);

        public static void Apply(string key, bool save = true)
        {
            var theme = Themes.FirstOrDefault(t => t.Key == key) ?? Themes[0];

            var dictionaries = Application.Current.Resources.MergedDictionaries;
            var index = _palette != null ? dictionaries.IndexOf(_palette) : -1;
            if (index < 0)
                index = dictionaries.ToList().FindIndex(d => d.Source?.OriginalString.Contains("Palette.") == true);

            var palette = theme.Light?.Build()
                          ?? new ResourceDictionary { Source = new Uri($"Themes/Palette.{theme.Key}.xaml", UriKind.Relative) };
            if (index >= 0)
                dictionaries[index] = palette;
            else
                dictionaries.Insert(Math.Max(0, dictionaries.Count - 1), palette);
            _palette = palette;

            Current = theme.Key;
            if (save) AppSettings.Set("Theme", theme.Key);
            ThemeChanged?.Invoke();
        }
    }
}
