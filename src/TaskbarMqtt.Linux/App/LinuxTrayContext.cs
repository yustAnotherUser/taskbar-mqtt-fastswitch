using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using SkiaSharp;
using TaskbarMqtt.Config;
using TaskbarMqtt.Mqtt;
using TaskbarMqtt.UI;

namespace TaskbarMqtt.Platform
{
    public class LinuxTrayContext : IDisposable
    {
        private AppConfig _config;
        private readonly MqttService _mqtt;

        private readonly List<TrayIcon> _trayIcons = new List<TrayIcon>();
        private PopupWindow _popup;
        private SettingsWindow _settings;
        private NativeMenu _menu;

        private readonly SKBitmap _appIcon;
        private readonly SKBitmap _defaultButtonIcon;
        private readonly SKBitmap _mqttIcon;

        public LinuxTrayContext(AppConfig config)
        {
            _config = config ?? AppConfig.CreateDefault();
            _config.Normalize();

            _appIcon = ImageUtils.LoadResource("taskbar-mqtt.png") ?? new SKBitmap(64, 64);
            _defaultButtonIcon = ImageUtils.LoadResource("button-default.ico");
            _mqttIcon = ImageUtils.LoadResource("mqtt-icon.png");

            _mqtt = new MqttService();
            _mqtt.Error += msg => SystemNotifications.Show("MQTT", msg);
        }

        public void Start()
        {
            BuildMenu();
            BuildTray();
            _mqtt.UpdateConfig(_config.Broker);
        }

        private void BuildMenu()
        {
            _menu = new NativeMenu();
            if (_config.DisplayMode == DisplayMode.PopupPanel)
            {
                var miPanel = new NativeMenuItem("Open panel");
                miPanel.Click += (s, e) => ShowPopup();
                _menu.Add(miPanel);
            }
            var miSettings = new NativeMenuItem("Settings…");
            miSettings.Click += (s, e) => OpenSettings();
            _menu.Add(miSettings);
            _menu.Add(new NativeMenuItemSeparator());
            var miQuit = new NativeMenuItem("Quit");
            miQuit.Click += (s, e) => Quit();
            _menu.Add(miQuit);
        }

        private void OpenSettings()
        {
            if (_settings != null)
            {
                try
                {
                    _settings.Activate();
                }
                catch
                {
                }
                return;
            }
            _settings = new SettingsWindow(_config, OnSettingsApplied);
            _settings.Closed += (s, e) => _settings = null;
            _settings.Show();
        }

        private void OnSettingsApplied(AppConfig newConfig)
        {
            _config = newConfig;
            _mqtt.UpdateConfig(_config.Broker);
            HidePopup();
            _popup = null;
            BuildMenu();
            BuildTray();
        }

        private void BuildTray()
        {
            DisposeTray();
            if (_config.DisplayMode == DisplayMode.MultipleIcons)
            {
                for (int i = 0; i < _config.Buttons.Count; i++)
                {
                    int captured = i;
                    var icon = IconForButton(captured);
                    var ti = new TrayIcon
                    {
                        Icon = ImageUtils.ToIcon(icon) ?? ImageUtils.ToIcon(_appIcon),
                        ToolTipText = TooltipFor(captured),
                        Menu = _menu,
                        IsVisible = true
                    };
                    ti.Clicked += (s, e) => PublishButton(captured);
                    _trayIcons.Add(ti);
                }
            }
            else
            {
                var ti = new TrayIcon
                {
                    Icon = ImageUtils.ToIcon(LoadCustomIcon()) ?? ImageUtils.ToIcon(_appIcon),
                    ToolTipText = "Taskbar MQTT",
                    Menu = _menu,
                    IsVisible = true
                };
                ti.Clicked += (s, e) => ShowPopup();
                _trayIcons.Add(ti);
            }

            var app = Application.Current;
            if (app != null)
            {
                var icons = new TrayIcons();
                icons.AddRange(_trayIcons);
                TrayIcon.SetIcons(app, icons);
            }
        }

        private void DisposeTray()
        {
            foreach (var ti in _trayIcons)
            {
                try
                {
                    ti.IsVisible = false;
                    ti.Dispose();
                }
                catch
                {
                }
            }
            _trayIcons.Clear();
            var app = Application.Current;
            if (app != null)
                TrayIcon.SetIcons(app, new TrayIcons());
        }

        private SKBitmap LoadCustomIcon()
        {
            var target = 64;
            if (!string.IsNullOrEmpty(_config.IconPath) && File.Exists(_config.IconPath))
            {
                var bmp = ImageUtils.Load(_config.IconPath);
                if (bmp != null)
                {
                    using (bmp)
                    {
                        ImageUtils.ApplyTransparency(bmp, _config.MakeWhiteTransparent, _config.MakeBlackTransparent);
                        using (var resized = ImageUtils.FitSquare(bmp, target, _config.StretchIcon))
                        {
                            if (_config.RoundedTrayIcon)
                                using (var rounded = ImageUtils.RoundCorners(resized, Math.Max(2, target / 8)))
                                    return rounded.Copy();
                            return resized.Copy();
                        }
                    }
                }
            }
            if (_config.RoundedTrayIcon || _config.MakeWhiteTransparent || _config.MakeBlackTransparent)
            {
                var bmp = _appIcon.Copy();
                ImageUtils.ApplyTransparency(bmp, _config.MakeWhiteTransparent, _config.MakeBlackTransparent);
                using (bmp)
                using (var resized = ImageUtils.FitSquare(bmp, target, _config.StretchIcon))
                {
                    if (_config.RoundedTrayIcon)
                        using (var rounded = ImageUtils.RoundCorners(resized, Math.Max(2, target / 8)))
                            return rounded.Copy();
                    return resized.Copy();
                }
            }
            return _appIcon;
        }

        private SKBitmap IconForButton(int index)
        {
            if (index < 0 || index >= _config.Buttons.Count) return _defaultButtonIcon ?? _appIcon;
            var path = _config.Buttons[index].IconPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var bmp = ImageUtils.Load(path);
                if (bmp != null)
                {
                    using (bmp)
                    {
                        ImageUtils.ApplyTransparency(bmp, _config.Buttons[index].MakeWhiteTransparent, _config.Buttons[index].MakeBlackTransparent);
                        using (var resized = ImageUtils.FitSquare(bmp, 64, _config.Buttons[index].StretchImage))
                        {
                            if (_config.RoundedTrayIcon)
                                using (var rounded = ImageUtils.RoundCorners(resized, 10))
                                    return rounded.Copy();
                            return resized.Copy();
                        }
                    }
                }
            }
            if (_defaultButtonIcon != null)
                return ImageUtils.FitSquare(_defaultButtonIcon, 64, false);
            return _appIcon;
        }

        private SKBitmap GetButtonImage(int index)
        {
            try
            {
                if (index < 0 || index >= _config.Buttons.Count) return null;
                var cfg = _config.Buttons[index];
                var path = cfg.IconPath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                var bmp = ImageUtils.Load(path);
                if (bmp == null) return null;
                ImageUtils.ApplyTransparency(bmp, cfg.MakeWhiteTransparent, cfg.MakeBlackTransparent);
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private void ShowPopup()
        {
            if (_config.DisplayMode != DisplayMode.PopupPanel) return;
            if (_popup == null)
            {
                _popup = new PopupWindow(_config.Buttons, GetButtonImage, PublishButton, _mqttIcon,
                    _config.PopupSizePercent, _config.ShowTooltips, _config.ShowPayloadInTooltip, _config.PopupStaysOpen);
            }
            _popup.OpenAtPointer();
        }

        private void HidePopup()
        {
            try
            {
                _popup?.Hide();
            }
            catch
            {
            }
        }

        private void PublishButton(int index)
        {
            if (index < 0 || index >= _config.Buttons.Count) return;
            var btn = _config.Buttons[index];
            if (string.IsNullOrEmpty(btn.Topic))
            {
                SystemNotifications.Show("No topic", "Button " + (index + 1) + " has no MQTT topic configured.");
                return;
            }
            Task.Run(async () =>
            {
                bool ok = await _mqtt.PublishAsync(btn);
                if (!ok)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        SystemNotifications.Show("Publish failed", "Could not send '" + btn.Topic + "'. Check broker connection."));
                }
            });
        }

        private string TooltipFor(int index)
        {
            if (index < 0 || index >= _config.Buttons.Count) return "Taskbar MQTT";
            var b = _config.Buttons[index];
            var label = string.IsNullOrEmpty(b.Label) ? "Button " + (index + 1) : b.Label;
            return label.Length > 63 ? label.Substring(0, 63) : label;
        }

        public void OnExternalShowRequest()
        {
            if (_config.DisplayMode == DisplayMode.PopupPanel)
            {
                ShowPopup();
            }
            else
            {
                SystemNotifications.Show("Already running", "Taskbar MQTT Client is already in the notification area.");
            }
        }

        private void Quit()
        {
            Dispose();
            App.RequestShutdown();
        }

        public void Dispose()
        {
            DisposeTray();
            HidePopup();
            _popup = null;
            try
            {
                if (_settings != null)
                    _settings.Close();
            }
            catch
            {
            }
            _mqtt.Dispose();
        }
    }
}
