using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TaskbarMqtt.Config;

namespace TaskbarMqtt.UI
{
    public class PopupForm : Form
    {
        private readonly List<ButtonConfig> _buttons;
        private readonly Action<int> _onClick;
        private readonly Func<int, Image> _imageFor;
        private readonly Bitmap _watermark;
        private FlowLayoutPanel _flow;
        private Timer _closeTimer;
        private readonly ToolTip _tooltip;
        private readonly int _buttonSize;
        private readonly int _pad;
        private readonly Font _btnFont;
        private readonly double _scale;
        private readonly bool _showTooltips;
        private readonly bool _showPayloadInTooltip;

        private const int BaseButtonSize = 44;
        private const int BasePad = 6;

        private static readonly bool IsDarkMode = DetectDarkMode();

        private static readonly Color BgColor = IsDarkMode ? Color.FromArgb(45, 45, 48) : SystemColors.Control;
        private static readonly Color BtnBack = IsDarkMode ? Color.FromArgb(60, 60, 65) : SystemColors.Window;
        private static readonly Color BtnFore = IsDarkMode ? Color.FromArgb(220, 220, 220) : SystemColors.ControlText;
        private static readonly Color BtnBorder = IsDarkMode ? Color.FromArgb(90, 90, 95) : SystemColors.ControlDark;
        private static readonly Color BtnHover = IsDarkMode ? Color.FromArgb(75, 75, 80) : SystemColors.ControlLight;
        private static readonly Color BtnDown = IsDarkMode ? Color.FromArgb(50, 50, 55) : SystemColors.ControlDark;

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

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        public PopupForm(List<ButtonConfig> buttons, Func<int, Image> imageFor, Action<int> onClick, Bitmap watermark, int popupSizePercent = 100, bool showTooltips = true, bool showPayloadInTooltip = false)
        {
            _buttons = buttons;
            _imageFor = imageFor;
            _onClick = onClick;
            _watermark = watermark;
            _showTooltips = showTooltips;
            _showPayloadInTooltip = showPayloadInTooltip;
            _tooltip = new ToolTip();
            _scale = Math.Max(0.25, Math.Min(2.0, popupSizePercent / 100.0));
            _buttonSize = (int)Math.Round(BaseButtonSize * _scale);
            _pad = Math.Max(2, (int)Math.Round(BasePad * _scale));
            var fontSize = (float)Math.Max(5, Math.Round(7 * _scale));
            _btnFont = new Font("Segoe UI", fontSize, FontStyle.Regular);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = BgColor;
            Padding = new Padding(_pad);
            DoubleBuffered = true;

            _flow = new FlowLayoutPanel
            {
                Dock = DockStyle.None,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = BgColor
            };
            Controls.Add(_flow);

            BuildButtons();

            Size = new Size(_flow.Width + _pad * 2, _flow.Height + _pad * 2);
            _flow.Location = new Point(_pad, _pad);

            Deactivate += (s, e) => Hide();
            Leave += (s, e) => Hide();

            _closeTimer = new Timer { Interval = 300 };
            _closeTimer.Tick += OnCloseTimer;

            MouseLeave += OnMaybeLeave;
            _flow.MouseLeave += OnMaybeLeave;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyRegion();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRegion();
        }

        private int FormCornerRadius => Math.Max(4, (int)Math.Round(20 * _scale));
        private int BtnCornerRadius => Math.Max(2, (int)Math.Round(8 * _scale));

        private void ApplyRegion()
        {
            var region = CreateRoundRectRgn(0, 0, Width, Height, FormCornerRadius, FormCornerRadius);
            SetWindowRgn(Handle, region, true);
        }

        private void RoundButton(Button btn)
        {
            var r = BtnCornerRadius;
            var rgn = CreateRoundRectRgn(0, 0, btn.Width + 1, btn.Height + 1, r, r);
            btn.Region = Region.FromHrgn(rgn);
        }

        private void BuildButtons()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                var cfg = _buttons[i];
                int idx = i;

                var label = string.IsNullOrEmpty(cfg.Label) ? null : cfg.Label;
                var topic = string.IsNullOrEmpty(cfg.Topic) ? null : cfg.Topic;

                var margin = Math.Max(1, (int)Math.Round(3 * _scale));
                var imgInset = Math.Max(4, (int)Math.Round(8 * _scale));

                var b = new Button
                {
                    Width = _buttonSize,
                    Height = _buttonSize,
                    Margin = new Padding(margin),
                    Padding = new Padding(0),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = BtnBack,
                    ForeColor = BtnFore,
                    Text = label ?? (idx + 1).ToString(),
                    Font = _btnFont,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ImageAlign = ContentAlignment.MiddleCenter,
                    Tag = idx,
                    Cursor = Cursors.Hand,
                    TabStop = false
                };
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = BtnBorder;
                b.FlatAppearance.MouseOverBackColor = BtnHover;
                b.FlatAppearance.MouseDownBackColor = BtnDown;

                try
                {
                    var img = _imageFor?.Invoke(idx);
                    if (img != null)
                    {
                        var scaled = ScaleImage(img, _buttonSize - imgInset, _buttonSize - imgInset);
                        b.Image = RoundImageCorners(scaled, Math.Max(2, (int)Math.Round(6 * _scale)));
                        b.Text = "";
                        img.Dispose();
                    }
                }
                catch { }

                if (_watermark != null && b.Image == null)
                {
                    b.BackgroundImage = MakeWatermark(_watermark, _buttonSize);
                    b.BackgroundImageLayout = ImageLayout.Center;
                }

                if (_showTooltips)
                {
                    var tipText = label ?? "Button " + (idx + 1);
                    if (!string.IsNullOrEmpty(topic))
                        tipText += "\n" + topic;
                    if (_showPayloadInTooltip && !string.IsNullOrEmpty(cfg.Payload))
                        tipText += "\nPayload: " + cfg.Payload;
                    _tooltip.SetToolTip(b, tipText);
                }

                RoundButton(b);

                b.Click += (s, e) =>
                {
                    try { _onClick?.Invoke(idx); } catch { }
                    Hide();
                };

                _flow.Controls.Add(b);
            }
        }

        private static Image ScaleImage(Image src, int maxW, int maxH)
        {
            var ratio = Math.Min((double)maxW / src.Width, (double)maxH / src.Height);
            var w = (int)(src.Width * ratio);
            var h = (int)(src.Height * ratio);
            if (w < 1) w = 1;
            if (h < 1) h = 1;
            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, 0, 0, w, h);
            }
            return bmp;
        }

        private static Bitmap MakeWatermark(Bitmap src, int buttonSize)
        {
            var inset = Math.Max(4, buttonSize / 5);
            var drawSize = buttonSize - inset * 2;
            if (drawSize < 4) drawSize = 4;
            var bmp = new Bitmap(buttonSize, buttonSize);
            using (var g = Graphics.FromImage(bmp))
            {
                var attr = new ImageAttributes();
                var matrix = new ColorMatrix { Matrix33 = 0.25f };
                attr.SetColorMatrix(matrix);
                g.DrawImage(src, new Rectangle(inset, inset, drawSize, drawSize),
                    0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attr);
            }
            return bmp;
        }

        private static Image RoundImageCorners(Image src, int radius)
        {
            var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            using (var path = GetRoundRect(new Rectangle(0, 0, src.Width, src.Height), radius))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new TextureBrush(src, WrapMode.Clamp))
                    g.FillPath(brush, path);
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

        public new void Show()
        {
            ShowAtCursor();
        }

        public void ShowAtCursor()
        {
            var screen = Screen.FromPoint(Cursor.Position);
            var wa = screen.WorkingArea;
            var x = Cursor.Position.X - Width / 2;
            var y = Cursor.Position.Y - Height - 10;
            if (y < wa.Top) y = Cursor.Position.Y + 20;
            if (x < wa.Left) x = wa.Left + 6;
            if (x + Width > wa.Right) x = wa.Right - Width - 6;
            Location = new Point(x, y);
            base.Show();
            Activate();
            _closeTimer.Stop();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _closeTimer.Stop();
        }

        private void OnMaybeLeave(object sender, EventArgs e)
        {
            _closeTimer.Stop();
            _closeTimer.Start();
        }

        private void OnCloseTimer(object sender, EventArgs e)
        {
            _closeTimer.Stop();
            if (!Bounds.Contains(Cursor.Position))
                Hide();
        }

        protected override bool ShowWithoutActivation => true;
    }
}
