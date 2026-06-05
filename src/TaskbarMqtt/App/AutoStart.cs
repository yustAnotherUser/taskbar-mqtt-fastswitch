using System;
using Microsoft.Win32;

namespace TaskbarMqtt.App
{
    public static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "TaskbarMqttClient";

        public static bool IsEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (key == null) return false;
                    var v = key.GetValue(ValueName) as string;
                    return !string.IsNullOrEmpty(v);
                }
            }
            catch { return false; }
        }

        public static bool SetEnabled(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return false;
                    if (enable)
                    {
                        var exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        if (string.IsNullOrEmpty(exe) || !System.IO.File.Exists(exe))
                            exe = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\') + "\\TaskbarMqtt.exe";
                        var cmd = "\"" + exe + "\"";
                        key.SetValue(ValueName, cmd);
                    }
                    else
                    {
                        if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName, false);
                    }
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
