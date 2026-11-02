using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SkiaSharp;
using TaskbarMqtt.Platform;
using TaskbarMqtt.Config;
using TaskbarMqtt.Mqtt;

namespace TaskbarMqtt.UI
{
    public class SettingsWindow : Window
    {
        private readonly AppConfig _original;
        private readonly AppConfig _draft;
        private readonly Action<AppConfig> _onApply;

        private RadioButton _rbPopup, _rbMulti;
        private CheckBox _chkAutoStart, _chkShowTooltips, _chkShowPayloadInTooltip, _chkPopupStayOpen;
        private CheckBox _chkRoundedTrayIcon, _chkIconWhiteTransparent, _chkIconBlackTransparent, _chkStretchIcon;
        private ComboBox _cbPopupSize;
        private TextBox _txIconPath;
        private Image _iconTrayPreview;

        private TextBox _txHost, _txPort, _txUser, _txPass, _txClientId, _txKeepAlive, _txConnectTimeout;
        private CheckBox _chkTls, _chkInvalidCerts;
        private Button _btnTestConn;
        private TextBlock _lblConnStatus;

        private StackPanel _btnRowsPanel;
        private readonly List<ButtonRowEditor> _btnRows = new List<ButtonRowEditor>();
        private TextBlock _validationLabel;

        private readonly SKBitmap _defaultButtonIcon;

        public SettingsWindow(AppConfig config, Action<AppConfig> onApply)
        {
            _original = config;
            _draft = CloneConfig(config);
            _onApply = onApply;
            _defaultButtonIcon = ImageUtils.LoadResource("mqtt-icon.png");

            Title = "Taskbar MQTT — Settings";
            Width = 780;
            Height = 600;
            MinWidth = 680;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Icon = ImageUtils.ToIcon(ImageUtils.LoadResource("taskbar-mqtt.png"));

            var root = new DockPanel { LastChildFill = true };

            var buttonBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 10),
                Spacing = 8
            };
            var btnCancel = new Button { Content = "Cancel", Width = 90 };
            var btnOk = new Button { Content = "OK", Width = 90 };
            var btnApply = new Button { Content = "Apply", Width = 90 };
            buttonBar.Children.Add(btnCancel);
            buttonBar.Children.Add(btnOk);
            buttonBar.Children.Add(btnApply);

            _validationLabel = new TextBlock
            {
                Foreground = Brushes.OrangeRed,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 400
            };

            var bottomBar = new Grid();
            bottomBar.Children.Add(_validationLabel);
            bottomBar.Children.Add(buttonBar);
            DockPanel.SetDock(bottomBar, Dock.Bottom);
            root.Children.Add(bottomBar);

            var tabs = new TabControl { Margin = new Thickness(0, 8, 0, 0) };
            var generalTab = new TabItem { Header = "General", Content = BuildGeneralTab() };
            var brokerTab = new TabItem { Header = "Broker", Content = BuildBrokerTab() };
            var buttonsTab = new TabItem { Header = "Buttons", Content = BuildButtonsTab() };
            var aboutTab = new TabItem { Header = "About", Content = BuildAboutTab() };
            tabs.Items.Add(generalTab);
            tabs.Items.Add(brokerTab);
            tabs.Items.Add(buttonsTab);
            tabs.Items.Add(aboutTab);
            root.Children.Add(tabs);

            Content = root;

            btnCancel.Click += (s, e) => Close();
            btnApply.Click += (s, e) => Apply();
            btnOk.Click += (s, e) => { if (Apply()) Close(); };

            PopulateFromDraft();
        }

        private static AppConfig CloneConfig(AppConfig src)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(src);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<AppConfig>(json);
        }

        private Control BuildGeneralTab()
        {
            var scroll = new ScrollViewer { Padding = new Thickness(0) };
            var layout = new StackPanel { Margin = new Thickness(20, 16, 20, 8), Spacing = 6 };

            layout.Children.Add(Header("MODE"));

            var modeGrid = FieldGrid();
            modeGrid.Children.Add(Label("Display mode:"));
            var modePanel = new StackPanel { Spacing = 4 };
            _rbPopup = new RadioButton { Content = "Popup panel (left-click to open)", GroupName = "displayMode" };
            _rbMulti = new RadioButton { Content = "One tray icon per button", GroupName = "displayMode" };
            _rbPopup.IsCheckedChanged += (s, e) => UpdateGreyOut();
            _rbMulti.IsCheckedChanged += (s, e) => UpdateGreyOut();
            modePanel.Children.Add(_rbPopup);
            modePanel.Children.Add(_rbMulti);
            Grid.SetColumn(modePanel, 1);
            modeGrid.Children.Add(modePanel);
            layout.Children.Add(modeGrid);

            layout.Children.Add(Header("POPUP"));

            var sizeGrid = FieldGrid();
            sizeGrid.Children.Add(Label("Popup size:"));
            _cbPopupSize = new ComboBox { MinWidth = 90, HorizontalAlignment = HorizontalAlignment.Left };
            var sizes = new List<string>();
            for (int v = 25; v <= 200; v += 25) sizes.Add(v + "%");
            _cbPopupSize.ItemsSource = sizes;
            Grid.SetColumn(_cbPopupSize, 1);
            sizeGrid.Children.Add(_cbPopupSize);
            layout.Children.Add(sizeGrid);

            _chkShowTooltips = new CheckBox { Content = "Show tooltips in popup" };
            _chkShowTooltips.IsCheckedChanged += (s, e) => UpdateGreyOut();
            _chkShowPayloadInTooltip = new CheckBox { Content = "Show payload in tooltip" };
            _chkPopupStayOpen = new CheckBox { Content = "Popup stays open on button click" };
            layout.Children.Add(Indent(_chkShowTooltips));
            layout.Children.Add(Indent(_chkShowPayloadInTooltip));
            layout.Children.Add(Indent(_chkPopupStayOpen));

            layout.Children.Add(Header("TRAY ICON"));

            var iconGrid = FieldGrid();
            iconGrid.Children.Add(Label("Custom icon:"));
            var iconPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var previewBorder = new Border
            {
                Width = 48,
                Height = 48,
                BorderThickness = new Thickness(1),
                BorderBrush = BorderBrushOf(),
                CornerRadius = new CornerRadius(4),
                Child = _iconTrayPreview = new Image
                {
                    Width = 44,
                    Height = 44,
                    Stretch = Stretch.Uniform
                }
            };
            _txIconPath = new TextBox { MinWidth = 260 };
            var browseBtn = new Button { Content = "Browse…" };
            browseBtn.Click += OnIconBrowse;
            var clearBtn = new Button { Content = "Clear" };
            clearBtn.Click += (s, e) =>
            {
                _txIconPath.Text = "";
                UpdateTrayIconPreview();
            };
            var pathPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            pathPanel.Children.Add(_txIconPath);
            pathPanel.Children.Add(browseBtn);
            pathPanel.Children.Add(clearBtn);
            var iconColumn = new StackPanel { Spacing = 6 };
            iconColumn.Children.Add(pathPanel);
            iconPanel.Children.Add(previewBorder);
            iconPanel.Children.Add(iconColumn);
            Grid.SetColumn(iconPanel, 1);
            iconGrid.Children.Add(iconPanel);
            layout.Children.Add(iconGrid);

            var appPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            _chkStretchIcon = new CheckBox { Content = "Stretch image" };
            _chkRoundedTrayIcon = new CheckBox { Content = "Rounded icon" };
            _chkIconWhiteTransparent = new CheckBox { Content = "White→Transparent" };
            _chkIconBlackTransparent = new CheckBox { Content = "Black→Transparent" };
            _chkStretchIcon.IsCheckedChanged += (s, e) => UpdateTrayIconPreview();
            _chkIconWhiteTransparent.IsCheckedChanged += (s, e) => UpdateTrayIconPreview();
            _chkIconBlackTransparent.IsCheckedChanged += (s, e) => UpdateTrayIconPreview();
            appPanel.Children.Add(_chkStretchIcon);
            appPanel.Children.Add(_chkRoundedTrayIcon);
            appPanel.Children.Add(_chkIconWhiteTransparent);
            appPanel.Children.Add(_chkIconBlackTransparent);
            layout.Children.Add(Indent(appPanel));

            layout.Children.Add(Header("STARTUP"));
            _chkAutoStart = new CheckBox { Content = "Launch on login (XDG autostart)" };
            layout.Children.Add(Indent(_chkAutoStart));

            scroll.Content = layout;
            return scroll;
        }

        private Control BuildBrokerTab()
        {
            var scroll = new ScrollViewer();
            var layout = new StackPanel { Margin = new Thickness(20, 16, 20, 8), Spacing = 6 };

            layout.Children.Add(Field("Host:", _txHost = new TextBox { MinWidth = 260 }));
            layout.Children.Add(Field("Port:", _txPort = new TextBox { MinWidth = 90 }));
            layout.Children.Add(Field("Username:", _txUser = new TextBox { MinWidth = 200 }));
            layout.Children.Add(Field("Password:", _txPass = new TextBox { MinWidth = 200, PasswordChar = '•' }));
            layout.Children.Add(Field("Client ID:", _txClientId = new TextBox { MinWidth = 260 }));
            layout.Children.Add(Field("Keep alive (s):", _txKeepAlive = new TextBox { MinWidth = 90 }));
            layout.Children.Add(Field("Connect timeout (s):", _txConnectTimeout = new TextBox { MinWidth = 90 }));

            var tlsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            _chkTls = new CheckBox { Content = "Use TLS" };
            _chkInvalidCerts = new CheckBox { Content = "Allow self-signed certificates" };
            tlsPanel.Children.Add(_chkTls);
            tlsPanel.Children.Add(_chkInvalidCerts);
            layout.Children.Add(Field("Encryption (TLS/SSL):", tlsPanel));

            var testRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 6, 0, 0) };
            _btnTestConn = new Button { Content = "Test Connection", MinWidth = 130 };
            _lblConnStatus = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 };
            _btnTestConn.Click += OnTestConnection;
            testRow.Children.Add(_btnTestConn);
            testRow.Children.Add(_lblConnStatus);
            layout.Children.Add(testRow);

            scroll.Content = layout;
            return scroll;
        }

        private Control BuildButtonsTab()
        {
            var dock = new DockPanel { LastChildFill = true };

            var bottom = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(12)
            };
            DockPanel.SetDock(bottom, Dock.Bottom);
            var btnAdd = new Button { Content = "+ Add Button", MinWidth = 110 };
            btnAdd.Click += OnAddButton;
            bottom.Children.Add(btnAdd);
            dock.Children.Add(bottom);

            var scroll = new ScrollViewer { Padding = new Thickness(0) };
            _btnRowsPanel = new StackPanel { Margin = new Thickness(12, 12, 12, 4), Spacing = 10 };
            scroll.Content = _btnRowsPanel;
            dock.Children.Add(scroll);

            return dock;
        }

        private Control BuildAboutTab()
        {
            var scroll = new ScrollViewer();
            var box = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(20, 30, 20, 20),
                Spacing = 2
            };
            var lines = new[]
            {
                "Taskbar MQTT Client",
                "",
                "Version 1.9.3 (Linux edition, Avalonia)",
                "",
                "Created with OpenCode's MiniMax V3:free",
                "OpenCode's Big Pickle",
                "and OpenCode's DeepSeek V4 Flash:free",
                "",
                "Maintained by yustAnotherUser",
                "",
                "A lightweight tray application that publishes",
                "pre-configured MQTT messages at the click of a button.",
            };
            for (int i = 0; i < lines.Length; i++)
            {
                box.Children.Add(new TextBlock
                {
                    Text = lines[i],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontWeight = i == 0 ? FontWeight.Bold : FontWeight.Normal,
                    FontSize = i == 0 ? 18 : 13,
                    Margin = new Thickness(0, i == 0 ? 0 : 1)
                });
            }
            scroll.Content = box;
            return scroll;
        }

        private TextBlock Header(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 10, 0, 4)
            };
        }

        private static Control Indent(Control c)
        {
            Grid.SetColumn(c, 1);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(140)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            grid.Children.Add(c);
            return grid;
        }

        private Grid FieldGrid()
        {
            var grid = new Grid { ColumnSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(140)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            return grid;
        }

        private Grid Field(string labelText, Control input)
        {
            var grid = FieldGrid();
            grid.Children.Add(Label(labelText));
            Grid.SetColumn(input, 1);
            grid.Children.Add(input);
            return grid;
        }

        private TextBlock Label(string text)
        {
            return new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static IBrush BorderBrushOf()
        {
            return new SolidColorBrush(Color.FromRgb(160, 160, 160));
        }

        private void UpdateGreyOut()
        {
            bool popupMode = _rbPopup.IsChecked == true;
            _cbPopupSize.IsEnabled = popupMode;
            _chkShowTooltips.IsEnabled = popupMode;
            _chkPopupStayOpen.IsEnabled = popupMode;
            bool payloadEnabled = popupMode && _chkShowTooltips.IsChecked == true;
            _chkShowPayloadInTooltip.IsEnabled = payloadEnabled;
        }

        private void UpdateTrayIconPreview()
        {
            SKBitmap source = null;
            var path = _txIconPath.Text;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                source = ImageUtils.Load(path);
            }
            if (source == null)
            {
                _iconTrayPreview.Source = _defaultButtonIcon != null
                    ? ImageUtils.ToAvalonia(_defaultButtonIcon)
                    : null;
                _iconTrayPreview.Stretch = Stretch.Uniform;
                return;
            }
            using (source)
            {
                ImageUtils.ApplyTransparency(source, _chkIconWhiteTransparent.IsChecked == true, _chkIconBlackTransparent.IsChecked == true);
                using (var fitted = ImageUtils.FitSquare(source, 64, _chkStretchIcon.IsChecked == true))
                {
                    _iconTrayPreview.Source = ImageUtils.ToAvalonia(fitted);
                    _iconTrayPreview.Stretch = _chkStretchIcon.IsChecked == true ? Stretch.Fill : Stretch.Uniform;
                }
            }
        }

        private async void OnIconBrowse(object sender, EventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select custom tray icon",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Icon files")
                    {
                        Patterns = new[] { "*.ico", "*.png", "*.jpg", "*.jpeg", "*.bmp" }
                    }
                }
            });
            if (files != null && files.Count > 0)
            {
                try
                {
                    _txIconPath.Text = files[0].Path.LocalPath;
                }
                catch
                {
                    _txIconPath.Text = "";
                }
                UpdateTrayIconPreview();
            }
        }

        private void OnAddButton(object sender, EventArgs e)
        {
            if (_btnRows.Count >= AppConfig.MaxButtons)
            {
                ShowValidation("Maximum " + AppConfig.MaxButtons + " buttons allowed.");
                return;
            }
            AddButtonRow();
            RefreshButtonStates();
        }

        private void AddButtonRow()
        {
            var row = new ButtonRowEditor(_btnRows.Count, RemoveButtonRow, _defaultButtonIcon);
            _btnRows.Add(row);
            _btnRowsPanel.Children.Add(row);
        }

        private void RemoveButtonRow(ButtonRowEditor row)
        {
            var idx = _btnRows.IndexOf(row);
            if (idx < 0) return;
            _btnRows.RemoveAt(idx);
            _btnRowsPanel.Children.Remove(row);
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
            _rbPopup.IsChecked = _draft.DisplayMode == DisplayMode.PopupPanel;
            _rbMulti.IsChecked = _draft.DisplayMode == DisplayMode.MultipleIcons;
            _chkAutoStart.IsChecked = _draft.StartWithWindows;

            int pct = _draft.PopupSizePercent;
            pct = ((pct + 12) / 25) * 25;
            if (pct < 25) pct = 25;
            if (pct > 200) pct = 200;
            _cbPopupSize.SelectedItem = pct + "%";
            _chkShowTooltips.IsChecked = _draft.ShowTooltips;
            _chkShowPayloadInTooltip.IsChecked = _draft.ShowPayloadInTooltip;
            _chkPopupStayOpen.IsChecked = _draft.PopupStaysOpen;
            _chkRoundedTrayIcon.IsChecked = _draft.RoundedTrayIcon;
            _chkIconWhiteTransparent.IsChecked = _draft.MakeWhiteTransparent;
            _chkIconBlackTransparent.IsChecked = _draft.MakeBlackTransparent;

            _txHost.Text = _draft.Broker.Host ?? "";
            _txPort.Text = _draft.Broker.Port.ToString();
            _txUser.Text = _draft.Broker.Username ?? "";
            _txPass.Text = _draft.Broker.Password ?? "";
            _txClientId.Text = string.IsNullOrEmpty(_draft.Broker.ClientId) ? "TaskbarClient" : _draft.Broker.ClientId;
            _txKeepAlive.Text = _draft.Broker.KeepAliveSeconds.ToString();
            _txConnectTimeout.Text = _draft.Broker.ConnectTimeoutSeconds.ToString();
            _txIconPath.Text = _draft.IconPath ?? "";
            _chkStretchIcon.IsChecked = _draft.StretchIcon;
            UpdateTrayIconPreview();
            _chkTls.IsChecked = _draft.Broker.UseTls;
            _chkInvalidCerts.IsChecked = _draft.Broker.AllowInvalidCerts;

            _btnRows.Clear();
            _btnRowsPanel.Children.Clear();
            int count = Math.Max(1, _draft.Buttons.Count);
            for (int i = 0; i < count; i++)
            {
                AddButtonRow();
                if (i < _draft.Buttons.Count)
                    _btnRows[i].Bind(_draft.Buttons[i]);
            }
            RefreshButtonStates();
            UpdateGreyOut();
        }

        private bool CollectToDraft()
        {
            _draft.ButtonCount = _btnRows.Count;
            _draft.DisplayMode = _rbPopup.IsChecked == true ? DisplayMode.PopupPanel : DisplayMode.MultipleIcons;
            _draft.StartWithWindows = _chkAutoStart.IsChecked == true;
            _draft.ShowTooltips = _chkShowTooltips.IsChecked == true;
            _draft.ShowPayloadInTooltip = _chkShowPayloadInTooltip.IsChecked == true;
            _draft.PopupStaysOpen = _chkPopupStayOpen.IsChecked == true;
            _draft.IconPath = _txIconPath.Text.Trim();
            _draft.StretchIcon = _chkStretchIcon.IsChecked == true;
            _draft.RoundedTrayIcon = _chkRoundedTrayIcon.IsChecked == true;
            _draft.MakeWhiteTransparent = _chkIconWhiteTransparent.IsChecked == true;
            _draft.MakeBlackTransparent = _chkIconBlackTransparent.IsChecked == true;
            var sel = _cbPopupSize.SelectedItem as string;
            if (sel != null && int.TryParse(sel.TrimEnd('%'), out var pct))
                _draft.PopupSizePercent = pct;

            if (!int.TryParse(_txPort.Text, out var port) || port <= 0 || port > 65535)
            {
                ShowValidation("Port must be a number between 1 and 65535.");
                return false;
            }
            if (!int.TryParse(_txKeepAlive.Text, out var ka) || ka <= 0)
            {
                ShowValidation("Keep alive must be a positive number.");
                return false;
            }
            if (!int.TryParse(_txConnectTimeout.Text, out var ct) || ct <= 0)
            {
                ShowValidation("Connect timeout must be a positive number.");
                return false;
            }
            _draft.Broker.Host = _txHost.Text.Trim();
            _draft.Broker.Port = port;
            _draft.Broker.Username = _txUser.Text;
            _draft.Broker.Password = _txPass.Text;
            _draft.Broker.ClientId = _txClientId.Text.Trim();
            _draft.Broker.KeepAliveSeconds = ka;
            _draft.Broker.ConnectTimeoutSeconds = ct;
            _draft.Broker.UseTls = _chkTls.IsChecked == true;
            _draft.Broker.AllowInvalidCerts = _chkInvalidCerts.IsChecked == true;

            _draft.Buttons.Clear();
            foreach (var row in _btnRows)
                _draft.Buttons.Add(row.Collect());
            _draft.Normalize();
            return true;
        }

        private void ShowValidation(string message)
        {
            _validationLabel.Text = message;
            Dispatcher.UIThread.Post(async () =>
            {
                await Task.Delay(4000);
                _validationLabel.Text = "";
            });
        }

        private bool Apply()
        {
            if (!CollectToDraft()) return false;

            if (!ConfigStore.Save(_draft))
            {
                ShowValidation("Failed to write config.json.");
                return false;
            }
            AutoStart.SetEnabled(_draft.StartWithWindows);
            _onApply?.Invoke(CloneConfig(_draft));
            _validationLabel.Text = "";
            return true;
        }

        private void OnTestConnection(object sender, EventArgs e)
        {
            if (!CollectToDraft()) return;
            _lblConnStatus.Text = "Testing…";
            _lblConnStatus.Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128));
            _btnTestConn.IsEnabled = false;

            var broker = _draft.Broker;
            var sw = new Stopwatch();
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
                    await Task.Delay(200);
                    ok = test.IsConnected;
                    if (!ok) err = "Could not connect within timeout";
                }
                catch (Exception ex)
                {
                    err = ex.Message;
                }
                finally
                {
                    try { test?.Dispose(); } catch { }
                }
                sw.Stop();
                Dispatcher.UIThread.Post(() =>
                {
                    _btnTestConn.IsEnabled = true;
                    if (ok)
                    {
                        _lblConnStatus.Text = "OK — connected in " + sw.ElapsedMilliseconds + " ms";
                        _lblConnStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 128, 0));
                    }
                    else
                    {
                        _lblConnStatus.Text = "Failed: " + err;
                        _lblConnStatus.Foreground = new SolidColorBrush(Color.FromRgb(178, 34, 34));
                    }
                });
            });
        }
    }

    public class ButtonRowEditor : UserControl
    {
        private int _index;
        private readonly Action<ButtonRowEditor> _removeAction;
        private readonly SKBitmap _defaultIcon;

        private TextBlock _headerLbl;
        private Button _removeBtn;
        private TextBox _label, _topic, _payload, _iconPath;
        private ComboBox _qos;
        private CheckBox _retain, _chkWhiteTransparent, _chkBlackTransparent, _chkStretch;
        private Image _iconPreview;

        public ButtonRowEditor(int index, Action<ButtonRowEditor> removeAction, SKBitmap defaultIcon)
        {
            _index = index;
            _removeAction = removeAction;
            _defaultIcon = defaultIcon;

            Build();
        }

        private void Build()
        {
            var border = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0)
            };

            var outer = new Grid { RowSpacing = 8 };
            outer.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            outer.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var header = new Grid { ColumnSpacing = 6 };
            header.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            _headerLbl = new TextBlock
            {
                Text = "Button " + (_index + 1),
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_headerLbl, 0);
            header.Children.Add(_headerLbl);
            _removeBtn = new Button
            {
                Content = "✕",
                Width = 26,
                Height = 24,
                Padding = new Thickness(0)
            };
            _removeBtn.Click += (s, e) => _removeAction?.Invoke(this);
            Grid.SetColumn(_removeBtn, 1);
            header.Children.Add(_removeBtn);
            Grid.SetRow(header, 0);
            outer.Children.Add(header);

            var contentGrid = new Grid { ColumnSpacing = 10 };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

            _iconPreview = new Image
            {
                Width = 56,
                Height = 56,
                Stretch = Stretch.Uniform,
                Source = _defaultIcon != null ? ImageUtils.ToAvalonia(_defaultIcon) : null,
                VerticalAlignment = VerticalAlignment.Top
            };
            var previewBorder = new Border
            {
                Width = 60,
                Height = 60,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                CornerRadius = new CornerRadius(4),
                Child = _iconPreview
            };
            Grid.SetColumn(previewBorder, 0);
            contentGrid.Children.Add(previewBorder);

            var lines = new StackPanel { Spacing = 6 };

            var line1 = new Grid { ColumnSpacing = 6 };
            line1.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            line1.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(40, GridUnitType.Star)));
            line1.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            line1.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(60, GridUnitType.Star)));
            line1.Children.Add(MidLabel("Description:"));
            _label = new TextBox();
            Grid.SetColumn(_label, 1);
            line1.Children.Add(_label);
            var lblTopic = MidLabel("Topic:");
            Grid.SetColumn(lblTopic, 2);
            line1.Children.Add(lblTopic);
            _topic = new TextBox();
            Grid.SetColumn(_topic, 3);
            line1.Children.Add(_topic);
            lines.Children.Add(line1);

            var line2 = new Grid { ColumnSpacing = 6 };
            line2.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            line2.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            line2.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            line2.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            line2.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            line2.Children.Add(MidLabel("Payload:"));
            _payload = new TextBox();
            Grid.SetColumn(_payload, 1);
            line2.Children.Add(_payload);
            var lblQos = MidLabel("QoS:");
            Grid.SetColumn(lblQos, 2);
            line2.Children.Add(lblQos);
            _qos = new ComboBox { MinWidth = 60 };
            _qos.Items.Add("0");
            _qos.Items.Add("1");
            _qos.Items.Add("2");
            _qos.SelectedIndex = 0;
            Grid.SetColumn(_qos, 3);
            line2.Children.Add(_qos);
            _retain = new CheckBox { Content = "Retain", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(_retain, 4);
            line2.Children.Add(_retain);
            lines.Children.Add(line2);

            var line3 = new Grid { ColumnSpacing = 6 };
            line3.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            line3.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            line3.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            line3.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            line3.Children.Add(MidLabel("Icon (ICO / PNG / JPG / BMP):"));
            _iconPath = new TextBox { IsReadOnly = true };
            Grid.SetColumn(_iconPath, 1);
            line3.Children.Add(_iconPath);
            var browseBtn = new Button { Content = "Browse…" };
            browseBtn.Click += OnBrowse;
            Grid.SetColumn(browseBtn, 2);
            line3.Children.Add(browseBtn);
            var clearBtn = new Button { Content = "Clear" };
            clearBtn.Click += (s, e) =>
            {
                _iconPath.Text = "";
                _iconPreview.Source = _defaultIcon != null ? ImageUtils.ToAvalonia(_defaultIcon) : null;
                _iconPreview.Stretch = Stretch.Uniform;
            };
            Grid.SetColumn(clearBtn, 3);
            line3.Children.Add(clearBtn);
            lines.Children.Add(line3);

            var line4 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            _chkWhiteTransparent = new CheckBox { Content = "White→Transparent" };
            _chkBlackTransparent = new CheckBox { Content = "Black→Transparent" };
            _chkStretch = new CheckBox { Content = "Stretch image" };
            _chkWhiteTransparent.IsCheckedChanged += (s, e) => RefreshPreview();
            _chkBlackTransparent.IsCheckedChanged += (s, e) => RefreshPreview();
            _chkStretch.IsCheckedChanged += (s, e) =>
            {
                _iconPreview.Stretch = _chkStretch.IsChecked == true ? Stretch.Fill : Stretch.Uniform;
            };
            line4.Children.Add(_chkWhiteTransparent);
            line4.Children.Add(_chkBlackTransparent);
            line4.Children.Add(_chkStretch);
            lines.Children.Add(line4);

            Grid.SetColumn(lines, 1);
            contentGrid.Children.Add(lines);

            Grid.SetRow(contentGrid, 1);
            outer.Children.Add(contentGrid);

            border.Child = outer;
            Content = border;
        }

        private TextBlock MidLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private async void OnBrowse(object sender, EventArgs e)
        {
            var top = TopLevel.GetTopLevel(this);
            if (top == null) return;
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose icon for button " + (_index + 1),
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Icons & Images")
                    {
                        Patterns = new[] { "*.ico", "*.png", "*.jpg", "*.jpeg", "*.bmp" }
                    }
                }
            });
            if (files != null && files.Count > 0)
            {
                try
                {
                    _iconPath.Text = files[0].Path.LocalPath;
                }
                catch
                {
                    _iconPath.Text = "";
                }
                RefreshPreview();
            }
        }

        private void RefreshPreview()
        {
            var path = _iconPath.Text.Trim();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _iconPreview.Source = _defaultIcon != null ? ImageUtils.ToAvalonia(_defaultIcon) : null;
                _iconPreview.Stretch = Stretch.Uniform;
                return;
            }
            var bmp = ImageUtils.Load(path);
            if (bmp == null)
            {
                _iconPreview.Source = _defaultIcon != null ? ImageUtils.ToAvalonia(_defaultIcon) : null;
                return;
            }
            using (bmp)
            {
                ImageUtils.ApplyTransparency(bmp, _chkWhiteTransparent.IsChecked == true, _chkBlackTransparent.IsChecked == true);
                _iconPreview.Source = ImageUtils.ToAvalonia(bmp);
            }
        }

        public void SetIndex(int index)
        {
            _index = index;
            _headerLbl.Text = "Button " + (index + 1);
        }

        public void SetRemoveEnabled(bool enabled)
        {
            _removeBtn.IsVisible = enabled;
        }

        public void Bind(ButtonConfig cfg)
        {
            if (cfg == null) cfg = new ButtonConfig();
            _label.Text = cfg.Label ?? "";
            _topic.Text = cfg.Topic ?? "";
            _payload.Text = cfg.Payload ?? "";
            _qos.SelectedIndex = Math.Max(0, Math.Min(2, cfg.Qos));
            _retain.IsChecked = cfg.Retain;
            _iconPath.Text = cfg.IconPath ?? "";
            _chkWhiteTransparent.IsChecked = cfg.MakeWhiteTransparent;
            _chkBlackTransparent.IsChecked = cfg.MakeBlackTransparent;
            _chkStretch.IsChecked = cfg.StretchImage;
            RefreshPreview();
        }

        public ButtonConfig Collect()
        {
            return new ButtonConfig
            {
                Label = _label.Text.Trim(),
                Topic = _topic.Text.Trim(),
                Payload = _payload.Text,
                Qos = _qos.SelectedIndex,
                Retain = _retain.IsChecked == true,
                IconPath = _iconPath.Text,
                MakeWhiteTransparent = _chkWhiteTransparent.IsChecked == true,
                MakeBlackTransparent = _chkBlackTransparent.IsChecked == true,
                StretchImage = _chkStretch.IsChecked == true
            };
        }
    }
}
