using System;
using System.IO;
using Newtonsoft.Json;

namespace TaskbarMqtt.Config
{
    public static class ConfigStore
    {
        public static string ConfigPath
        {
            get
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(baseDir, "config.json");
            }
        }

        public static AppConfig Load()
        {
            try
            {
                var path = ConfigPath;
                if (!File.Exists(path)) return AppConfig.CreateDefault();

                var json = File.ReadAllText(path);
                var cfg = JsonConvert.DeserializeObject<AppConfig>(json);
                if (cfg == null) return AppConfig.CreateDefault();

                cfg.Normalize();
                return cfg;
            }
            catch
            {
                return AppConfig.CreateDefault();
            }
        }

        public static bool Save(AppConfig cfg)
        {
            try
            {
                if (cfg == null) return false;
                cfg.Normalize();

                var path = ConfigPath;
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var tmp = path + ".tmp";
                var json = JsonConvert.SerializeObject(cfg, Formatting.Indented);
                File.WriteAllText(tmp, json);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
