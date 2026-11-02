using System;
using System.Diagnostics;

namespace TaskbarMqtt.Platform
{
    public static class SystemNotifications
    {
        public static void Show(string summary, string body)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "notify-send",
                    Arguments = "--app-name=taskbar-mqtt --expire-time=3500 --icon=taskbar-mqtt "
                        + Quote(summary ?? "Taskbar MQTT") + " " + Quote(body ?? ""),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    if (p != null && !p.WaitForExit(2000))
                    {
                        try { p.Kill(); } catch { }
                    }
                }
            }
            catch
            {
            }
        }

        private static string Quote(string s)
        {
            return "'" + (s ?? "").Replace("'", "'\\''") + "'";
        }
    }
}
