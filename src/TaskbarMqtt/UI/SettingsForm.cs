using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using TaskbarMqtt.App;
using TaskbarMqtt.Config;
using TaskbarMqtt.Mqtt;

namespace TaskbarMqtt.UI
{
    public class SettingsForm : Form
    {
        private readonly AppConfig _original;
        private AppConfig _draft;
        private readonly Action<AppConfig> _onApply;

        private Panel _tabStrip;
        private Panel _tabContent;
        private Panel _tabPageGeneral, _tabPageBroker, _tabPageButtons, _tabPageAbout;
        private List<Label> _tabLabels;
        private int _selectedTabIndex;

        // General
        private RadioButton _rbPopup, _rbMulti;
        private CheckBox _chkAutoStart, _chkShowTooltips, _chkShowPayloadInTooltip, _chkPopupStayOpen, _chkRoundedTrayIcon, _chkIconWhiteTransparent, _chkIconBlackTransparent;
        private ComboBox _cbPopupSize;
        private TextBox _txIconPath;
        private Button _btnIconBrowse;

        // Broker
        private TextBox _txHost, _txPort, _txUser, _txPass, _txClientId, _txKeepAlive, _txConnectTimeout;
        private CheckBox _chkTls, _chkInvalidCerts;
        private Button _btnTestConn;
        private Label _lblConnStatus;

        // Buttons
        private FlowLayoutPanel _btnPanel;
        private List<ButtonRow> _btnRows = new List<ButtonRow>();

        private bool _isDarkMode;
        private Color _formBack, _pageBack, _cellBack, _headerBack, _textColor, _grayText, _borderColor, _hoverBack;
        private Color _inputBack, _inputFore, _btnBack, _btnFore;

        private void ReloadTheme()
        {
            _isDarkMode = DetectDarkMode();
            _formBack = _isDarkMode ? Color.FromArgb(45, 45, 48) : SystemColors.Control;
            _pageBack = _isDarkMode ? Color.FromArgb(37, 37, 38) : SystemColors.Window;
            _cellBack = _isDarkMode ? Color.FromArgb(37, 37, 38) : Color.White;
            _headerBack = _isDarkMode ? Color.FromArgb(60, 60, 65) : Color.FromArgb(245, 245, 245);
            _textColor = _isDarkMode ? Color.FromArgb(220, 220, 220) : SystemColors.ControlText;
            _grayText = _isDarkMode ? Color.FromArgb(150, 150, 150) : Color.FromArgb(120, 120, 120);
            _borderColor = _isDarkMode ? Color.FromArgb(90, 90, 95) : SystemColors.ControlDark;
            _hoverBack = _isDarkMode ? Color.FromArgb(75, 75, 80) : Color.FromArgb(230, 230, 230);
            _inputBack = _isDarkMode ? Color.FromArgb(55, 55, 60) : SystemColors.Window;
            _inputFore = _isDarkMode ? Color.FromArgb(220, 220, 220) : SystemColors.WindowText;
            _btnBack = _isDarkMode ? Color.FromArgb(70, 70, 75) : SystemColors.Control;
            _btnFore = _isDarkMode ? Color.FromArgb(220, 220, 220) : SystemColors.ControlText;
        }

        private void ApplyTheme()
        {
            Controls.Clear();
            _btnRows.Clear();
            ApplyFormDarkMode();
            BuildUi();
            PopulateFromDraft();
        }

        private void ApplyFormDarkMode()
        {
            if (!IsHandleCreated) return;
            var val = _isDarkMode ? 1 : 0;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref val, sizeof(int));
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCPAINT = 0x0085;
        private const int WM_THEMECHANGED = 0x031A;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private void ApplyDarkWindow(Control ctl)
        {
            if (!_isDarkMode) return;
            Action apply = () =>
            {
                if (!ctl.IsHandleCreated) return;
                var val = 1;
                DwmSetWindowAttribute(ctl.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref val, sizeof(int));
                SetWindowTheme(ctl.Handle, "DarkMode_Explorer", null);
                SendMessage(ctl.Handle, WM_THEMECHANGED, IntPtr.Zero, IntPtr.Zero);
                SendMessage(ctl.Handle, WM_NCPAINT, (IntPtr)1, IntPtr.Zero);
                ctl.Invalidate(true);
            };
            ctl.HandleCreated += (s, e) => apply();
            apply();
            if (ctl is ComboBox cb)
                cb.DropDown += (s, e) =>
                {
                    var dropHwnd = FindWindowEx(cb.Handle, IntPtr.Zero, "ComboLBox", null);
                    if (dropHwnd != IntPtr.Zero)
                    {
                        var v = 1;
                        DwmSetWindowAttribute(dropHwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int));
                        SetWindowTheme(dropHwnd, "DarkMode_Explorer", null);
                        SendMessage(dropHwnd, WM_THEMECHANGED, IntPtr.Zero, IntPtr.Zero);
                    }
                };
        }

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        private static Icon BitmapToIcon(Bitmap bmp, int size)
        {
            using (var resized = new Bitmap(bmp, size, size))
                return Icon.FromHandle(resized.GetHicon());
        }

        private static Icon IconFromResizedBitmap(Bitmap bmp)
        {
            return Icon.FromHandle(bmp.GetHicon());
        }

        private static Bitmap RoundBitmap(Bitmap src, int radius)
        {
            var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
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

        private static Bitmap LoadAlphaBitmap(string path)
        {
            using (var tmp = new Bitmap(path))
            {
                if (tmp.PixelFormat == PixelFormat.Format32bppArgb)
                    return new Bitmap(tmp);
                var bmp = new Bitmap(tmp.Width, tmp.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                    g.DrawImage(tmp, 0, 0, tmp.Width, tmp.Height);
                return bmp;
            }
        }

        private static Bitmap ToAlpha(Bitmap src)
        {
            var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
                g.DrawImage(src, 0, 0, src.Width, src.Height);
            return bmp;
        }

        private static void ApplyTransparency(Bitmap bmp, bool makeWhite, bool makeBlack)
        {
            if (!makeWhite && !makeBlack) return;
            for (int x = 0; x < bmp.Width; x++)
                for (int y = 0; y < bmp.Height; y++)
                {
                    var p = bmp.GetPixel(x, y);
                    if ((makeWhite && p.R > 240 && p.G > 240 && p.B > 240) ||
                        (makeBlack && p.R < 15 && p.G < 15 && p.B < 15))
                        bmp.SetPixel(x, y, Color.Transparent);
                }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyFormDarkMode();
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

        public SettingsForm(AppConfig config, Action<AppConfig> onApply)
        {
            _original = config;
            _draft = CloneConfig(config);
            _onApply = onApply;

            ReloadTheme();
            BuildUi();
            PopulateFromDraft();

            Microsoft.Win32.SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == UserPreferenceCategory.General)
                {
                    var wasDark = _isDarkMode;
                    ReloadTheme();
                    if (_isDarkMode != wasDark)
                        ApplyTheme();
                }
            };
        }

        private static AppConfig CloneConfig(AppConfig src)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(src);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<AppConfig>(json);
        }

        private void BuildUi()
        {
            Text = "Taskbar MQTT \u2014 Settings";
            ClientSize = new Size(720, 560);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            MinimumSize = new Size(640, 480);
            ShowInTaskbar = true;
            Font = new Font("Segoe UI", 9F);
            BackColor = _formBack;
            ForeColor = _textColor;

            try
            {
                var makeWhite = _draft.MakeWhiteTransparent;
                var makeBlack = _draft.MakeBlackTransparent;
                var round = _draft.RoundedTrayIcon;

                Bitmap LoadIconBitmap()
                {
                    if (!string.IsNullOrEmpty(_draft.IconPath) && File.Exists(_draft.IconPath))
                    {
                        var ext = Path.GetExtension(_draft.IconPath)?.ToLowerInvariant();
                        if (ext == ".ico")
                            using (var tmp = new Icon(_draft.IconPath))
                                return tmp.ToBitmap();
                        return LoadAlphaBitmap(_draft.IconPath);
                    }
                    var asm = Assembly.GetExecutingAssembly();
                    var resName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(".app.ico", StringComparison.OrdinalIgnoreCase));
                    if (resName != null)
                        using (var s = asm.GetManifestResourceStream(resName))
                            if (s != null)
                                using (var tmp = new Icon(s))
                                    return tmp.ToBitmap();
                    return null;
                }

                using (var raw = LoadIconBitmap())
                {
                    if (raw == null) return;
                    using (var alpha = ToAlpha(raw))
                    {
                        ApplyTransparency(alpha, makeWhite, makeBlack);
                        using (var resized = new Bitmap(alpha, 16, 16))
                        {
                            if (round)
                                using (var rounded = RoundBitmap(resized, 3))
                                    Icon = IconFromResizedBitmap(rounded);
                            else
                                Icon = IconFromResizedBitmap(resized);
                        }
                    }
                }
            }
            catch { }

            // Tab strip
            _tabStrip = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = _formBack };

            var tabNames = new[] { "General", "Broker", "Buttons", "About" };
            _tabLabels = new List<Label>();
            int xPos = 0;
            for (int i = 0; i < tabNames.Length; i++)
            {
                var sel = i == 0;
                var lbl = new Label
                {
                    Text = tabNames[i],
                    AutoSize = false,
                    Width = 110,
                    Height = 32,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = sel ? _textColor : _grayText,
                    BackColor = sel ? _cellBack : _formBack,
                    Font = new Font("Segoe UI", 9F, sel ? FontStyle.Bold : FontStyle.Regular),
                    Cursor = Cursors.Hand
                };
                lbl.Location = new Point(xPos, 0);
                var idx = i;
                lbl.Click += (s, e) => SelectTab(idx);
                lbl.MouseEnter += (s, e) => { if (idx != _selectedTabIndex) lbl.BackColor = _hoverBack; };
                lbl.MouseLeave += (s, e) => { if (idx != _selectedTabIndex) lbl.BackColor = _formBack; };
                _tabLabels.Add(lbl);
                _tabStrip.Controls.Add(lbl);
                xPos += lbl.Width;
            }
            var borderLine = new Panel { BackColor = _borderColor, Height = 1, Dock = DockStyle.Bottom };
            _tabStrip.Controls.Add(borderLine);

            // Content panel
            _tabContent = new Panel { Dock = DockStyle.Fill, BackColor = _cellBack };
            _tabPageGeneral = new Panel { Dock = DockStyle.Fill, BackColor = _cellBack };
            _tabPageBroker = new Panel { Dock = DockStyle.Fill, BackColor = _cellBack };
            _tabPageButtons = new Panel { Dock = DockStyle.Fill, BackColor = _cellBack };
            _tabPageAbout = new Panel { Dock = DockStyle.Fill, BackColor = _cellBack };
            _tabContent.Controls.Add(_tabPageAbout);
            _tabContent.Controls.Add(_tabPageButtons);
            _tabContent.Controls.Add(_tabPageBroker);
            _tabContent.Controls.Add(_tabPageGeneral);
            ShowPage(0);

            Controls.Add(_tabContent);
            Controls.Add(_tabStrip);

            BuildGeneralTab();
            BuildBrokerTab();
            BuildButtonsTab();
            BuildAboutTab();

            var btnApply = new Button { Text = "Apply", Width = 86, Height = 26, BackColor = _btnBack, ForeColor = _btnFore, FlatStyle = FlatStyle.Flat };
            btnApply.FlatAppearance.BorderColor = _borderColor;
            var btnOk = new Button { Text = "OK", Width = 86, Height = 26, BackColor = _btnBack, ForeColor = _btnFore, FlatStyle = FlatStyle.Flat };
            btnOk.FlatAppearance.BorderColor = _borderColor;
            var btnCancel = new Button { Text = "Cancel", Width = 86, Height = 26, BackColor = _btnBack, ForeColor = _btnFore, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat };
            btnCancel.FlatAppearance.BorderColor = _borderColor;

            btnOk.Click += (s, e) => { if (Apply()) { DialogResult = DialogResult.OK; Close(); } };
            btnApply.Click += (s, e) => { Apply(); };

            var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = Color.Transparent };
            btnApply.Location = new Point(btnPanel.Width - 86 * 3 - 26, 10);
            btnOk.Location = new Point(btnPanel.Width - 86 * 2 - 18, 10);
            btnCancel.Location = new Point(btnPanel.Width - 86 - 10, 10);
            btnPanel.Resize += (s, e) =>
            {
                btnApply.Location = new Point(btnPanel.Width - 86 * 3 - 26, 10);
                btnOk.Location = new Point(btnPanel.Width - 86 * 2 - 18, 10);
                btnCancel.Location = new Point(btnPanel.Width - 86 - 10, 10);
            };
            btnPanel.Controls.AddRange(new[] { btnApply, btnOk, btnCancel });
            Controls.Add(btnPanel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void BuildGeneralTab()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 9,
                Padding = new Padding(20, 20, 20, 8),
                BackColor = _cellBack
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 9; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _rbPopup = new RadioButton { Text = "Popup panel (left-click to open)", AutoSize = true, ForeColor = _textColor };
            _rbMulti = new RadioButton { Text = "One tray icon per button", AutoSize = true, ForeColor = _textColor };
            _chkAutoStart = new CheckBox { Text = "Launch on Windows startup", AutoSize = true, ForeColor = _textColor };
            _chkShowTooltips = new CheckBox { Text = "Show tooltips in popup", AutoSize = true, ForeColor = _textColor };
            _chkShowPayloadInTooltip = new CheckBox { Text = "Show payload in tooltip", AutoSize = true, ForeColor = _textColor };
            _chkPopupStayOpen = new CheckBox { Text = "Popup stays open on button click", AutoSize = true, ForeColor = _textColor };

            var modePanel = new Panel { AutoSize = true };
            modePanel.Controls.Add(_rbPopup);
            modePanel.Controls.Add(_rbMulti);
            _rbMulti.Top = _rbPopup.Bottom + 4;

            layout.Controls.Add(new Label { Text = "Display mode:", AutoSize = true, Anchor = AnchorStyles.Top, Padding = new Padding(0, 3, 0, 0), ForeColor = _textColor }, 0, 0);
            layout.Controls.Add(modePanel, 1, 0);
            layout.Controls.Add(new Label { Text = "Autostart:", AutoSize = true, Anchor = AnchorStyles.Top, Padding = new Padding(0, 3, 0, 0), ForeColor = _textColor }, 0, 1);
            layout.Controls.Add(_chkAutoStart, 1, 1);

            _cbPopupSize = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Anchor = AnchorStyles.Left | AnchorStyles.Top, BackColor = _inputBack, ForeColor = _inputFore, DrawMode = DrawMode.OwnerDrawFixed };
            _cbPopupSize.DrawItem += ComboDrawItem;
            ApplyDarkWindow(_cbPopupSize);
            for (int v = 25; v <= 200; v += 25)
                _cbPopupSize.Items.Add(v + "%");
            layout.Controls.Add(new Label { Text = "Popup size:", AutoSize = true, Anchor = AnchorStyles.Top, Padding = new Padding(0, 3, 0, 0), ForeColor = _textColor }, 0, 2);
            layout.Controls.Add(_cbPopupSize, 1, 2);

            _txIconPath = new TextBox { Width = 220, Anchor = AnchorStyles.Left, BackColor = _inputBack, ForeColor = _inputFore };
            _btnIconBrowse = new Button { Text = "Browse\u2026", Width = 75, Height = 23, Margin = new Padding(6, 0, 0, 0), BackColor = _btnBack, ForeColor = _btnFore, FlatStyle = FlatStyle.Flat };
            _btnIconBrowse.FlatAppearance.BorderColor = _borderColor;
            _btnIconBrowse.Click += OnIconBrowse;
            var btnClearIcon = new Button { Text = "Clear", Width = 55, Height = 23, Margin = new Padding(4, 0, 0, 0), BackColor = _btnBack, ForeColor = _btnFore, FlatStyle = FlatStyle.Flat };
            btnClearIcon.FlatAppearance.BorderColor = _borderColor;
            btnClearIcon.Click += (s, e) => _txIconPath.Text = "";
            var iconPanel = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
            iconPanel.Controls.Add(_txIconPath);
            iconPanel.Controls.Add(_btnIconBrowse);
            iconPanel.Controls.Add(btnClearIcon);
            layout.Controls.Add(new Label { Text = "Custom tray icon:", AutoSize = true, Anchor = AnchorStyles.Top, Padding = new Padding(0, 3, 0, 0), ForeColor = _textColor }, 0, 3);
            layout.Controls.Add(iconPanel, 1, 3);
            layout.Controls.Add(new Label { Text = "Tooltips:", AutoSize = true, Anchor = AnchorStyles.Top, Padding = new Padding(0, 3, 0, 0), ForeColor = _textColor }, 0, 4);
            layout.Controls.Add(_chkShowTooltips, 1, 4);
            layout.Controls.Add(new Label { Text = "", AutoSize = true, Anchor = AnchorStyles.Top, Padding = new Padding(0, 3, 0, 0), ForeColor = _textColor }, 0, 5);
            layout.Controls.Add(_chkShowPayloadInTooltip, 1, 5);
            layout.Controls.Add(new Label { Text = "Popup behavior:", AutoSize = true, Anchor = AnchorStyles.Top, Padding = new Padding(0, 3, 0, 0), ForeColor = _textColor }, 0, 6);
            layout.Controls.Add(_chkPopupStayOpen, 1, 6);

            _chkShowTooltips.CheckedChanged += (s, e) => _chkShowPayloadInTooltip.Enabled = _chkShowTooltips.Checked;

            _chkRoundedTrayIcon = new CheckBox { Text = "Rounded tray icon", AutoSize = true, ForeColor = _textColor };
            layout.Controls.Add(new Label { Text = "Appearance:", AutoSize = true, Anchor = AnchorStyles.Top, Padding = new Padding(0, 3, 0, 0), ForeColor = _textColor }, 0, 7);
            layout.Controls.Add(_chkRoundedTrayIcon, 1, 7);

            _chkIconWhiteTransparent = new CheckBox { Text = "White\u2192Transparent (tray icon)", AutoSize = true, ForeColor = _textColor };
            _chkIconBlackTransparent = new CheckBox { Text = "Black\u2192Transparent (tray icon)", AutoSize = true, ForeColor = _textColor };
            layout.Controls.Add(new Label { Text = "", AutoSize = true }, 0, 8);
            layout.Controls.Add(_chkIconWhiteTransparent, 1, 8);

            layout.Controls.Add(new Label { Text = "", AutoSize = true }, 0, 9);
            layout.Controls.Add(_chkIconBlackTransparent, 1, 9);

            _tabPageGeneral.Controls.Add(layout);
        }

        private void OnIconBrowse(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog { Filter = "Icon files|*.ico;*.png;*.jpg;*.jpeg;*.bmp", Title = "Select custom tray icon" })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                    _txIconPath.Text = dlg.FileName;
            }
        }

        private void BuildBrokerTab()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 9,
                Padding = new Padding(20, 20, 20, 8),
                BackColor = _cellBack
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 9; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            _txHost = new TextBox { Width = 260, Anchor = AnchorStyles.Left, BackColor = _inputBack, ForeColor = _inputFore };
            _txPort = new TextBox { Width = 70, Anchor = AnchorStyles.Left, BackColor = _inputBack, ForeColor = _inputFore };
            _txUser = new TextBox { Width = 200, Anchor = AnchorStyles.Left, BackColor = _inputBack, ForeColor = _inputFore };
            _txPass = new TextBox { Width = 200, Anchor = AnchorStyles.Left, UseSystemPasswordChar = true, BackColor = _inputBack, ForeColor = _inputFore };
            _txClientId = new TextBox { Width = 260, Anchor = AnchorStyles.Left, Text = "TaskbarMqtt", BackColor = _inputBack, ForeColor = _inputFore };
            _txKeepAlive = new TextBox { Width = 70, Anchor = AnchorStyles.Left, BackColor = _inputBack, ForeColor = _inputFore };
            _txConnectTimeout = new TextBox { Width = 70, Anchor = AnchorStyles.Left, BackColor = _inputBack, ForeColor = _inputFore };
            _chkTls = new CheckBox { Text = "Use TLS", AutoSize = true, ForeColor = _textColor };
            _chkInvalidCerts = new CheckBox { Text = "Allow self-signed certificates", AutoSize = true, Checked = true, ForeColor = _textColor };

            var tlsPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0) };
            tlsPanel.Controls.Add(_chkTls);
            tlsPanel.Controls.Add(_chkInvalidCerts);

            _btnTestConn = new Button { Text = "Test Connection", Width = 130, Height = 26, BackColor = _btnBack, ForeColor = _btnFore, FlatStyle = FlatStyle.Flat };
            _btnTestConn.FlatAppearance.BorderColor = _borderColor;
            _btnTestConn.Click += OnTestConnection;
            _lblConnStatus = new Label { AutoSize = true, ForeColor = _grayText, Padding = new Padding(0, 5, 0, 0) };

            layout.Controls.Add(new Label { Text = "Host:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = _textColor }, 0, 0);
            layout.Controls.Add(_txHost, 1, 0);
            layout.Controls.Add(new Label { Text = "Port:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = _textColor }, 0, 1);
            layout.Controls.Add(_txPort, 1, 1);
            layout.Controls.Add(new Label { Text = "Username:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = _textColor }, 0, 2);
            layout.Controls.Add(_txUser, 1, 2);
            layout.Controls.Add(new Label { Text = "Password:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = _textColor }, 0, 3);
            layout.Controls.Add(_txPass, 1, 3);
            layout.Controls.Add(new Label { Text = "Client ID:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = _textColor }, 0, 4);
            layout.Controls.Add(_txClientId, 1, 4);
            layout.Controls.Add(new Label { Text = "Keep alive (s):", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = _textColor }, 0, 5);
            layout.Controls.Add(_txKeepAlive, 1, 5);
            layout.Controls.Add(new Label { Text = "Connect timeout (s):", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = _textColor }, 0, 6);
            layout.Controls.Add(_txConnectTimeout, 1, 6);
            layout.Controls.Add(new Label { Text = "Encryption (TLS/SSL):", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = _textColor }, 0, 7);
            layout.Controls.Add(tlsPanel, 1, 7);
            layout.Controls.Add(new Label { Text = "", Anchor = AnchorStyles.Left }, 0, 8);
            layout.Controls.Add(_btnTestConn, 1, 8);
            layout.Controls.Add(new Label { Text = "", Anchor = AnchorStyles.Left }, 0, 9);
            layout.Controls.Add(_lblConnStatus, 1, 9);

            _tabPageBroker.Controls.Add(layout);
        }

        private void BuildButtonsTab()
        {
            _btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(12, 12, 12, 4),
                BackColor = _cellBack
            };
            _btnPanel.Resize += (s, e) => UpdateRowWidths();
            ApplyDarkWindow(_btnPanel);
            _tabPageButtons.Controls.Add(_btnPanel);

            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = _cellBack };
            var btnAdd = new Button { Text = "+ Add Button", Width = 110, Height = 26, Left = 14, Top = 9, BackColor = _btnBack, ForeColor = _btnFore, FlatStyle = FlatStyle.Flat };
            btnAdd.FlatAppearance.BorderColor = _borderColor;
            btnAdd.Click += OnAddButton;
            bottomPanel.Controls.Add(btnAdd);
            _tabPageButtons.Controls.Add(bottomPanel);
        }

        private void BuildAboutTab()
        {
            var box = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _pageBack,
                ForeColor = _textColor
            };

            var lines = new[]
            {
                "Taskbar MQTT FastSwitch",
                "",
                "Version 1.7",
                "",
                "Created by MiniMax V3",
                "Made working by OpenCode's Big Pickle",
                "Supervised by yustAnotherUser",
                "",
                "A lightweight tray application that publishes",
                "pre-configured MQTT messages at the click of a button.",
            };

            int y = 10;
            using (var boldFont = new Font("Segoe UI", 12F, FontStyle.Bold))
            using (var regFont = new Font("Segoe UI", 9F))
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    var lbl = new Label
                    {
                        Text = lines[i],
                        AutoSize = true,
                        TextAlign = ContentAlignment.TopCenter,
                        ForeColor = _textColor,
                        Font = i == 0 ? boldFont : regFont,
                        Padding = new Padding(0),
                        Margin = new Padding(0)
                    };
                    lbl.Location = new Point((box.Width - lbl.Width) / 2, y);
                    box.Controls.Add(lbl);
                    var h = string.IsNullOrEmpty(lines[i]) ? 6 : lbl.Height;
                    y += h + (i == 0 ? 6 : 2);
                }
            }

            _tabPageAbout.Controls.Add(box);
        }

        private void SelectTab(int index)
        {
            if (index == _selectedTabIndex) return;
            _selectedTabIndex = index;
            for (int i = 0; i < _tabLabels.Count; i++)
            {
                var sel = i == index;
                _tabLabels[i].ForeColor = sel ? _textColor : _grayText;
                _tabLabels[i].BackColor = sel ? _cellBack : _formBack;
                _tabLabels[i].Font = new Font("Segoe UI", 9F, sel ? FontStyle.Bold : FontStyle.Regular);
            }
            ShowPage(index);
        }

        private void ShowPage(int index)
        {
            _tabPageGeneral.Visible = index == 0;
            _tabPageBroker.Visible = index == 1;
            _tabPageButtons.Visible = index == 2;
            _tabPageAbout.Visible = index == 3;
        }

        private void ComboDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var cb = (ComboBox)sender;
            var back = (e.State & DrawItemState.Selected) != 0 ? _hoverBack : _inputBack;
            using (var b = new SolidBrush(back))
                e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, cb.Items[e.Index].ToString(), cb.Font, e.Bounds, _inputFore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            e.DrawFocusRectangle();
        }

        private void UpdateRowWidths()
        {
            int w = _btnPanel.ClientSize.Width - _btnPanel.Padding.Horizontal;
            if (w < 100) return;
            foreach (var row in _btnRows)
                row.SetWidth(w);
        }

        private void OnAddButton(object sender, EventArgs e)
        {
            if (_btnRows.Count >= AppConfig.MaxButtons)
            {
                MessageBox.Show("Maximum " + AppConfig.MaxButtons + " buttons allowed.", "Limit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            AddButtonRow();
            RefreshButtonStates();
        }

        private void AddButtonRow()
        {
            var row = new ButtonRow(this, _btnRows.Count, RemoveButtonRow);
            _btnRows.Add(row);
            _btnPanel.Controls.Add(row);
            UpdateRowWidths();
        }

        private void RemoveButtonRow(ButtonRow row)
        {
            var idx = _btnRows.IndexOf(row);
            if (idx < 0) return;
            _btnRows.RemoveAt(idx);
            _btnPanel.Controls.Remove(row);
            row.Dispose();
            RefreshButtonIndices();
            RefreshButtonStates();
        }

        private void RefreshButtonIndices()
        {
            for (int i = 0; i < _btnRows.Count; i++)
                _btnRows[i].SetIndex(i);
        }

        private void RefreshButtonStates()
        {
            bool canRemove = _btnRows.Count > 1;
            foreach (var r in _btnRows)
                r.SetRemoveEnabled(canRemove);
        }

        private void PopulateFromDraft()
        {
            _rbPopup.Checked = _draft.DisplayMode == DisplayMode.PopupPanel;
            _rbMulti.Checked = _draft.DisplayMode == DisplayMode.MultipleIcons;
            _chkAutoStart.Checked = _draft.StartWithWindows;

            int pct = _draft.PopupSizePercent;
            pct = ((pct + 12) / 25) * 25;
            if (pct < 25) pct = 25;
            if (pct > 200) pct = 200;
            _cbPopupSize.SelectedItem = pct + "%";
            _chkShowTooltips.Checked = _draft.ShowTooltips;
            _chkShowPayloadInTooltip.Checked = _draft.ShowPayloadInTooltip;
            _chkShowPayloadInTooltip.Enabled = _draft.ShowTooltips;
            _chkPopupStayOpen.Checked = _draft.PopupStaysOpen;
            _chkRoundedTrayIcon.Checked = _draft.RoundedTrayIcon;
            _chkIconWhiteTransparent.Checked = _draft.MakeWhiteTransparent;
            _chkIconBlackTransparent.Checked = _draft.MakeBlackTransparent;

            _txHost.Text = _draft.Broker.Host ?? "";
            _txPort.Text = _draft.Broker.Port.ToString();
            _txUser.Text = _draft.Broker.Username ?? "";
            _txPass.Text = _draft.Broker.Password ?? "";
            _txClientId.Text = string.IsNullOrEmpty(_draft.Broker.ClientId) ? "TaskbarClient" : _draft.Broker.ClientId;
            _txKeepAlive.Text = _draft.Broker.KeepAliveSeconds.ToString();
            _txConnectTimeout.Text = _draft.Broker.ConnectTimeoutSeconds.ToString();
            _txIconPath.Text = _draft.IconPath ?? "";
            _chkTls.Checked = _draft.Broker.UseTls;
            _chkInvalidCerts.Checked = _draft.Broker.AllowInvalidCerts;

            _btnRows.Clear();
            _btnPanel.SuspendLayout();
            _btnPanel.Controls.Clear();
            int count = Math.Max(1, _draft.Buttons.Count);
            for (int i = 0; i < count; i++)
            {
                var row = new ButtonRow(this, i, RemoveButtonRow);
                if (i < _draft.Buttons.Count)
                    row.Bind(_draft.Buttons[i]);
                _btnPanel.Controls.Add(row);
                _btnRows.Add(row);
            }
            _btnPanel.ResumeLayout();
            UpdateRowWidths();
            RefreshButtonStates();
        }

        private bool CollectToDraft()
        {
            _draft.ButtonCount = _btnRows.Count;
            _draft.DisplayMode = _rbPopup.Checked ? DisplayMode.PopupPanel : DisplayMode.MultipleIcons;
            _draft.StartWithWindows = _chkAutoStart.Checked;
            _draft.ShowTooltips = _chkShowTooltips.Checked;
            _draft.ShowPayloadInTooltip = _chkShowPayloadInTooltip.Checked;
            _draft.PopupStaysOpen = _chkPopupStayOpen.Checked;
            _draft.IconPath = _txIconPath.Text.Trim();
            _draft.RoundedTrayIcon = _chkRoundedTrayIcon.Checked;
            _draft.MakeWhiteTransparent = _chkIconWhiteTransparent.Checked;
            _draft.MakeBlackTransparent = _chkIconBlackTransparent.Checked;
            var sel = _cbPopupSize.SelectedItem?.ToString();
            if (sel != null && int.TryParse(sel.TrimEnd('%'), out var pct))
                _draft.PopupSizePercent = pct;

            if (!int.TryParse(_txPort.Text, out var port) || port <= 0 || port > 65535)
            {
                MessageBox.Show("Port must be a number between 1 and 65535.", "Invalid Port", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (!int.TryParse(_txKeepAlive.Text, out var ka) || ka <= 0)
            {
                MessageBox.Show("Keep alive must be a positive number.", "Invalid Value", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            _draft.Broker.Host = _txHost.Text.Trim();
            _draft.Broker.Port = port;
            _draft.Broker.Username = _txUser.Text;
            _draft.Broker.Password = _txPass.Text;
            _draft.Broker.ClientId = _txClientId.Text.Trim();
            _draft.Broker.KeepAliveSeconds = ka;
            if (!int.TryParse(_txConnectTimeout.Text, out var ct) || ct <= 0)
            {
                MessageBox.Show("Connect timeout must be a positive number.", "Invalid Value", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            _draft.Broker.ConnectTimeoutSeconds = ct;
            _draft.Broker.UseTls = _chkTls.Checked;
            _draft.Broker.AllowInvalidCerts = _chkInvalidCerts.Checked;

            _draft.Buttons.Clear();
            foreach (var row in _btnRows)
                _draft.Buttons.Add(row.Collect());
            _draft.Normalize();
            return true;
        }

        private bool Apply()
        {
            if (!CollectToDraft()) return false;

            if (!ConfigStore.Save(_draft))
            {
                MessageBox.Show("Failed to write config.json.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            AutoStart.SetEnabled(_draft.StartWithWindows);
            _onApply?.Invoke(_draft);
            return true;
        }

        private void OnTestConnection(object sender, EventArgs e)
        {
            if (!CollectToDraft()) return;
            _lblConnStatus.Text = "Testing\u2026";
            _lblConnStatus.ForeColor = SystemColors.ControlDarkDark;
            _btnTestConn.Enabled = false;

            var broker = _draft.Broker;
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            Task.Run(async () =>
            {
                bool ok = false;
                string err = "";
                MqttService test = null;
                try
                {
                    test = new MqttService();
                    test.UpdateConfig(broker);
                    await test.StartAsync();
                    ok = test.IsConnected;
                    if (!ok) err = "Could not connect within timeout";
                }
                catch (Exception ex) { err = ex.Message; }
                finally
                {
                    try { test?.Dispose(); } catch { }
                }
                sw.Stop();
                BeginInvoke((Action)(() =>
                {
                    _btnTestConn.Enabled = true;
                    if (ok)
                    {
                        _lblConnStatus.Text = "OK \u2014 connected in " + sw.ElapsedMilliseconds + " ms";
                        _lblConnStatus.ForeColor = Color.DarkGreen;
                    }
                    else
                    {
                        _lblConnStatus.Text = "Failed: " + err;
                        _lblConnStatus.ForeColor = Color.DarkRed;
                    }
                }));
            });
        }

        // -------- Button row --------
        private class ButtonRow : UserControl
        {
            private readonly SettingsForm _parent;
            private int _index;
            private TextBox _label, _topic, _payload, _iconPath;
            private ComboBox _qos;
            private CheckBox _retain, _chkWhiteTransparent, _chkBlackTransparent;
            private Button _browseBtn, _clearBtn, _removeBtn;
            private Label _headerLbl;
            private Label _lblDesc, _lblTopic, _lblQos, _lblPayload, _lblIcon;
            private PictureBox _iconPreview;
            private Panel _header;
            private readonly ToolTip _tooltip;
            private readonly Action<ButtonRow> _removeAction;

            public ButtonRow(SettingsForm parent, int index, Action<ButtonRow> removeAction)
            {
                _parent = parent;
                _index = index;
                _removeAction = removeAction;
                _tooltip = new ToolTip();
                Height = 175;
                Build();
            }

            private void Build()
            {
                BorderStyle = BorderStyle.None;
                BackColor = _parent._cellBack;
                var margin = 6;

                _header = new Panel
                {
                    Height = 28,
                    BackColor = _parent._headerBack
                };

                _headerLbl = new Label
                {
                    Text = "Button " + (_index + 1),
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    AutoSize = true,
                    ForeColor = _parent._textColor
                };
                _header.Controls.Add(_headerLbl);

                _removeBtn = new Button
                {
                    Text = "\u2715",
                    Width = 22,
                    Height = 22,
                    FlatStyle = FlatStyle.Flat,
                    TabStop = false,
                    ForeColor = _parent._grayText
                };
                _removeBtn.FlatAppearance.BorderSize = 0;
                _removeBtn.FlatAppearance.MouseOverBackColor = _parent._hoverBack;
                _removeBtn.Click += (s, e) => _removeAction?.Invoke(this);
                _header.Controls.Add(_removeBtn);
                Controls.Add(_header);

                _iconPreview = new PictureBox
                {
                    Width = 44,
                    Height = 44,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = _parent._cellBack,
                    BorderStyle = BorderStyle.FixedSingle
                };
                Controls.Add(_iconPreview);

                _lblDesc = new Label { Text = "Description:", AutoSize = true, ForeColor = _parent._textColor };
                _lblTopic = new Label { Text = "Topic:", AutoSize = true, ForeColor = _parent._textColor };
                _lblQos = new Label { Text = "QoS:", AutoSize = true, ForeColor = _parent._textColor };
                _lblPayload = new Label { Text = "Payload:", AutoSize = true, ForeColor = _parent._textColor };
                _lblIcon = new Label { Text = "Icon (ICO / PNG / JPG / BMP):", AutoSize = true, ForeColor = _parent._textColor };

                _label = new TextBox { Height = 22, BackColor = _parent._inputBack, ForeColor = _parent._inputFore };
                _topic = new TextBox { Height = 22, BackColor = _parent._inputBack, ForeColor = _parent._inputFore };
                _qos = new ComboBox { Height = 22, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = _parent._inputBack, ForeColor = _parent._inputFore, DrawMode = DrawMode.OwnerDrawFixed };
                _qos.DrawItem += _parent.ComboDrawItem;
                _parent.ApplyDarkWindow(_qos);
                _qos.Items.AddRange(new object[] { "0", "1", "2" });
                _qos.SelectedIndex = 0;

                _payload = new TextBox { Height = 22, BackColor = _parent._inputBack, ForeColor = _parent._inputFore };
                _retain = new CheckBox { Text = "Retain", AutoSize = true, ForeColor = _parent._textColor };

                _iconPath = new TextBox { Height = 22, ReadOnly = true, BackColor = _parent._inputBack, ForeColor = _parent._inputFore };
                _browseBtn = new Button { Text = "Browse\u2026", Height = 23, BackColor = _parent._btnBack, ForeColor = _parent._btnFore, FlatStyle = FlatStyle.Flat };
                _browseBtn.FlatAppearance.BorderColor = _parent._borderColor;
                _browseBtn.Click += OnBrowse;
                _clearBtn = new Button { Text = "Clear", Height = 23, BackColor = _parent._btnBack, ForeColor = _parent._btnFore, FlatStyle = FlatStyle.Flat };
                _clearBtn.FlatAppearance.BorderColor = _parent._borderColor;
                _clearBtn.Click += (s, e) => { _iconPath.Text = ""; _iconPreview.Image = null; };

                _chkWhiteTransparent = new CheckBox { Text = "White\u2192Transparent", AutoSize = true, ForeColor = _parent._textColor };
                _chkBlackTransparent = new CheckBox { Text = "Black\u2192Transparent", AutoSize = true, ForeColor = _parent._textColor };

                Controls.AddRange(new Control[] {
                    _lblDesc, _lblTopic, _lblQos, _lblPayload, _lblIcon,
                    _label, _topic, _qos, _payload, _retain,
                    _iconPath, _browseBtn, _clearBtn,
                    _chkWhiteTransparent, _chkBlackTransparent
                });
            }

            public void SetWidth(int w)
            {
                Width = w;
                int pad = 10;
                int rowW = w - pad * 2;

                _header.Width = w;
                _header.Height = 26;
                _headerLbl.Location = new Point(pad, 4);
                _removeBtn.Location = new Point(w - pad - 22, 2);

                int row1Top = 35;
                int iconSize = 44;
                int labelY = row1Top + 3;

                _iconPreview.Location = new Point(pad, row1Top);

                int fieldGap = 6;
                int labelGap = 4;
                int x = pad + iconSize + fieldGap;

                int descLblW = _lblDesc.Width;
                int topicLblW = _lblTopic.Width;
                int qosLblW = _lblQos.Width;

                int retainW = 70;
                int qosDW = 54;
                int avail = rowW - iconSize - fieldGap - descLblW - labelGap - topicLblW - labelGap - qosLblW - labelGap - qosDW - fieldGap - retainW;
                int labelW = (int)(avail * 0.45);
                if (labelW < 80) labelW = 80;
                int topicW = avail - labelW;
                if (topicW < 100) topicW = 100;

                _lblDesc.Location = new Point(x, labelY);
                x += descLblW + labelGap;
                _label.Location = new Point(x, row1Top);
                _label.Width = labelW;
                x += labelW + fieldGap;

                _lblTopic.Location = new Point(x, labelY);
                x += topicLblW + labelGap;
                _topic.Location = new Point(x, row1Top);
                _topic.Width = topicW;
                x += topicW + fieldGap;

                _lblQos.Location = new Point(x, labelY);
                x += qosLblW + labelGap;
                _qos.Location = new Point(x, row1Top);
                _qos.Width = qosDW;
                x += qosDW + fieldGap;

                _retain.Location = new Point(x, row1Top + 1);

                int row2Top = row1Top + 30;
                x = pad + iconSize + fieldGap;

                int payloadLblW = _lblPayload.Width;
                int payloadW = Math.Max(120, rowW - iconSize - fieldGap - x - payloadLblW - labelGap);
                _lblPayload.Location = new Point(x, labelY + 30);
                x += payloadLblW + labelGap;
                _payload.Location = new Point(x, row2Top);
                _payload.Width = payloadW;

                int row3Top = row2Top + 30;
                x = pad + iconSize + fieldGap;

                int iconLblW = _lblIcon.Width;
                int iconPathW = Math.Max(100, rowW - iconSize - fieldGap - x - iconLblW - labelGap - 70 - 60 - fieldGap * 2);
                _lblIcon.Location = new Point(x, labelY + 60);
                x += iconLblW + labelGap;
                _iconPath.Location = new Point(x, row3Top);
                _iconPath.Width = iconPathW;

                x += iconPathW + fieldGap;
                _browseBtn.Location = new Point(x, row3Top - 1);
                _browseBtn.Width = 70;

                x += 70 + fieldGap;
                _clearBtn.Location = new Point(x, row3Top - 1);
                _clearBtn.Width = 60;

                int row4Top = row3Top + 30;
                _chkWhiteTransparent.Location = new Point(pad + iconSize + fieldGap, row4Top);
                _chkBlackTransparent.Location = new Point(_chkWhiteTransparent.Right + 12, row4Top);
            }

            private void OnBrowse(object sender, EventArgs e)
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Title = "Choose icon for button " + (_index + 1);
                    dlg.Filter = "Icons & Images (*.ico;*.png;*.jpg;*.jpeg;*.bmp)|*.ico;*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*";
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        _iconPath.Text = dlg.FileName;
                        try
                        {
                            using (var bmp = new Bitmap(dlg.FileName))
                                _iconPreview.Image = new Bitmap(bmp, 44, 44);
                        }
                        catch { _iconPreview.Image = null; }
                    }
                }
            }

            public void SetIndex(int index)
            {
                _index = index;
                _headerLbl.Text = "Button " + (index + 1);
            }

            public void SetRemoveEnabled(bool enabled)
            {
                _removeBtn.Visible = enabled;
            }

            public void Bind(ButtonConfig cfg)
            {
                if (cfg == null) cfg = new ButtonConfig();
                _label.Text = cfg.Label ?? "";
                _topic.Text = cfg.Topic ?? "";
                _payload.Text = cfg.Payload ?? "";
                _qos.SelectedIndex = Math.Max(0, Math.Min(2, cfg.Qos));
                _retain.Checked = cfg.Retain;
                _iconPath.Text = cfg.IconPath ?? "";
                _chkWhiteTransparent.Checked = cfg.MakeWhiteTransparent;
                _chkBlackTransparent.Checked = cfg.MakeBlackTransparent;
                if (!string.IsNullOrEmpty(cfg.IconPath) && File.Exists(cfg.IconPath))
                {
                    try
                    {
                        using (var bmp = new Bitmap(cfg.IconPath))
                            _iconPreview.Image = new Bitmap(bmp, 44, 44);
                    }
                    catch { _iconPreview.Image = null; }
                }
                var label = string.IsNullOrEmpty(cfg.Label) ? "Button " + (_index + 1) : cfg.Label;
                var topic = string.IsNullOrEmpty(cfg.Topic) ? "(no topic)" : cfg.Topic;
                _tooltip.SetToolTip(_iconPreview, label + "\n" + topic);
            }

            public ButtonConfig Collect()
            {
                return new ButtonConfig
                {
                    Label = _label.Text.Trim(),
                    Topic = _topic.Text.Trim(),
                    Payload = _payload.Text,
                    Qos = _qos.SelectedIndex,
                    Retain = _retain.Checked,
                    IconPath = _iconPath.Text,
                    MakeWhiteTransparent = _chkWhiteTransparent.Checked,
                    MakeBlackTransparent = _chkBlackTransparent.Checked
                };
            }
        }
    }
}
