using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace WpfApp3.Services
{
    /// <summary>
    /// 简单的键值配置，保存在程序目录下的 settings.json。
    /// </summary>
    public static class AppSettings
    {
        private static string SettingsFile => Path.Combine(AppPaths.BaseDir, "settings.json");
        private static Dictionary<string, string>? _cache;

        private static Dictionary<string, string> Data
        {
            get
            {
                if (_cache != null) return _cache;
                try
                {
                    if (File.Exists(SettingsFile))
                        _cache = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(SettingsFile));
                }
                catch
                {
                    // 配置损坏时使用默认值
                }
                return _cache ??= new Dictionary<string, string>();
            }
        }

        public static string Get(string key, string fallback) =>
            Data.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : fallback;

        public static void Set(string key, string value)
        {
            Data[key] = value;
            try
            {
                File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(Data, Formatting.Indented));
            }
            catch
            {
                // 无法写入配置（如只读目录）时忽略，仅本次生效
            }
        }
    }
}
