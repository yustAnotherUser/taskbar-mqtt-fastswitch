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
                var local = Path.Combine(baseDir, "config.json");
                if (!IsWindows)
                {
                    if (File.Exists(local)) return local;
                    return DefaultDataPath;
                }
                try
                {
                    var probe = Path.Combine(baseDir, Path.GetRandomFileName());
                    using (var s = File.Create(probe)) { }
                    File.Delete(probe);
                    return local;
                }
                catch { }
                return DefaultDataPath;
            }
        }

        private static bool IsWindows
        {
            get
            {
#if NET5_0_OR_GREATER
                return OperatingSystem.IsWindows();
#else
                return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                    System.Runtime.InteropServices.OSPlatform.Windows);
#endif
            }
        }

        public static string DefaultDataPath
        {
            get
            {
                if (!IsWindows)
                {
                    var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                    if (string.IsNullOrEmpty(xdgConfig))
                        xdgConfig = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                    return Path.Combine(xdgConfig, "TaskbarMqtt", "config.json");
                }
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TaskbarMqtt", "config.json");
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
