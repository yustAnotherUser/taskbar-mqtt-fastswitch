using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TaskbarMqtt.Config;

namespace TaskbarMqtt.UI
{
    public class PopupForm : Form
    {
        private readonly List<ButtonConfig> _buttons;
        private readonly Action<int> _onClick;
        private readonly Func<int, Image> _imageFor;
        private FlowLayoutPanel _flow;
        private Timer _closeTimer;
        private readonly ToolTip _tooltip;
        private const int ButtonSize = 56;
        private const int Pad = 8;

        private static readonly Color BgColor = Color.FromArgb(240, 240, 240);
        private static readonly Color BtnBorder = Color.FromArgb(190, 190, 190);
        private static readonly Color BtnHover = Color.FromArgb(210, 225, 245);
        private static readonly Color BtnDown = Color.FromArgb(180, 205, 235);
        private static readonly Font BtnFont = new Font("Segoe UI", 7F, FontStyle.Regular);

        public PopupForm(List<ButtonConfig> buttons, Func<int, Image> imageFor, Action<int> onClick)
        {
            _buttons = buttons;
            _imageFor = imageFor;
            _onClick = onClick;
            _tooltip = new ToolTip();

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = BgColor;
            Padding = new Padding(Pad);
            DoubleBuffered = true;

            _flow = new FlowLayoutPanel
            {
                Dock = DockStyle.None,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            Controls.Add(_flow);

            BuildButtons();

            Size = new Size(_flow.Width + Pad * 2, _flow.Height + Pad * 2);
            _flow.Location = new Point(Pad, Pad);

            Deactivate += (s, e) => Hide();
            Leave += (s, e) => Hide();

            _closeTimer = new Timer { Interval = 300 };
            _closeTimer.Tick += OnCloseTimer;

            MouseLeave += OnMaybeLeave;
            _flow.MouseLeave += OnMaybeLeave;
        }

        private void BuildButtons()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                var cfg = _buttons[i];
                int idx = i;

                var label = string.IsNullOrEmpty(cfg.Label) ? null : cfg.Label;
                var topic = string.IsNullOrEmpty(cfg.Topic) ? null : cfg.Topic;

                var b = new Button
                {
                    Width = ButtonSize,
                    Height = ButtonSize,
                    Margin = new Padding(3),
                    Padding = new Padding(0),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.White,
                    Text = label ?? "",
                    Font = BtnFont,
                    TextAlign = ContentAlignment.BottomCenter,
                    ImageAlign = ContentAlignment.TopCenter,
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
                        b.Image = img;
                        b.Text = "";
                    }
                }
                catch { }

                if (!string.IsNullOrEmpty(topic))
                    _tooltip.SetToolTip(b, (label ?? "Button " + (idx + 1)) + "\n" + topic);

                b.Click += (s, e) =>
                {
                    try { _onClick?.Invoke(idx); } catch { }
                    Hide();
                };

                _flow.Controls.Add(b);
            }
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
