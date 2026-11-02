using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;
using TaskbarMqtt.Config;
using TaskbarMqtt.Platform;

namespace TaskbarMqtt.UI
{
    public class PopupWindow : Window
    {
        private const int BaseButtonSize = 44;
        private const int BasePad = 6;

        private readonly List<ButtonConfig> _buttons;
        private readonly Func<int, SKBitmap> _imageFor;
        private readonly Action<int> _onClick;
        private readonly SKBitmap _watermark;
        private readonly bool _showTooltips;
        private readonly bool _showPayloadInTooltip;
        private readonly bool _stayOpen;

        private readonly int _buttonSize;
        private readonly int _pad;
        private readonly double _scale;

        private readonly DispatcherTimer _closeTimer;
        private readonly Action _onSettings;
        private readonly Action _onQuit;

        private static readonly bool IsDarkMode = ThemeDetector.IsDark;

        private static readonly IBrush BgBrush = Solid(IsDarkMode ? 0xFF2D2D30 : 0xFFF0F0F0);
        private static readonly IBrush BtnBack = Solid(IsDarkMode ? 0xFF3C3C41 : 0xFFFFFFFF);
        private static readonly IBrush BtnBorder = Solid(IsDarkMode ? 0xFF5A5A5F : 0xFFA0A0A0);

        private static IBrush Solid(uint argb)
        {
            return new SolidColorBrush(Color.FromUInt32(argb));
        }

        public PopupWindow(List<ButtonConfig> buttons, Func<int, SKBitmap> imageFor, Action<int> onClick,
            SKBitmap watermark, int popupSizePercent = 100, bool showTooltips = true,
            bool showPayloadInTooltip = false, bool stayOpen = false,
            Action onSettings = null, Action onQuit = null)
        {
            _buttons = buttons;
            _imageFor = imageFor;
            _onClick = onClick;
            _watermark = watermark;
            _showTooltips = showTooltips;
            _showPayloadInTooltip = showPayloadInTooltip;
            _stayOpen = stayOpen;
            _onSettings = onSettings;
            _onQuit = onQuit;

            _scale = Math.Max(0.25, Math.Min(2.0, popupSizePercent / 100.0));
            _buttonSize = (int)Math.Round(BaseButtonSize * _scale);
            _pad = Math.Max(2, (int)Math.Round(BasePad * _scale));

            SystemDecorations = SystemDecorations.None;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            CanResize = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            ShowActivated = false;

            int formRadius = Math.Max(4, (int)Math.Round(20 * _scale));
            int btnRadius = Math.Max(2, (int)Math.Round(8 * _scale));

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            for (int i = 0; i < _buttons.Count; i++)
            {
                var cfg = _buttons[i];
                int idx = i;
                var label = string.IsNullOrEmpty(cfg.Label) ? null : cfg.Label;
                var topic = string.IsNullOrEmpty(cfg.Topic) ? null : cfg.Topic;
                int margin = Math.Max(1, (int)Math.Round(3 * _scale));
                int imgInset = Math.Max(4, (int)Math.Round(8 * _scale));

                var b = new Button
                {
                    Width = _buttonSize,
                    Height = _buttonSize,
                    Margin = new Thickness(margin),
                    Padding = new Thickness(0),
                    Background = BtnBack,
                    Foreground = Brushes.White,
                    BorderBrush = BtnBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(btnRadius),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Tag = idx,
                    Cursor = new Cursor(StandardCursorType.Hand)
                };

                object content = null;
                try
                {
                    var img = _imageFor?.Invoke(idx);
                    if (img != null)
                    {
                        int avail = _buttonSize - imgInset;
                        int imgRadius = Math.Max(2, (int)Math.Round(6 * _scale));
                        int radius = cfg.StretchImage ? Math.Max(2, imgRadius / 2) : imgRadius;
                        using (var fitted = ImageUtils.FitSquare(img, avail, cfg.StretchImage))
                        using (var rounded = ImageUtils.RoundCorners(fitted, radius))
                        {
                            content = new Image
                            {
                                Source = ImageUtils.ToAvalonia(rounded),
                                Width = avail,
                                Height = avail,
                                Stretch = Stretch.Uniform
                            };
                        }
                    }
                }
                catch
                {
                    content = null;
                }

                if (content == null && _watermark != null)
                {
                    int avail = _buttonSize - imgInset;
                    int imgRadius = Math.Max(2, (int)Math.Round(6 * _scale));
                    using (var fitted = ImageUtils.FitSquare(_watermark, avail, false))
                    using (var rounded = ImageUtils.RoundCorners(fitted, imgRadius))
                    {
                        content = new Image
                        {
                            Source = ImageUtils.ToAvalonia(rounded),
                            Width = avail,
                            Height = avail,
                            Stretch = Stretch.Uniform,
                            Opacity = 0.25
                        };
                    }
                }

                if (content == null)
                {
                    content = new TextBlock
                    {
                        Text = label ?? (idx + 1).ToString(),
                        FontSize = Math.Max(9, Math.Round(13 * _scale)),
                        Foreground = IsDarkMode ? BrushFrom(0xFFDCDCDC) : BrushFrom(0xFF202020)
                    };
                }

                b.Content = content;

                if (_showTooltips)
                {
                    var tipText = label ?? "Button " + (idx + 1);
                    if (!string.IsNullOrEmpty(topic))
                        tipText += "\n" + topic;
                    if (_showPayloadInTooltip && !string.IsNullOrEmpty(cfg.Payload))
                        tipText += "\nPayload: " + cfg.Payload;
                    ToolTip.SetTip(b, tipText);
                }

                b.Click += (s, e) =>
                {
                    try { _onClick?.Invoke(idx); } catch { }
                    if (!_stayOpen)
                        Hide();
                };

                panel.Children.Add(b);
            }

            var border = new Border
            {
                Background = BgBrush,
                CornerRadius = new CornerRadius(formRadius),
                Padding = new Thickness(_pad),
                ClipToBounds = true
            };

            var dock = new DockPanel { LastChildFill = true };

            var footerRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 2
            };
            var footerButtonStyle = new Action<Button>(b =>
            {
                b.Padding = new Thickness(10, 2);
                b.FontSize = Math.Max(9, Math.Round(10 * _scale));
                b.Background = Brushes.Transparent;
                b.BorderThickness = new Thickness(0);
                b.Foreground = IsDarkMode ? BrushFrom(0xFFB8B8BC) : BrushFrom(0xFF505054);
                b.MinHeight = 0;
            });
            var settingsBtn = new Button { Content = "Settings" };
            footerButtonStyle(settingsBtn);
            settingsBtn.Click += (s, e) =>
            {
                Hide();
                try { _onSettings?.Invoke(); } catch { }
            };
            var quitBtn = new Button { Content = "Quit" };
            footerButtonStyle(quitBtn);
            quitBtn.Click += (s, e) =>
            {
                Hide();
                try { _onQuit?.Invoke(); } catch { }
            };
            footerRow.Children.Add(settingsBtn);
            footerRow.Children.Add(quitBtn);

            var footer = new StackPanel { Orientation = Orientation.Vertical };
            footer.Children.Add(new Border
            {
                Height = 1,
                Background = BtnBorder,
                Opacity = 0.6,
                Margin = new Thickness(2, Math.Max(2, (int)Math.Round(5 * _scale)), 2, Math.Max(1, (int)Math.Round(2 * _scale)))
            });
            footer.Children.Add(footerRow);
            DockPanel.SetDock(footer, Dock.Bottom);
            dock.Children.Add(footer);
            dock.Children.Add(panel);

            border.Child = dock;

            Content = border;

            Deactivated += (s, e) => Hide();

            _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _closeTimer.Tick += OnCloseTimer;

            PointerExited += (s, e) =>
            {
                _closeTimer.Stop();
                _closeTimer.Start();
            };
            PointerEntered += (s, e) => _closeTimer.Stop();
        }

        private static IBrush BrushFrom(uint argb)
        {
            return new SolidColorBrush(Color.FromUInt32(argb));
        }

        private void OnCloseTimer(object sender, EventArgs e)
        {
            _closeTimer.Stop();
            if (!PointerInsideBounds())
                Hide();
        }

        private bool PointerInsideBounds()
        {
            if (!PointerPosition.TryGet(out int px, out int py))
                return true;
            try
            {
                double scaling = RenderScaling;
                int w = (int)Math.Max(1, Width * scaling);
                int h = (int)Math.Max(1, Height * scaling);
                var rect = new PixelRect(Position, new PixelSize(w, h));
                return rect.Contains(new PixelPoint(px, py));
            }
            catch
            {
                return true;
            }
        }

        public void OpenAtPointer()
        {
            ComputeSize(out double width, out double height);
            if (PointerPosition.TryGet(out int px, out int py))
            {
                Position = ComputePosition(px, py, width, height);
            }
            else
            {
                var screen = FindScreen(0, 0);
                var area = screen != null ? screen.WorkingArea : new PixelRect(0, 0, 1920, 1080);
                Position = new PixelPoint(area.X + area.Width - (int)width - 8, area.Y + area.Height - (int)height - 8);
            }
            ShowWindow();
        }

        public void OpenAt(int px, int py)
        {
            ComputeSize(out double width, out double height);
            Position = ComputePosition(px, py, width, height);
            ShowWindow();
        }

        private void ShowWindow()
        {
            _closeTimer.Stop();
            Show();
            Activate();
        }

        private void ComputeSize(out double width, out double height)
        {
            int count = Math.Max(1, _buttons.Count);
            double marginPerButton = Math.Max(2, (int)Math.Round(6 * _scale));
            width = _pad * 2 + count * (_buttonSize + marginPerButton);
            height = _pad * 2 + _buttonSize + marginPerButton + Math.Max(2, (int)Math.Round(26 * _scale));
        }

        private PixelPoint ComputePosition(int px, int py, double width, double height)
        {
            var screen = FindScreen(px, py);
            var area = screen != null ? screen.WorkingArea : new PixelRect(0, 0, 1920, 1080);
            int x = px - (int)(width / 2);
            int y = py - (int)height - 10;
            if (y < area.Y)
                y = py + 20;
            if (x < area.X + 6)
                x = area.X + 6;
            if (x + (int)width > area.X + area.Width - 6)
                x = area.X + area.Width - (int)width - 6;
            if (y + (int)height > area.Y + area.Height - 6)
                y = area.Y + area.Height - (int)height - 6;
            return new PixelPoint(x, y);
        }

        private Avalonia.Platform.Screen FindScreen(int x, int y)
        {
            try
            {
                var point = new PixelPoint(x, y);
                var screen = Screens.ScreenFromPoint(point);
                if (screen != null) return screen;
                foreach (var s in Screens.All)
                {
                    if (s.Bounds.Contains(point)) return s;
                }
                if (Screens.All.Count > 0)
                    return Screens.All[0];
            }
            catch
            {
            }
            return null;
        }

        protected override void OnClosed(EventArgs e)
        {
            _closeTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
