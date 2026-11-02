using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TaskbarMqtt.Platform;
using TaskbarMqtt.Config;

namespace TaskbarMqtt
{
    public partial class App : Application
    {
        public static LinuxTrayContext Tray { get; private set; }

        private IClassicDesktopStyleApplicationLifetime _lifetime;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            var config = ConfigStore.Load();
            bool firstRun = !File.Exists(ConfigStore.ConfigPath);
            if (firstRun || config.ButtonCount < AppConfig.MinButtons || config.ButtonCount > AppConfig.MaxButtons)
            {
                config.ButtonCount = 4;
                config.Normalize();
                ConfigStore.Save(config);
            }

            ThemeDetector.Apply();

            Tray = new LinuxTrayContext(config);
            Tray.Start();

            SingleInstance.StartListening(Tray.OnExternalShowRequest);

            base.OnFrameworkInitializationCompleted();

            _lifetime = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (_lifetime != null)
            {
                _lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }
        }

        public static void RequestShutdown()
        {
            Tray?.Dispose();
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        }
    }
}
