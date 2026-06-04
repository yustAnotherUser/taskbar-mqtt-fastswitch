using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        private readonly Bitmap _mqttIcon;

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
            _mqttIcon = LoadResourceBitmap("mqtt-icon.png");

            _mqtt = new MqttService();
            _mqtt.Error += msg => ShowBalloon("MQTT", msg, ToolTipIcon.Error);
            _mqtt.StatusChanged += msg => { /* could update tooltip */ };

            BuildContextMenu();
            BuildTray();

            _mqtt.UpdateConfig(_config.Broker);
        }

        private Icon LoadCustomIcon()
        {
            if (!string.IsNullOrEmpty(_config.IconPath) && File.Exists(_config.IconPath))
            {
                try
                {
                    using (var bmp = LoadAlphaBitmap(_config.IconPath))
                    {
                        ApplyTransparency(bmp, _config);
                        using (var resized = new Bitmap(bmp, 16, 16))
                        {
                            if (_config.RoundedTrayIcon)
                            {
                                using (var rounded = RoundBitmap(resized, 3))
                                    return IconFromResizedBitmap(rounded);
                            }
                            return IconFromResizedBitmap(resized);
                        }
                    }
                }
                catch { }
            }
            if (_config.RoundedTrayIcon || _config.MakeWhiteTransparent || _config.MakeBlackTransparent)
            {
                using (var bmp = _appIcon != null ? _appIcon.ToBitmap() : SystemIcons.Application.ToBitmap())
                using (var alpha = ToAlpha(bmp))
                {
                    ApplyTransparency(alpha, _config);
                    using (var resized = new Bitmap(alpha, 16, 16))
                    {
                        if (_config.RoundedTrayIcon)
                        {
                            using (var rounded = RoundBitmap(resized, 3))
                                return IconFromResizedBitmap(rounded);
                        }
                        return IconFromResizedBitmap(resized);
                    }
                }
            }
            return _appIcon ?? SystemIcons.Application;
        }

        private static void ApplyTransparency(Bitmap bmp, AppConfig cfg)
        {
            if (!cfg.MakeWhiteTransparent && !cfg.MakeBlackTransparent) return;
            for (int x = 0; x < bmp.Width; x++)
                for (int y = 0; y < bmp.Height; y++)
                {
                    var p = bmp.GetPixel(x, y);
                    if ((cfg.MakeWhiteTransparent && p.R > 240 && p.G > 240 && p.B > 240) ||
                        (cfg.MakeBlackTransparent && p.R < 15 && p.G < 15 && p.B < 15))
                        bmp.SetPixel(x, y, Color.Transparent);
                }
        }

        private static Bitmap ToAlpha(Bitmap src)
        {
            if (src.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                return new Bitmap(src);
            var bmp = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
                g.DrawImage(src, 0, 0);
            return bmp;
        }

        // ----- Resource image loading -----
        private static Bitmap LoadResourceBitmap(string fileName)
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
                    return new Bitmap(s);
                }
            }
            catch { return null; }
        }

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
                return IconFromResizedBitmap(resized);
        }

        private static Icon IconFromResizedBitmap(Bitmap bmp)
        {
            var hIcon = bmp.GetHicon();
            var ico = (Icon)Icon.FromHandle(hIcon).Clone();
            return ico;
        }

        private Icon IconForButton(int index, int size)
        {
            if (index < 0 || index >= _config.Buttons.Count) return _defaultButtonIcon;
            var path = _config.Buttons[index].IconPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    using (var bmp = LoadAlphaBitmap(path))
                    {
                        ApplyTransparency(bmp, _config.Buttons[index]);
                        using (var resized = new Bitmap(bmp, size, size))
                        {
                            if (_config.RoundedTrayIcon)
                            {
                                using (var rounded = RoundBitmap(resized, Math.Max(1, size / 6)))
                                    return IconFromResizedBitmap(rounded);
                            }
                            return IconFromResizedBitmap(resized);
                        }
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
            var isDark = DetectDarkMode();
            var menuBack = isDark ? Color.FromArgb(45, 45, 48) : SystemColors.Control;
            var menuFore = isDark ? Color.FromArgb(220, 220, 220) : SystemColors.ControlText;
            var itemBack = isDark ? Color.FromArgb(45, 45, 48) : SystemColors.Menu;
            var itemHover = isDark ? Color.FromArgb(60, 60, 65) : SystemColors.MenuHighlight;

            _contextMenu = new ContextMenuStrip
            {
                BackColor = menuBack,
                ForeColor = menuFore,
                Renderer = new ToolStripProfessionalRenderer(new ContextMenuColors(isDark))
            };
            var miSettings = new ToolStripMenuItem("Settings\u2026") { BackColor = itemBack, ForeColor = menuFore };
            miSettings.Click += (s, e) => OpenSettings();
            var miQuit = new ToolStripMenuItem("Quit") { BackColor = itemBack, ForeColor = menuFore };
            miQuit.Click += (s, e) => ExitThread();
            _contextMenu.Opening += (s, e) => HidePopup();
            _contextMenu.Items.Add(miSettings);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(miQuit);
        }

        private static bool DetectDarkMode()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("AppsUseLightTheme");
                        if (val is int i && i == 0) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private class ContextMenuColors : ProfessionalColorTable
        {
            private readonly bool _isDark;
            public ContextMenuColors(bool isDark) { _isDark = isDark; }
            public override Color MenuItemSelected => _isDark ? Color.FromArgb(60, 60, 65) : base.MenuItemSelected;
            public override Color MenuItemSelectedGradientBegin => _isDark ? Color.FromArgb(60, 60, 65) : base.MenuItemSelectedGradientBegin;
            public override Color MenuItemSelectedGradientEnd => _isDark ? Color.FromArgb(60, 60, 65) : base.MenuItemSelectedGradientEnd;
            public override Color MenuItemPressedGradientBegin => _isDark ? Color.FromArgb(50, 50, 55) : base.MenuItemPressedGradientBegin;
            public override Color MenuItemPressedGradientEnd => _isDark ? Color.FromArgb(50, 50, 55) : base.MenuItemPressedGradientEnd;
            public override Color MenuBorder => _isDark ? Color.FromArgb(90, 90, 95) : base.MenuBorder;
            public override Color MenuItemBorder => _isDark ? Color.FromArgb(90, 90, 95) : base.MenuItemBorder;
            public override Color ToolStripDropDownBackground => _isDark ? Color.FromArgb(45, 45, 48) : base.ToolStripDropDownBackground;
            public override Color ImageMarginGradientBegin => _isDark ? Color.FromArgb(45, 45, 48) : base.ImageMarginGradientBegin;
            public override Color ImageMarginGradientMiddle => _isDark ? Color.FromArgb(45, 45, 48) : base.ImageMarginGradientMiddle;
            public override Color ImageMarginGradientEnd => _isDark ? Color.FromArgb(45, 45, 48) : base.ImageMarginGradientEnd;
            public override Color SeparatorDark => _isDark ? Color.FromArgb(90, 90, 95) : base.SeparatorDark;
            public override Color SeparatorLight => _isDark ? Color.FromArgb(45, 45, 48) : base.SeparatorLight;
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
            if (_popup != null && !_popup.IsDisposed)
            {
                _popup.Hide();
                _popup.Dispose();
            }
            _popup = null;
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
                Icon = LoadCustomIcon(),
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
                _popup = new PopupForm(_config.Buttons, GetButtonImage, PublishButton, _mqttIcon, _config.PopupSizePercent, _config.ShowTooltips, _config.ShowPayloadInTooltip);
            }
            _popup.ShowAtCursor();
        }

        private static Bitmap LoadAlphaBitmap(string path)
        {
            using (var tmp = new Bitmap(path))
            {
                if (tmp.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                    return new Bitmap(tmp);
                var bmp = new Bitmap(tmp.Width, tmp.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                    g.DrawImage(tmp, 0, 0, tmp.Width, tmp.Height);
                return bmp;
            }
        }

        private Image GetButtonImage(int index)
        {
            try
            {
                if (index < 0 || index >= _config.Buttons.Count) return null;
                var cfg = _config.Buttons[index];
                var path = cfg.IconPath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                var bmp = LoadAlphaBitmap(path);
                ApplyTransparency(bmp, cfg);
                return bmp;
            }
            catch { return null; }
        }

        private static void ApplyTransparency(Bitmap bmp, ButtonConfig cfg)
        {
            if (!cfg.MakeWhiteTransparent && !cfg.MakeBlackTransparent) return;
            for (int x = 0; x < bmp.Width; x++)
                for (int y = 0; y < bmp.Height; y++)
                {
                    var p = bmp.GetPixel(x, y);
                    if ((cfg.MakeWhiteTransparent && p.R > 240 && p.G > 240 && p.B > 240) ||
                        (cfg.MakeBlackTransparent && p.R < 15 && p.G < 15 && p.B < 15))
                        bmp.SetPixel(x, y, Color.Transparent);
                }
        }

        private static Bitmap RoundBitmap(Bitmap src, int radius)
        {
            var bmp = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            using (var path = GetRoundRect(new Rectangle(0, 0, src.Width, src.Height), radius))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.SetClip(path);
                g.DrawImage(src, 0, 0);
            }
            return bmp;
        }

        private static GraphicsPath GetRoundRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
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
