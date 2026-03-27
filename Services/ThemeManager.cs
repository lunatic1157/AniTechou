using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace AniTechou.Services
{
    public sealed class AccentOption
    {
        public AccentOption(string key, string label, string hex)
        {
            Key = key;
            Label = label;
            Color = (Color)ColorConverter.ConvertFromString(hex);
        }

        public string Key { get; }
        public string Label { get; }
        public Color Color { get; }
    }

    public static class ThemeManager
    {
        private static readonly AccentOption[] AccentOptionsInternal =
        {
            new AccentOption("Crimson", "暮绯", "#C92A3A"),
            new AccentOption("Aurora", "极光蓝", "#2F7BFF"),
            new AccentOption("Violet", "夜紫", "#7C4DFF"),
            new AccentOption("Rose", "蔷薇", "#E94B7A"),
            new AccentOption("Tangerine", "赤橙", "#F97316"),
            new AccentOption("Mint", "青柠绿", "#10B981"),
            new AccentOption("Graphite", "曜石灰", "#566074")
        };

        private static readonly Dictionary<string, AccentOption> AccentMap = AccentOptionsInternal.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        private static ResourceDictionary _runtimeDictionary;

        public static IReadOnlyList<AccentOption> AccentOptions => AccentOptionsInternal;

        public static void Initialize(Application application)
        {
            EnsureRuntimeDictionary(application);
            var config = ConfigManager.Load();
            ApplyTheme(config.ThemeAccent, config.ThemeMode);
        }

        public static void ApplyTheme(string accentKey, string modeKey)
        {
            if (Application.Current == null)
            {
                return;
            }

            EnsureRuntimeDictionary(Application.Current);

            var accent = AccentMap.TryGetValue(accentKey ?? string.Empty, out var option)
                ? option
                : AccentMap["Aurora"];

            bool isDark = string.Equals(modeKey, "Dark", StringComparison.OrdinalIgnoreCase);

            Color window = isDark ? FromHex("#0F172A") : FromHex("#F4F7FB");
            Color surface1 = isDark ? FromHex("#111827") : FromHex("#FFFFFF");
            Color surface2 = isDark ? FromHex("#182235") : FromHex("#F8FAFC");
            Color surface3 = isDark ? FromHex("#223047") : FromHex("#EEF2F7");
            Color textPrimary = isDark ? FromHex("#F8FAFC") : FromHex("#1F2937");
            Color textSecondary = isDark ? FromHex("#CBD5E1") : FromHex("#64748B");
            Color textMuted = isDark ? FromHex("#94A3B8") : FromHex("#94A3B8");
            Color border = isDark ? FromHex("#334155") : FromHex("#D8E0EA");
            Color borderSubtle = isDark ? FromHex("#263346") : FromHex("#E7EDF5");
            Color accentSoft = isDark ? Mix(option.Color, surface2, 0.74) : Mix(option.Color, Colors.White, 0.86);
            Color accentStrong = isDark ? Mix(option.Color, Colors.White, 0.14) : Mix(option.Color, Colors.Black, 0.16);
            Color accentContrast = GetContrastColor(option.Color);

            _runtimeDictionary["WindowBackgroundBrush"] = CreateBrush(window);
            _runtimeDictionary["Surface1Brush"] = CreateBrush(surface1);
            _runtimeDictionary["Surface2Brush"] = CreateBrush(surface2);
            _runtimeDictionary["Surface3Brush"] = CreateBrush(surface3);
            _runtimeDictionary["TextPrimaryBrush"] = CreateBrush(textPrimary);
            _runtimeDictionary["TextSecondaryBrush"] = CreateBrush(textSecondary);
            _runtimeDictionary["TextMutedBrush"] = CreateBrush(textMuted);
            _runtimeDictionary["BorderBrush"] = CreateBrush(border);
            _runtimeDictionary["BorderSubtleBrush"] = CreateBrush(borderSubtle);
            _runtimeDictionary["AccentBrush"] = CreateBrush(option.Color);
            _runtimeDictionary["AccentSoftBrush"] = CreateBrush(accentSoft);
            _runtimeDictionary["AccentStrongBrush"] = CreateBrush(accentStrong);
            _runtimeDictionary["AccentContrastBrush"] = CreateBrush(accentContrast);
            _runtimeDictionary["DangerBrush"] = CreateBrush(isDark ? FromHex("#FB7185") : FromHex("#E35D6A"));
            _runtimeDictionary["SuccessBrush"] = CreateBrush(isDark ? FromHex("#34D399") : FromHex("#18A573"));
            _runtimeDictionary["ShadowBrush"] = CreateBrush(isDark ? FromHex("#40000000") : FromHex("#220F172A"));
        }

        public static string NormalizeAccent(string accentKey)
        {
            if (string.Equals(accentKey, "CloudRed", StringComparison.OrdinalIgnoreCase))
            {
                return "Crimson";
            }

            return AccentMap.ContainsKey(accentKey ?? string.Empty) ? accentKey : "Aurora";
        }

        public static string NormalizeMode(string modeKey)
        {
            return string.Equals(modeKey, "Dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
        }

        public static SolidColorBrush GetBrush(string resourceKey)
        {
            if (Application.Current?.TryFindResource(resourceKey) is SolidColorBrush brush)
            {
                return brush;
            }

            return new SolidColorBrush(Colors.Transparent);
        }

        public static AccentOption GetAccentOption(string accentKey)
        {
            var normalizedKey = NormalizeAccent(accentKey);
            return AccentMap.TryGetValue(normalizedKey, out var option)
                ? option
                : AccentMap["Aurora"];
        }

        private static void EnsureRuntimeDictionary(Application application)
        {
            if (_runtimeDictionary != null)
            {
                return;
            }

            _runtimeDictionary = new ResourceDictionary();
            application.Resources.MergedDictionaries.Add(_runtimeDictionary);
        }

        private static SolidColorBrush CreateBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Color GetContrastColor(Color color)
        {
            double luminance = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
            return luminance > 168 ? FromHex("#111827") : Colors.White;
        }

        private static Color Mix(Color baseColor, Color targetColor, double ratio)
        {
            byte a = (byte)Math.Round(baseColor.A + ((targetColor.A - baseColor.A) * ratio));
            byte r = (byte)Math.Round(baseColor.R + ((targetColor.R - baseColor.R) * ratio));
            byte g = (byte)Math.Round(baseColor.G + ((targetColor.G - baseColor.G) * ratio));
            byte b = (byte)Math.Round(baseColor.B + ((targetColor.B - baseColor.B) * ratio));
            return Color.FromArgb(a, r, g, b);
        }

        private static Color FromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
    }
}
