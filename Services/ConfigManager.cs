using System;
using System.IO;
using System.Text.Json;

namespace AniTechou.Services
{
    public class AppConfig
    {
        public string Platform { get; set; } = "DeepSeek";
        public string ApiKey { get; set; } = "";
        public string ApiUrl { get; set; } = "https://api.deepseek.com/v1";
        public string Model { get; set; } = "deepseek-chat";
        public bool AutoLogin { get; set; } = true;
        public string LastAccount { get; set; } = "";
        public string CustomSystemPrompt { get; set; } = "";
        public string ThemeAccent { get; set; } = "Aurora";
        public string ThemeMode { get; set; } = "Light";
    }

    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AniTechou",
            "config.json"
        );

        public static AppConfig Load()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigManager] Load - ConfigPath: {ConfigPath}");
                System.Diagnostics.Debug.WriteLine($"[ConfigManager] Load - File exists: {File.Exists(ConfigPath)}");

                if (!File.Exists(ConfigPath))
                {
                    return new AppConfig();
                }

                string json = File.ReadAllText(ConfigPath);
                System.Diagnostics.Debug.WriteLine($"[ConfigManager] Load - JSON: {json}");

                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigManager] Load error: {ex.Message}");
                return new AppConfig();
            }
        }

        public static void Save(AppConfig config)
        {
            try
            {
                string dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);

                System.Diagnostics.Debug.WriteLine($"[ConfigManager] Save - Saved to: {ConfigPath}");
                System.Diagnostics.Debug.WriteLine($"[ConfigManager] Save - JSON: {json}");
                System.Diagnostics.Debug.WriteLine($"[ConfigManager] Save - File exists after save: {File.Exists(ConfigPath)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigManager] Save error: {ex.Message}");
            }
        }
    }
}
