using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Styling;

namespace TaskbarMqtt.Platform
{
    public static class ThemeDetector
    {
        public static bool IsDark { get; private set; }

        public static void Apply()
        {
            IsDark = Detect();
            var app = Application.Current;
            if (app != null)
                app.RequestedThemeVariant = IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        private static bool Detect()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "gdbus",
                    Arguments = "call --session --dest org.freedesktop.portal.Desktop "
                        + "--object-path /org/freedesktop/portal/desktop "
                        + "--method org.freedesktop.portal.Settings.ReadOne "
                        + "org.freedesktop.appearance color-scheme",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    string output = p != null ? p.StandardOutput.ReadToEnd() : "";
                    if (p != null && !p.WaitForExit(1500))
                    {
                        try { p.Kill(); } catch { }
                    }
                    if (output.Contains("uint32 1"))
                        return true;
                    if (output.Contains("uint32 2"))
                        return false;
                }
            }
            catch
            {
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "gsettings",
                    Arguments = "get org.gnome.desktop.interface color-scheme",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    string output = p != null ? p.StandardOutput.ReadToEnd() : "";
                    if (p != null && !p.WaitForExit(1500))
                    {
                        try { p.Kill(); } catch { }
                    }
                    if (output.Contains("prefer-dark"))
                        return true;
                }
            }
            catch
            {
            }

            try
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var kdeGlobals = Path.Combine(home, ".config", "kdeglobals");
                if (File.Exists(kdeGlobals))
                {
                    var content = File.ReadAllText(kdeGlobals);
                    var colorScheme = Regex.Match(content, @"ColorScheme[^\r\n=]*=(.*)");
                    if (colorScheme.Success && colorScheme.Groups[1].Value.Contains("Dark", StringComparison.OrdinalIgnoreCase))
                        return true;
                    var widgetStyle = Regex.Match(content, @"widgetStyle[^\r\n=]*=(.*)");
                    if (widgetStyle.Success && widgetStyle.Groups[1].Value.Contains("dark", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
