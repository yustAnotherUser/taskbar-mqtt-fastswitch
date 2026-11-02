using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TaskbarMqtt.Platform;

namespace TaskbarMqtt.UI
{
    public class TrayMenuWindow : Window
    {
        private readonly Action _openPanel;
        private readonly Action _settings;
        private readonly Action _quit;

        private readonly DispatcherTimer _closeTimer;

        private static readonly bool IsDarkMode = ThemeDetector.IsDark;

        private static readonly IBrush BgBrush = Solid(IsDarkMode ? 0xFF2D2D30 : 0xFFF8F8F8);
        private static readonly IBrush ItemHover = Solid(IsDarkMode ? 0xFF4A4A50 : 0xFFE4E4E8);
        private static readonly IBrush ItemFore = Solid(IsDarkMode ? 0xFFDCDCDC : 0xFF202020);

        private static IBrush Solid(uint argb)
        {
            return new SolidColorBrush(Color.FromUInt32(argb));
        }

        public TrayMenuWindow(Action openPanel, Action settings, Action quit)
        {
            _openPanel = openPanel;
            _settings = settings;
            _quit = quit;

            SystemDecorations = SystemDecorations.None;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            CanResize = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            ShowActivated = false;

            var border = new Border
            {
                Background = BgBrush,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6),
                ClipToBounds = true,
                Child = BuildItems(false)
            };

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

        private StackPanel BuildItems(bool includeOpenPanel)
        {
            var panel = new StackPanel { MinWidth = 190 };

            if (includeOpenPanel)
                panel.Children.Add(Item("Open panel", () => Run(_openPanel)));

            panel.Children.Add(Item("Settings…", () => Run(_settings)));
            panel.Children.Add(Separator());
            panel.Children.Add(Item("Quit", () => Run(_quit)));

            return panel;
        }

        private Border Item(string text, Action action)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(12, 8),
                Background = Brushes.Transparent,
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 13,
                    Foreground = ItemFore,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            border.PointerEntered += (s, e) => border.Background = ItemHover;
            border.PointerExited += (s, e) => border.Background = Brushes.Transparent;
            border.PointerReleased += (s, e) =>
            {
                Hide();
                action();
            };
            return border;
        }

        private static Border Separator()
        {
            return new Border
            {
                Height = 1,
                Background = Solid(IsDarkMode ? 0xFF4A4A50 : 0xFFD8D8DC),
                Margin = new Thickness(6, 4)
            };
        }

        private void Run(Action action)
        {
            try { action?.Invoke(); } catch { }
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

        public void OpenAt(int px, int py, bool includeOpenPanel)
        {
            var border = (Border)Content;
            border.Child = BuildItems(includeOpenPanel);

            double width = 200;
            double height = includeOpenPanel ? 130 : 102;

            var screen = FindScreen(px, py);
            var area = screen != null ? screen.WorkingArea : new PixelRect(0, 0, 1920, 1080);
            int x = px - 8;
            int y = py - (int)height - 8;
            if (y < area.Y)
                y = py + 20;
            if (x < area.X + 6)
                x = area.X + 6;
            if (x + (int)width > area.X + area.Width - 6)
                x = area.X + area.Width - (int)width - 6;
            if (y + (int)height > area.Y + area.Height - 6)
                y = area.Y + area.Height - (int)height - 6;

            Position = new PixelPoint(x, y);

            _closeTimer.Stop();
            Show();
            Activate();
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
