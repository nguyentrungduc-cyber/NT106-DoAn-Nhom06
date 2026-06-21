using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SecureChat.Client.Services;
using SecureChat.Client.Services.Api;
using SecureChat.DTOs;

namespace SecureChat.Client.Forms.Settings
{
    public class frmPrivacySecurity : Form
    {
        // Colors read from TG at paint time

        private TableLayoutPanel _table = null!;
        private Label _lblAutoDeleteStatus = null!;
        private Label _lblLoginEmail = null!;
        private Label _lblBlocked = null!;

        private readonly PrivacyService _privacyService = new();
        private PrivacySettingsDto _settings = new();

        private static readonly Dictionary<string, string> PrivacyKeyMap = new()
        {
            ["Last seen online"] = "LastSeenPrivacy",
            ["Profile photos"] = "ProfilePhotoPrivacy",
            ["Forwarded messages"] = "ForwardedMessagesPrivacy",
            ["Calls"] = "CallsPrivacy",
            ["Voice messages"] = "VoiceMessagesPrivacy",
            ["Messages"] = "MessagesPrivacy",
            ["Birthday"] = "BirthdayPrivacy",
            ["Bio"] = "BioPrivacy"
        };

        public frmPrivacySecurity()
        {
            InitializeComponent();
            BuildUI();
            ThemeRefreshHelper.Hook(this);
            Load += async (_, __) => await LoadSettingsAsync();
        }

        private void InitializeComponent() { }

        private async Task LoadSettingsAsync()
        {
            var result = await _privacyService.GetSettingsAsync();
            if (result.Success && result.Data is not null)
            {
                _settings = result.Data;
                ApplySettingsToUI();
            }

            var meResult = await ApiClient.Instance.GetAsync<UserResponse>("api/users/me");
            if (meResult.IsSuccess && meResult.Data is not null && !string.IsNullOrWhiteSpace(meResult.Data.Email))
                _lblLoginEmail.Text = meResult.Data.Email;
        }

        private void ApplySettingsToUI()
        {
            _lblAutoDeleteStatus.Text = AutoDeleteLabel(_settings.AutoDeleteMode);

            foreach (Control c in _table.Controls)
            {
                if (c is TableLayoutPanel p)
                {
                    Label? textLbl = null;
                    Label? statusLbl = null;
                    foreach (Control child in p.Controls)
                    {
                        if (child is Label l && l.TextAlign == ContentAlignment.MiddleLeft) textLbl = l;
                        if (child is Label l2 && l2.ForeColor == TG.CAccent) statusLbl = l2;
                    }
                    if (textLbl != null && statusLbl != null && PrivacyKeyMap.TryGetValue(textLbl.Text, out var prop))
                    {
                        var val = GetSettingValue(prop);
                        if (val is not null)
                            statusLbl.Text = val;
                    }
                }
            }
        }

        private string? GetSettingValue(string prop) => prop switch
        {
            "LastSeenPrivacy" => _settings.LastSeenPrivacy,
            "ProfilePhotoPrivacy" => _settings.ProfilePhotoPrivacy,
            "ForwardedMessagesPrivacy" => _settings.ForwardedMessagesPrivacy,
            "CallsPrivacy" => _settings.CallsPrivacy,
            "VoiceMessagesPrivacy" => _settings.VoiceMessagesPrivacy,
            "MessagesPrivacy" => _settings.MessagesPrivacy,
            "BirthdayPrivacy" => _settings.BirthdayPrivacy,
            "BioPrivacy" => _settings.BioPrivacy,
            _ => null
        };

        private void BuildUI()
        {
            Text = "Privacy and Security";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            HelpButton = false;
            ControlBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 780);
            BackColor = TG.WindowBg;
            Font = new Font("Segoe UI", 10f);
            DoubleBuffered = true;

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = TG.WindowBg,
                Padding = new Padding(12, 8, 12, 12)
            };
            Controls.Add(scroll);

            _table = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = TG.WindowBg,
            };
            _table.RowStyles.Clear();
            scroll.Controls.Add(_table);

            AddHeaderRow();
            AddSectionHeader("Security");

            _lblAutoDeleteStatus = AddActionRow("Auto-Delete Messages", "input_autodelete", "Off", () => ChooseAutoDelete());
            _lblLoginEmail = AddActionRow("Login Email", "account_check", "Loading...", () => ChangeEmail());
            _lblBlocked = AddActionRow("Blocked users", "info_block", "None", () => OpenBlockedUsers());

            AddDivider();
            AddSectionHeader("Privacy");

            AddPrivacyOption("Last seen online");
            AddPrivacyOption("Profile photos");
            AddPrivacyOption("Forwarded messages");
            AddPrivacyOption("Calls");
            AddPrivacyOption("Voice messages");
            AddPrivacyOption("Messages");
            AddPrivacyOption("Birthday");
            AddPrivacyOption("Bio");
        }

        private void AddSectionHeader(string text)
        {
            var lbl = new Label
            {
                Text = text,
                ForeColor = TG.TextPrimary,
                Font = new Font("Segoe UI Semibold", 11f),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 6, 0, 4)
            };
            var row = new Panel { Dock = DockStyle.Top, Height = lbl.PreferredHeight + 8, BackColor = TG.WindowBg, Padding = new Padding(0, 2, 0, 2) };
            row.Controls.Add(lbl);
            _table.Controls.Add(row);
        }

        private void AddHeaderRow()
        {
            var header = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 4),
                Margin = new Padding(0, 0, 0, 8)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var back = new PictureBox
            {
                Size = new Size(24, 24),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = LoadIcon("title_back"),
                Cursor = Cursors.Hand,
                Dock = DockStyle.Left,
                Margin = new Padding(4, 0, 8, 0),
                BackColor = Color.Transparent
            };
            back.Click += (_, __) => Close();

            var title = new Label
            {
                Text = "Privacy and Security",
                ForeColor = TG.TextPrimary,
                Font = new Font("Segoe UI Semibold", 13f),
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            var btnClose = new Button
            {
                Text = "X",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TG.TextPrimary,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 0),
                Padding = new Padding(6, 2, 6, 2),
                TabStop = false,
                Anchor = AnchorStyles.Right
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (_, __) => Close();

            var titleHost = new Panel { Dock = DockStyle.Fill, Height = 32, Padding = new Padding(0, 2, 0, 2), BackColor = Color.Transparent };
            titleHost.Controls.Add(title);
            titleHost.Controls.Add(back);

            header.Controls.Add(titleHost, 0, 0);
            header.Controls.Add(btnClose, 1, 0);
            _table.Controls.Add(header);
        }

        private Label AddActionRow(string text, string iconFile, string status, Action onClick)
        {
            var row = new TableLayoutPanel
            {
                ColumnCount = 3,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 4),
                Margin = new Padding(0, 0, 0, 6)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var icon = new PictureBox
            {
                Size = new Size(24, 24),
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = LoadIcon(iconFile),
                BackColor = Color.Transparent,
                Margin = new Padding(8, 4, 8, 4)
            };
            var lblText = new Label
            {
                Text = text,
                ForeColor = TG.TextPrimary,
                Font = new Font("Segoe UI", 10.5f),
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            var lblStatus = new Label
            {
                Text = status,
                ForeColor = TG.CAccent,
                Font = new Font("Segoe UI", 10.5f),
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent,
                Margin = new Padding(4, 0, 4, 0)
            };

            foreach (Control c in new Control[] { icon, lblText, lblStatus })
            {
                c.Click += (_, __) => onClick();
                c.MouseEnter += (_, __) => row.BackColor = TG.SidebarHover;
                c.MouseLeave += (_, __) => row.BackColor = Color.Transparent;
            }
            row.Click += (_, __) => onClick();
            row.MouseEnter += (_, __) => row.BackColor = TG.SidebarHover;
            row.MouseLeave += (_, __) => row.BackColor = Color.Transparent;

            row.Controls.Add(icon, 0, 0);
            row.Controls.Add(lblText, 1, 0);
            row.Controls.Add(lblStatus, 2, 0);
            _table.Controls.Add(row);
            return lblStatus;
        }

        private void AddDivider()
        {
            var sep = new Panel
            {
                Height = 1,
                Dock = DockStyle.Top,
                BackColor = TG.Divider,
                Margin = new Padding(0, 4, 0, 8)
            };
            _table.Controls.Add(sep);
        }

        private void AddPrivacyOption(string text)
        {
            var lbl = AddActionRow(text, "info_rights_lock", "Everybody", () => CyclePrivacy(text));
        }

        private async void CyclePrivacy(string key)
        {
            foreach (Control c in _table.Controls)
            {
                if (c is TableLayoutPanel p)
                {
                    Label? textLbl = null;
                    Label? statusLbl = null;
                    foreach (Control child in p.Controls)
                    {
                        if (child is Label l && l.TextAlign == ContentAlignment.MiddleLeft) textLbl = l;
                        if (child is Label l2 && l2.ForeColor == TG.CAccent) statusLbl = l2;
                    }
                    if (textLbl != null && statusLbl != null && textLbl.Text == key)
                    {
                        using var dlg = new Form
                        {
                            Text = key,
                            Size = new Size(260, 200),
                            StartPosition = FormStartPosition.CenterParent,
                            BackColor = TG.WindowBg,
                            ForeColor = TG.TextPrimary,
                            FormBorderStyle = FormBorderStyle.FixedDialog
                        };
                        var radios = new[] { "Everybody", "Contacts", "Nobody" };
                        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12, 12, 12, 12) };
                        foreach (var r in radios)
                        {
                            var rb = new RadioButton { Text = r, ForeColor = TG.TextPrimary, BackColor = TG.WindowBg, AutoSize = true, Checked = statusLbl.Text == r };
                            panel.Controls.Add(rb);
                        }
                        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = false, Width = 80, Height = 30, Padding = new Padding(6, 2, 6, 2) };
                        btnOk.FlatStyle = FlatStyle.Flat;
                        btnOk.FlatAppearance.BorderSize = 0;
                        btnOk.BackColor = TG.CAccent;
                        btnOk.ForeColor = Color.White;
                        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(0, 6, 12, 6) };
                        bottom.BackColor = TG.WindowBg;
                        bottom.Controls.Add(btnOk);
                        btnOk.Location = new Point(bottom.Width - btnOk.Width, 6);
                        bottom.Resize += (_, __) => btnOk.Location = new Point(bottom.Width - btnOk.Width, 6);
                        dlg.Controls.Add(panel);
                        dlg.Controls.Add(bottom);
                        if (dlg.ShowDialog(this) == DialogResult.OK)
                        {
                            foreach (var ctrl in panel.Controls)
                            {
                                if (ctrl is RadioButton rb && rb.Checked)
                                {
                                    statusLbl.Text = rb.Text;
                                    await SavePrivacyToApi(key, rb.Text);
                                    break;
                                }
                            }
                        }
                        return;
                    }
                }
            }
        }

        private async Task SavePrivacyToApi(string key, string value)
        {
            if (!PrivacyKeyMap.TryGetValue(key, out var prop))
                return;

            var update = new UpdatePrivacySettingsDto();
            switch (prop)
            {
                case "LastSeenPrivacy": update = update with { LastSeenPrivacy = value }; break;
                case "ProfilePhotoPrivacy": update = update with { ProfilePhotoPrivacy = value }; break;
                case "ForwardedMessagesPrivacy": update = update with { ForwardedMessagesPrivacy = value }; break;
                case "CallsPrivacy": update = update with { CallsPrivacy = value }; break;
                case "VoiceMessagesPrivacy": update = update with { VoiceMessagesPrivacy = value }; break;
                case "MessagesPrivacy": update = update with { MessagesPrivacy = value }; break;
                case "BirthdayPrivacy": update = update with { BirthdayPrivacy = value }; break;
                case "BioPrivacy": update = update with { BioPrivacy = value }; break;
            }

            var result = await _privacyService.UpdateSettingsAsync(update);
            if (result.Success && result.Data is not null)
                _settings = result.Data;
        }

        private async void ChooseAutoDelete()
        {
            var items = new[] { "Off", "After 1 day", "After 1 week", "After 1 month" };
            using var dlg = new Form
            {
                Text = "Auto-Delete",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(240, 220),
                BackColor = TG.WindowBg,
                Font = new Font("Segoe UI", 10f),
                ForeColor = TG.TextPrimary
            };
            var list = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = TG.WindowBg,
                ForeColor = TG.TextPrimary,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10.5f),
                IntegralHeight = false,
                ItemHeight = 28
            };
            list.Items.AddRange(items);

            list.SelectedIndexChanged += async (_, __) =>
            {
                var selected = list.SelectedItem?.ToString() ?? "Off";
                var mode = selected switch
                {
                    "After 1 day" => "TwentyFourHours",
                    "After 1 week" => "SevenDays",
                    "After 1 month" => "ThirtyDays",
                    _ => "Off"
                };
                _lblAutoDeleteStatus.Text = selected;
                var result = await _privacyService.UpdateSettingsAsync(new UpdatePrivacySettingsDto { AutoDeleteMode = mode });
                if (result.Success && result.Data is not null)
                    _settings = result.Data;
                dlg.DialogResult = DialogResult.OK;
                dlg.Close();
            };
            dlg.Controls.Add(list);
            dlg.ShowDialog(this);
        }

        private void ChangeEmail()
        {
            MessageBox.Show(this, "Email changes are managed through the profile settings.", "Info");
        }

        private async void OpenBlockedUsers()
        {
            var blockedResult = await ApiClient.Instance.GetAsync<List<BlockedUserItem>>("api/friends/blocked");
            if (!blockedResult.IsSuccess)
            {
                MessageBox.Show(this, $"Failed to load blocked users: {blockedResult.ErrorMessage}", "Error");
                return;
            }

            var blocked = blockedResult.Data ?? new List<BlockedUserItem>();
            _lblBlocked.Text = blocked.Count == 0 ? "None" : $"{blocked.Count} user(s)";

            if (blocked.Count == 0)
            {
                MessageBox.Show(this, "No blocked users.", "Blocked Users");
                return;
            }

            using var dlg = new Form
            {
                Text = "Blocked Users",
                Size = new Size(400, 350),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = TG.WindowBg,
                ForeColor = TG.TextPrimary,
                FormBorderStyle = FormBorderStyle.FixedDialog
            };

            var listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = TG.WindowBg,
                ForeColor = TG.TextPrimary,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10.5f),
                DisplayMember = "DisplayName"
            };
            listBox.Items.AddRange(blocked.ToArray());

            var btnUnblock = new Button
            {
                Text = "Unblock",
                Dock = DockStyle.Bottom,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0xF1, 0x5B, 0x5B),
                ForeColor = Color.White
            };
            btnUnblock.FlatAppearance.BorderSize = 0;
            btnUnblock.Click += async (_, __) =>
            {
                if (listBox.SelectedItem is BlockedUserItem selected)
                {
                    var delResult = await ApiClient.Instance.DeleteAsync($"api/friends/blocked/{selected.BlockID}");
                    if (delResult.IsSuccess)
                    {
                        listBox.Items.Remove(selected);
                        _lblBlocked.Text = listBox.Items.Count == 0 ? "None" : $"{listBox.Items.Count} user(s)";
                        if (listBox.Items.Count == 0)
                            dlg.Close();
                    }
                    else
                    {
                        MessageBox.Show(this, $"Failed to unblock: {delResult.ErrorMessage}", "Error");
                    }
                }
            };

            dlg.Controls.Add(listBox);
            dlg.Controls.Add(btnUnblock);
            dlg.ShowDialog(this);
        }

        private class BlockedUserItem
        {
            public string BlockID { get; set; } = "";
            public BlockedUserBlocked? Blocked { get; set; }
            public string DisplayName => Blocked?.DisplayName ?? Blocked?.Username ?? "Unknown";
        }

        private class BlockedUserBlocked
        {
            public string DisplayName { get; set; } = "";
            public string Username { get; set; } = "";
        }

        private static Image LoadIcon(string key)
        {
            var file = key.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? key : key + ".png";
            return SettingsGlyphIcons.Create(file, 24);
        }

        private static string AutoDeleteLabel(string mode) => mode switch
        {
            "TwentyFourHours" => "After 1 day",
            "SevenDays" => "After 1 week",
            "ThirtyDays" => "After 1 month",
            _ => "Off"
        };
        private void OnThemeChanged()
        {
            if (InvokeRequired) { Invoke(new Action(OnThemeChanged)); return; }
            BackColor = TG.WindowBg;
            this.Invalidate(true);
            foreach (Control c in this.Controls)
                ApplyThemeRecursive(c);
        }

        private static void ApplyThemeRecursive(Control c)
        {
            if (c.BackColor != Color.Transparent &&
                c.BackColor != TG.Blue &&
                c.BackColor != TG.SidebarActive)
                c.BackColor = TG.WindowBg;
            if (c.Tag is string t && t == "secondary-text")
                c.ForeColor = TG.TextSecondary;
            c.Invalidate();
            foreach (Control child in c.Controls)
                ApplyThemeRecursive(child);
        }

    }
}
