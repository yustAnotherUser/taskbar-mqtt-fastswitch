using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaskbarMqtt.Config;
using TaskbarMqtt.Mqtt;
using TaskbarMqtt.UI;

namespace TaskbarMqtt.App
{
    public class TrayContext : ApplicationContext
    {
        private AppConfig _config;
        private readonly MqttService _mqtt;

        private NotifyIcon _trayIcon;
        private List<NotifyIcon> _multiIcons;
        private PopupForm _popup;
        private ContextMenuStrip _contextMenu;

        private readonly Icon _appIcon;
        private readonly Icon _defaultButtonIcon;
        private readonly Bitmap _defaultButtonBitmap;

        private readonly Form _marshalForm;

        public TrayContext(AppConfig config)
        {
            _config = config ?? AppConfig.CreateDefault();
            _config.Normalize();

            _marshalForm = new Form
            {
                ShowInTaskbar = false,
                FormBorderStyle = FormBorderStyle.None,
                Opacity = 0,
                Width = 0,
                Height = 0,
                WindowState = FormWindowState.Minimized
            };

            _appIcon = LoadResourceIcon("app.ico");
            _defaultButtonIcon = LoadResourceIcon("button-default.ico");
            _defaultButtonBitmap = _defaultButtonIcon != null ? _defaultButtonIcon.ToBitmap() : null;

            _mqtt = new MqttService();
            _mqtt.Error += msg => ShowBalloon("MQTT", msg, ToolTipIcon.Error);
            _mqtt.StatusChanged += msg => { /* could update tooltip */ };

            BuildContextMenu();
            BuildTray();

            _mqtt.UpdateConfig(_config.Broker);
        }

        // ----- Resource icon loading -----
        private static Icon LoadResourceIcon(string fileName)
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var resName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));
                if (resName == null) return null;
                using (var s = asm.GetManifestResourceStream(resName))
                {
                    if (s == null) return null;
                    return new Icon(s);
                }
            }
            catch { return null; }
        }

        private static Icon BitmapToIcon(Bitmap bmp, int size)
        {
            using (var resized = new Bitmap(bmp, size, size))
            {
                var hIcon = resized.GetHicon();
                var ico = (Icon)Icon.FromHandle(hIcon).Clone();
                return ico;
            }
        }

        private Icon IconForButton(int index, int size)
        {
            if (index < 0 || index >= _config.Buttons.Count) return _defaultButtonIcon;
            var path = _config.Buttons[index].IconPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    using (var bmp = new Bitmap(path))
                    {
                        return BitmapToIcon(bmp, size);
                    }
                }
                catch { }
            }
            if (_defaultButtonBitmap != null) return BitmapToIcon(_defaultButtonBitmap, size);
            return _appIcon;
        }

        // ----- Context menu -----
        private void BuildContextMenu()
        {
            _contextMenu = new ContextMenuStrip();
            var miSettings = new ToolStripMenuItem("Settings…");
            miSettings.Click += (s, e) => OpenSettings();
            var miReconnect = new ToolStripMenuItem("Reconnect now");
            miReconnect.Click += (s, e) => Task.Run(() => _mqtt.ReconnectAsync());
            var miQuit = new ToolStripMenuItem("Quit");
            miQuit.Click += (s, e) => ExitThread();
            _contextMenu.Opening += (s, e) => HidePopup();
            _contextMenu.Items.Add(miSettings);
            _contextMenu.Items.Add(miReconnect);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(miQuit);
        }

        private void OpenSettings()
        {
            if (Application.OpenForms.OfType<SettingsForm>().Any()) return;
            using (var f = new SettingsForm(_config, OnSettingsApplied))
            {
                f.ShowDialog();
            }
        }

        private void OnSettingsApplied(AppConfig newConfig)
        {
            _config = newConfig;
            _mqtt.UpdateConfig(_config.Broker);
            RebuildTray();
        }

        // ----- Tray construction -----
        private void BuildTray()
        {
            DisposeTray();
            if (_config.DisplayMode == DisplayMode.MultipleIcons)
                BuildMultiIconTray();
            else
                BuildPopupPanelTray();
        }

        private void RebuildTray()
        {
            BuildTray();
        }

        private void BuildPopupPanelTray()
        {
            _trayIcon = new NotifyIcon
            {
                Icon = _appIcon ?? SystemIcons.Application,
                Text = "Taskbar MQTT",
                Visible = true,
                ContextMenuStrip = _contextMenu
            };
            _trayIcon.MouseClick += OnPrimaryMouseClick;
        }

        private void BuildMultiIconTray()
        {
            _multiIcons = new List<NotifyIcon>();
            for (int i = _config.Buttons.Count - 1; i >= 0; i--)
            {
                int captured = i;
                var ni = new NotifyIcon
                {
                    Icon = IconForButton(captured, 16) ?? SystemIcons.Application,
                    Text = TooltipFor(captured),
                    Visible = true,
                    ContextMenuStrip = _contextMenu
                };
                ni.MouseClick += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        PublishButton(captured);
                    }
                };
                _multiIcons.Add(ni);
            }
            _multiIcons.Reverse();
        }

        private void DisposeTray()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.MouseClick -= OnPrimaryMouseClick;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            if (_multiIcons != null)
            {
                foreach (var ni in _multiIcons)
                {
                    ni.Visible = false;
                    ni.Dispose();
                }
                _multiIcons = null;
            }
        }

        private void OnPrimaryMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowPopup();
            }
        }

        private void HidePopup()
        {
            if (_popup != null && !_popup.IsDisposed)
            {
                _popup.Hide();
            }
        }

        private void ShowPopup()
        {
            if (_config.DisplayMode != DisplayMode.PopupPanel) return;
            HidePopup();
            if (_popup == null || _popup.IsDisposed)
            {
                _popup = new PopupForm(_config.Buttons, GetButtonImage, PublishButton);
            }
            _popup.ShowAtCursor();
        }

        private Image GetButtonImage(int index)
        {
            try
            {
                var ico = IconForButton(index, 32);
                if (ico == null) return null;
                return ico.ToBitmap();
            }
            catch { return null; }
        }

        // ----- Publish -----
        private void PublishButton(int index)
        {
            if (index < 0 || index >= _config.Buttons.Count) return;
            var btn = _config.Buttons[index];
            if (string.IsNullOrEmpty(btn.Topic))
            {
                ShowBalloon("No topic", "Button " + (index + 1) + " has no MQTT topic configured.", ToolTipIcon.Warning);
                return;
            }
            Task.Run(async () =>
            {
                bool ok = await _mqtt.PublishAsync(btn);
                if (!ok)
                {
                    _marshalForm.BeginInvoke((Action)(() =>
                        ShowBalloon("Publish failed", "Could not send '" + btn.Topic + "'. Check broker connection.", ToolTipIcon.Error)));
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

        private void ShowBalloon(string title, string text, ToolTipIcon icon)
        {
            try
            {
                if (_trayIcon != null)
                {
                    _trayIcon.BalloonTipTitle = title;
                    _trayIcon.BalloonTipText = text;
                    _trayIcon.BalloonTipIcon = icon;
                    _trayIcon.ShowBalloonTip(3500);
                }
                else if (_multiIcons != null && _multiIcons.Count > 0)
                {
                    _multiIcons[0].BalloonTipTitle = title;
                    _multiIcons[0].BalloonTipText = text;
                    _multiIcons[0].BalloonTipIcon = icon;
                    _multiIcons[0].ShowBalloonTip(3500);
                }
            }
            catch { }
        }

        public void OnExternalShowRequest()
        {
            if (_config.DisplayMode == DisplayMode.PopupPanel)
            {
                ShowPopup();
            }
            else
            {
                ShowBalloon("Already running", "Taskbar MQTT FastSwitch is already in the notification area.", ToolTipIcon.Info);
            }
        }

        public void PostToUiThread(Action action)
        {
            if (_marshalForm != null && !_marshalForm.IsDisposed)
            {
                if (_marshalForm.InvokeRequired)
                {
                    _marshalForm.BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
        }

        public new void ExitThread()
        {
            DisposeTray();
            _mqtt.Dispose();
            _popup?.Close();
            if (_appIcon != null) _appIcon.Dispose();
            if (_defaultButtonIcon != null) _defaultButtonIcon.Dispose();
            _defaultButtonBitmap?.Dispose();
            Application.Exit();
        }
    }
}
