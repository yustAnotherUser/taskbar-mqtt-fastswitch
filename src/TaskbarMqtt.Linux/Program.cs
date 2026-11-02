using System;
using Avalonia;
using TaskbarMqtt.Platform;

namespace TaskbarMqtt
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (!SingleInstance.IsPrimary())
                return;

            if (OperatingSystem.IsLinux() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
            {
                Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", null);
                Environment.SetEnvironmentVariable("WAYLAND_SOCKET", null);
            }

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Console.Error.WriteLine("taskbar-mqtt: " + e.ExceptionObject);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(new X11PlatformOptions { EnableIme = true })
                .LogToTrace();
    }
}
