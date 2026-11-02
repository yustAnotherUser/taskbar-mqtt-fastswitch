using System;
using System.IO;

namespace TaskbarMqtt.Platform
{
    public static class AutoStart
    {
        private const string DesktopFileName = "taskbar-mqtt.desktop";

        private static string AutostartDir
        {
            get
            {
                var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (string.IsNullOrEmpty(xdgConfig))
                    xdgConfig = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                return Path.Combine(xdgConfig, "autostart");
            }
        }

        private static string DesktopFilePath => Path.Combine(AutostartDir, DesktopFileName);

        public static bool IsEnabled()
        {
            try
            {
                return File.Exists(DesktopFilePath);
            }
            catch
            {
                return false;
            }
        }

        public static bool SetEnabled(bool enable)
        {
            try
            {
                if (!enable)
                {
                    if (File.Exists(DesktopFilePath))
                        File.Delete(DesktopFilePath);
                    return true;
                }

                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                    return false;

                Directory.CreateDirectory(AutostartDir);

                var contents =
                    "[Desktop Entry]\n" +
                    "Type=Application\n" +
                    "Name=Taskbar MQTT Client\n" +
                    "Comment=Publish pre-configured MQTT messages from the notification area\n" +
                    "Exec=" + exe + "\n" +
                    "Icon=taskbar-mqtt\n" +
                    "Terminal=false\n" +
                    "X-GNOME-Autostart-enabled=true\n" +
                    "StartupNotify=false\n";

                var tmp = DesktopFilePath + ".tmp";
                File.WriteAllText(tmp, contents);
                if (File.Exists(DesktopFilePath))
                    File.Delete(DesktopFilePath);
                File.Move(tmp, DesktopFilePath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
