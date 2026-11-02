using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
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

        private TrayIconService _tray;
        private PopupWindow _popup;
        private SettingsWindow _settings;

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

        public async void Start()
        {
            _tray = new TrayIconService();
            _tray.ItemActivated += OnItemActivated;
            _tray.ItemContextMenu += OnItemContextMenu;
            try
            {
                await _tray.StartAsync();
            }
            catch (Exception ex)
            {
                SystemNotifications.Show("Taskbar MQTT", "Tray init failed: " + ex.Message);
                return;
            }
            await UpdateTrayItemsAsync();
            _mqtt.UpdateConfig(_config.Broker);
        }

        private void OnItemActivated(int index, int x, int y)
        {
            if (_config.DisplayMode == DisplayMode.MultipleIcons)
            {
                PublishButton(index);
            }
            else
            {
                ShowPopupAt(x, y);
            }
        }

        private void OnItemContextMenu(int index, int x, int y)
        {
            ShowPopupAt(x, y);
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

        private void Quit()
        {
            Dispose();
            App.RequestShutdown();
        }

        private async void OnSettingsApplied(AppConfig newConfig)
        {
            _config = newConfig;
            _mqtt.UpdateConfig(_config.Broker);
            HidePopup();
            _popup = null;
            await UpdateTrayItemsAsync();
        }

        private async Task UpdateTrayItemsAsync()
        {
            if (_tray == null) return;

            if (_config.DisplayMode == DisplayMode.MultipleIcons)
            {
                for (int i = 0; i < TrayIconService.MaxVisibleIcons && i < _config.Buttons.Count; i++)
                {
                    var icon = IconForButton(i);
                    await _tray.SetItemAsync(i, icon, TooltipFor(i), "Left-click: publish\nRight-click: open panel", active: true);
                }
                for (int i = _config.Buttons.Count; i < TrayIconService.MaxVisibleIcons; i++)
                {
                    await _tray.SetItemAsync(i, null, "Taskbar MQTT", "", active: false);
                }
            }
            else
            {
                var icon = LoadCustomIcon();
                await _tray.SetItemAsync(0, icon, "Taskbar MQTT", "Left-click: open panel", active: true);
                for (int i = 1; i < TrayIconService.MaxVisibleIcons; i++)
                {
                    await _tray.SetItemAsync(i, null, "Taskbar MQTT", "", active: false);
                }
            }
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

        private void ShowPopupAt(int x, int y)
        {
            if (_config.DisplayMode != DisplayMode.PopupPanel) return;
            bool haveCoords = x != 0 || y != 0;
            if (!haveCoords)
            {
                haveCoords = PointerPosition.TryGet(out x, out y);
            }
            if (_popup == null)
            {
                _popup = new PopupWindow(_config.Buttons, GetButtonImage, PublishButton, _mqttIcon,
                    _config.PopupSizePercent, _config.ShowTooltips, _config.ShowPayloadInTooltip, _config.PopupStaysOpen,
                    OpenSettings, Quit);
            }
            if (haveCoords)
                _popup.OpenAt(x, y);
            else
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
                    Dispatcher.UIThread.Post(() =>
                        SystemNotifications.Show("Publish failed", "Could not send '" + btn.Topic + "'. Check broker connection."));
                }
            });
        }

        private string TooltipFor(int index)
        {
            if (index < 0 || index >= _config.Buttons.Count) return "Taskbar MQTT";
            var b = _config.Buttons[index];
            var label = string.IsNullOrEmpty(b.Label) ? "Button " + (index + 1) : b.Label;
            var desc = label.Length > 63 ? label.Substring(0, 63) : label;
            if (!string.IsNullOrEmpty(b.Topic))
                desc += "\n" + b.Topic;
            return desc;
        }

        public void OnExternalShowRequest()
        {
            if (_config.DisplayMode == DisplayMode.PopupPanel)
            {
                ShowPopupAt(0, 0);
            }
            else
            {
                SystemNotifications.Show("Already running", "Taskbar MQTT Client is already in the notification area.");
            }
        }

        public void Dispose()
        {
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
            try { _tray?.Dispose(); } catch { }
            _tray = null;
            _mqtt.Dispose();
        }
    }
}
