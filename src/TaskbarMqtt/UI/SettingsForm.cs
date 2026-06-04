using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
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

        private TabControl _tabs;
        private TabPage _tabGeneral, _tabBroker, _tabButtons, _tabAbout;

        // General
        private RadioButton _rbPopup, _rbMulti;
        private CheckBox _chkAutoStart;
        private ComboBox _cbPopupSize;
        private TextBox _txIconPath;
        private Button _btnIconBrowse;

        // Broker
        private TextBox _txHost, _txPort, _txUser, _txPass, _txClientId, _txKeepAlive;
        private CheckBox _chkTls, _chkInvalidCerts;
        private Button _btnTestConn;
        private Label _lblConnStatus;

        // Buttons
        private FlowLayoutPanel _btnPanel;
        private List<ButtonRow> _btnRows = new List<ButtonRow>();

        private static readonly Color PanelBg = Color.FromArgb(250, 250, 250);

        public SettingsForm(AppConfig config, Action<AppConfig> onApply)
        {
            _original = config;
            _draft = CloneConfig(config);
            _onApply = onApply;

            BuildUi();
            PopulateFromDraft();
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
            BackColor = PanelBg;

            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tabGeneral = new TabPage("General");
            _tabBroker = new TabPage("Broker");
            _tabButtons = new TabPage("Buttons");
            _tabAbout = new TabPage("About");
            _tabs.TabPages.AddRange(new[] { _tabGeneral, _tabBroker, _tabButtons, _tabAbout });
            Controls.Add(_tabs);

            BuildGeneralTab();
            BuildBrokerTab();
            BuildButtonsTab();
            BuildAboutTab();

            var btnApply = new Button { Text = "Apply", Width = 86, Height = 26 };
            var btnOk = new Button { Text = "OK", Width = 86, Height = 26 };
            var btnCancel = new Button { Text = "Cancel", Width = 86, Height = 26, DialogResult = DialogResult.Cancel };

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
                RowCount = 3,
                Padding = new Padding(20, 20, 20, 8),
                BackColor = Color.White
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _rbPopup = new RadioButton { Text = "Popup panel (left-click to open)", AutoSize = true };
            _rbMulti = new RadioButton { Text = "One tray icon per button", AutoSize = true };
            _chkAutoStart = new CheckBox { Text = "Launch on Windows startup", AutoSize = true };

            var modePanel = new Panel { AutoSize = true };
            modePanel.Controls.Add(_rbPopup);
            modePanel.Controls.Add(_rbMulti);
            _rbMulti.Top = _rbPopup.Bottom + 4;

            layout.Controls.Add(new Label { Text = "Display mode:", AutoSize = true, Anchor = AnchorStyles.Top, Padding = new Padding(0, 3, 0, 0) }, 0, 0);
            layout.Controls.Add(modePanel, 1, 0);
            layout.Controls.Add(new Label { Text = "Autostart:", AutoSize = true, Anchor = AnchorStyles.Top, Padding = new Padding(0, 3, 0, 0) }, 0, 1);
            layout.Controls.Add(_chkAutoStart, 1, 1);

            _cbPopupSize = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Anchor = AnchorStyles.Left | AnchorStyles.Top };
            for (int v = 25; v <= 200; v += 25)
                _cbPopupSize.Items.Add(v + "%");
            layout.Controls.Add(new Label { Text = "Popup size:", AutoSize = true, Anchor = AnchorStyles.Top, Padding = new Padding(0, 3, 0, 0) }, 0, 2);
            layout.Controls.Add(_cbPopupSize, 1, 2);

            _txIconPath = new TextBox { Width = 220, Anchor = AnchorStyles.Left };
            _btnIconBrowse = new Button { Text = "Browse...", Width = 75, Height = 23, Margin = new Padding(6, 0, 0, 0) };
            _btnIconBrowse.Click += OnIconBrowse;
            var iconPanel = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
            iconPanel.Controls.Add(_txIconPath);
            iconPanel.Controls.Add(_btnIconBrowse);
            layout.Controls.Add(new Label { Text = "Custom tray icon:", AutoSize = true, Anchor = AnchorStyles.Top, Padding = new Padding(0, 3, 0, 0) }, 0, 3);
            layout.Controls.Add(iconPanel, 1, 3);

            _tabGeneral.Controls.Add(layout);
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
                BackColor = Color.White
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 9; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            _txHost = new TextBox { Width = 260, Anchor = AnchorStyles.Left };
            _txPort = new TextBox { Width = 70, Anchor = AnchorStyles.Left };
            _txUser = new TextBox { Width = 200, Anchor = AnchorStyles.Left };
            _txPass = new TextBox { Width = 200, Anchor = AnchorStyles.Left, UseSystemPasswordChar = true };
            _txClientId = new TextBox { Width = 260, Anchor = AnchorStyles.Left, Text = "TaskbarMqtt" };
            _txKeepAlive = new TextBox { Width = 70, Anchor = AnchorStyles.Left };
            _chkTls = new CheckBox { Text = "Use TLS", AutoSize = true };
            _chkInvalidCerts = new CheckBox { Text = "Allow self-signed certificates", AutoSize = true, Checked = true };

            var tlsPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0) };
            tlsPanel.Controls.Add(_chkTls);
            tlsPanel.Controls.Add(_chkInvalidCerts);

            _btnTestConn = new Button { Text = "Test Connection", Width = 130, Height = 26 };
            _btnTestConn.Click += OnTestConnection;
            _lblConnStatus = new Label { AutoSize = true, ForeColor = SystemColors.ControlDarkDark, Padding = new Padding(0, 5, 0, 0) };

            layout.Controls.Add(new Label { Text = "Host:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            layout.Controls.Add(_txHost, 1, 0);
            layout.Controls.Add(new Label { Text = "Port:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            layout.Controls.Add(_txPort, 1, 1);
            layout.Controls.Add(new Label { Text = "Username:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            layout.Controls.Add(_txUser, 1, 2);
            layout.Controls.Add(new Label { Text = "Password:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
            layout.Controls.Add(_txPass, 1, 3);
            layout.Controls.Add(new Label { Text = "Client ID:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
            layout.Controls.Add(_txClientId, 1, 4);
            layout.Controls.Add(new Label { Text = "Keep alive (s):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
            layout.Controls.Add(_txKeepAlive, 1, 5);
            layout.Controls.Add(new Label { Text = "Encryption (TLS/SSL):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
            layout.Controls.Add(tlsPanel, 1, 6);
            layout.Controls.Add(new Label { Text = "", Anchor = AnchorStyles.Left }, 0, 7);
            layout.Controls.Add(_btnTestConn, 1, 7);
            layout.Controls.Add(new Label { Text = "", Anchor = AnchorStyles.Left }, 0, 8);
            layout.Controls.Add(_lblConnStatus, 1, 8);

            _tabBroker.Controls.Add(layout);
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
                BackColor = Color.White
            };
            _btnPanel.Resize += (s, e) => UpdateRowWidths();
            _tabButtons.Controls.Add(_btnPanel);

            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Color.White };
            var btnAdd = new Button { Text = "+ Add Button", Width = 110, Height = 26, Left = 14, Top = 9 };
            btnAdd.Click += OnAddButton;
            bottomPanel.Controls.Add(btnAdd);
            _tabButtons.Controls.Add(bottomPanel);
        }

        private void BuildAboutTab()
        {
            var box = new GroupBox
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 16, 20, 16),
                BackColor = Color.White
            };

            var lines = new[]
            {
                "Taskbar MQTT FastSwitch",
                "",
                "Version 1.1",
                "",
                "Created by MiniMax V3",
                "Made working by OpenCode's Big Pickle",
                "Supervised by yustAnotherUser",
                "",
                "A lightweight tray application that publishes",
                "pre-configured MQTT messages at the click of a button.",
            };

            int y = 24;
            using (var boldFont = new Font("Segoe UI", 12F, FontStyle.Bold))
            using (var regFont = new Font("Segoe UI", 9F))
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    var lbl = new Label
                    {
                        Text = lines[i],
                        AutoSize = true,
                        Location = new Point(24, y),
                        Font = i == 0 ? boldFont : regFont
                    };
                    box.Controls.Add(lbl);
                    y += lbl.Height + (i == 0 ? 6 : 2);
                }
            }

            _tabAbout.Controls.Add(box);
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
            var row = new ButtonRow(_btnRows.Count, RemoveButtonRow);
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

            _txHost.Text = _draft.Broker.Host ?? "";
            _txPort.Text = _draft.Broker.Port.ToString();
            _txUser.Text = _draft.Broker.Username ?? "";
            _txPass.Text = _draft.Broker.Password ?? "";
            _txClientId.Text = string.IsNullOrEmpty(_draft.Broker.ClientId) ? "TaskbarMqtt" : _draft.Broker.ClientId;
            _txKeepAlive.Text = _draft.Broker.KeepAliveSeconds.ToString();
            _txIconPath.Text = _draft.IconPath ?? "";
            _chkTls.Checked = _draft.Broker.UseTls;
            _chkInvalidCerts.Checked = _draft.Broker.AllowInvalidCerts;

            _btnRows.Clear();
            _btnPanel.SuspendLayout();
            _btnPanel.Controls.Clear();
            int count = Math.Max(1, _draft.Buttons.Count);
            for (int i = 0; i < count; i++)
            {
                var row = new ButtonRow(i, RemoveButtonRow);
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
            private int _index;
            private TextBox _label, _topic, _payload, _iconPath;
            private ComboBox _qos;
            private CheckBox _retain;
            private Button _browseBtn, _clearBtn, _removeBtn;
            private Label _headerLbl;
            private Label _lblDesc, _lblTopic, _lblQos, _lblPayload, _lblIcon;
            private PictureBox _iconPreview;
            private Panel _header;
            private readonly ToolTip _tooltip;
            private readonly Action<ButtonRow> _removeAction;

            private static readonly Color HeaderBg = Color.FromArgb(245, 245, 245);

            public ButtonRow(int index, Action<ButtonRow> removeAction)
            {
                _index = index;
                _removeAction = removeAction;
                _tooltip = new ToolTip();
                Height = 145;
                Build();
            }

            private void Build()
            {
                BorderStyle = BorderStyle.None;
                BackColor = Color.White;
                Margin = new Padding(0, 0, 0, 8);

                _header = new Panel
                {
                    BackColor = HeaderBg
                };

                _headerLbl = new Label
                {
                    Text = "Button " + (_index + 1),
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    AutoSize = true
                };
                _header.Controls.Add(_headerLbl);

                _removeBtn = new Button
                {
                    Text = "\u2715",
                    Width = 22,
                    Height = 22,
                    FlatStyle = FlatStyle.Flat,
                    TabStop = false,
                    ForeColor = Color.FromArgb(120, 120, 120)
                };
                _removeBtn.FlatAppearance.BorderSize = 0;
                _removeBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 230, 230);
                _removeBtn.Click += (s, e) => _removeAction?.Invoke(this);
                _header.Controls.Add(_removeBtn);
                Controls.Add(_header);

                _iconPreview = new PictureBox
                {
                    Width = 44,
                    Height = 44,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };
                Controls.Add(_iconPreview);

                _lblDesc = new Label { Text = "Description:", AutoSize = true };
                _lblTopic = new Label { Text = "Topic:", AutoSize = true };
                _lblQos = new Label { Text = "QoS:", AutoSize = true };
                _lblPayload = new Label { Text = "Payload:", AutoSize = true };
                _lblIcon = new Label { Text = "Icon (ICO / PNG / JPG / BMP):", AutoSize = true };

                _label = new TextBox { Height = 22 };
                _topic = new TextBox { Height = 22 };
                _qos = new ComboBox { Height = 22, DropDownStyle = ComboBoxStyle.DropDownList };
                _qos.Items.AddRange(new object[] { "0", "1", "2" });
                _qos.SelectedIndex = 0;

                _payload = new TextBox { Height = 22 };
                _retain = new CheckBox { Text = "Retain", AutoSize = true };

                _iconPath = new TextBox { Height = 22, ReadOnly = true };
                _browseBtn = new Button { Text = "Browse\u2026", Height = 23 };
                _browseBtn.Click += OnBrowse;
                _clearBtn = new Button { Text = "Clear", Height = 23 };
                _clearBtn.Click += (s, e) => { _iconPath.Text = ""; _iconPreview.Image = null; };

                Controls.AddRange(new Control[] {
                    _lblDesc, _lblTopic, _lblQos, _lblPayload, _lblIcon,
                    _label, _topic, _qos, _payload, _retain,
                    _iconPath, _browseBtn, _clearBtn
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
                    IconPath = _iconPath.Text
                };
            }
        }
    }
}
