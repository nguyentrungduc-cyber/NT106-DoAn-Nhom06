using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SecureChat.Client.Forms.Settings
{
    public class frmPrivacySecurity : Form
    {
        private static readonly Color C_BG = Color.White;
        private static readonly Color C_TEXT = Color.FromArgb(0x1F, 0x2D, 0x3D);
        private static readonly Color C_SUB = Color.FromArgb(0x7A, 0x8A, 0x99);
        private static readonly Color C_ACCENT = Color.FromArgb(0x33, 0x99, 0xFF);
        private static readonly Color C_HOVER = Color.FromArgb(0xF2, 0xF5, 0xF9);
        private static readonly Color C_STATUS_ON = Color.FromArgb(0x33, 0x99, 0xFF);
        private static readonly Color C_BORDER = Color.FromArgb(0xE8, 0xEC, 0xF1);

        private TableLayoutPanel _table = null!;
        private Label _lblTwoStepStatus = null!;
        private Label _lblAutoDeleteStatus = null!;
        private Label _lblLoginEmail = null!;
        private Label _lblBlocked = null!;

        private bool _isTwoStepOn;
        private string _pendingEmailCode = string.Empty;

        public frmPrivacySecurity()
        {
            InitializeComponent();
            BuildUI();
            Load += (_, __) => UpdateSecurityUI();
        }

        private void InitializeComponent() { }

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
            BackColor = C_BG;
            Font = new Font("Segoe UI", 10f);
            DoubleBuffered = true;

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = C_BG,
                Padding = new Padding(12, 8, 12, 12)
            };
            Controls.Add(scroll);

            _table = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = C_BG,
            };
            _table.RowStyles.Clear();
            scroll.Controls.Add(_table);

            AddHeaderRow();
            AddSectionHeader("Security");

            _lblTwoStepStatus = AddActionRow("Two-Step Verification", "mini_lock", "Off", () => OpenTwoStepFlow());
            _lblAutoDeleteStatus = AddActionRow("Auto-Delete Messages", "input_autodelete", "Off", () => ChooseAutoDelete());
            _lblLoginEmail = AddActionRow("Login Email", "account_check", "hi*****@gmail.com", () => ChangeEmail());
            _lblBlocked = AddActionRow("Blocked users", "info_block", "None", () => MessageBox.Show("No blocked users.", "Info"));

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

            UpdateSecurityUI();
        }

        private void AddSectionHeader(string text)
        {
            var lbl = new Label
            {
                Text = text,
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI Semibold", 11f),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 6, 0, 4)
            };
            var row = new Panel { Dock = DockStyle.Top, Height = lbl.PreferredHeight + 8, BackColor = C_BG, Padding = new Padding(0, 2, 0, 2) };
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
                ForeColor = C_TEXT,
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
                ForeColor = C_TEXT,
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
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 10.5f),
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            var lblStatus = new Label
            {
                Text = status,
                ForeColor = C_ACCENT,
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
                c.MouseEnter += (_, __) => row.BackColor = C_HOVER;
                c.MouseLeave += (_, __) => row.BackColor = Color.Transparent;
            }
            row.Click += (_, __) => onClick();
            row.MouseEnter += (_, __) => row.BackColor = C_HOVER;
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
                BackColor = C_BORDER,
                Margin = new Padding(0, 4, 0, 8)
            };
            _table.Controls.Add(sep);
        }

        private void AddPrivacyOption(string text)
        {
            AddActionRow(text, "info_rights_lock", "Everybody", () => CyclePrivacy(text));
        }

        private void CyclePrivacy(string key)
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
                        if (child is Label l2 && l2.ForeColor == C_ACCENT) statusLbl = l2;
                    }
                    if (textLbl != null && statusLbl != null && textLbl.Text == key)
                    {
                        using var dlg = new Form
                        {
                            Text = key,
                            Size = new Size(260, 200),
                            StartPosition = FormStartPosition.CenterParent,
                            BackColor = C_BG,
                            ForeColor = C_TEXT,
                            FormBorderStyle = FormBorderStyle.FixedDialog
                        };
                        var radios = new[] { "Everybody", "My contacts", "Nobody" };
                        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12, 12, 12, 12) };
                        RadioButton? selected = null;
                        foreach (var r in radios)
                        {
                            var rb = new RadioButton { Text = r, ForeColor = C_TEXT, BackColor = C_BG, AutoSize = true, Checked = statusLbl.Text == r };
                            if (rb.Checked) selected = rb;
                            panel.Controls.Add(rb);
                        }
                        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = false, Width = 80, Height = 30, Padding = new Padding(6, 2, 6, 2) };
                        btnOk.FlatStyle = FlatStyle.Flat;
                        btnOk.FlatAppearance.BorderSize = 0;
                        btnOk.BackColor = C_ACCENT;
                        btnOk.ForeColor = Color.White;
                        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(0, 6, 12, 6) };
                        bottom.BackColor = C_BG;
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
                                    break;
                                }
                            }
                        }
                        return;
                    }
                }
            }
        }

        private void OpenTwoStepFlow()
        {
            // If already enabled, go directly to manage
            if (CheckTwoStepExists())
            {
                using var manageExisting = new frmTwoStepManage(() => { _isTwoStepOn = false; SaveTwoStepState(false); UpdateSecurityUI(); });
                manageExisting.ShowDialog(this);
                return;
            }

            using var start = new frmTwoStepStart();
            if (start.ShowDialog(this) == DialogResult.OK)
            {
                using var setPwd = new frmTwoStepPassword();
                if (setPwd.ShowDialog(this) == DialogResult.OK)
                {
                    // Mark as enabled immediately after successful password set
                    _isTwoStepOn = true;
                    SaveTwoStepState(true);
                    UpdateSecurityUI();

                    // Allow user to manage/disable if desired
                    using var manage = new frmTwoStepManage(() => { _isTwoStepOn = false; SaveTwoStepState(false); UpdateSecurityUI(); });
                    manage.ShowDialog(this);
                }
            }
        }

        private void ChooseAutoDelete()
        {
            var items = new[] { "Off", "After 1 day", "After 1 week", "After 1 month" };
            using var dlg = new Form
            {
                Text = "Auto-Delete",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(240, 220),
                BackColor = C_BG,
                Font = new Font("Segoe UI", 10f),
                ForeColor = C_TEXT
            };
            var list = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                ForeColor = C_TEXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10.5f),
                IntegralHeight = false,
                ItemHeight = 28
            };
            list.Items.AddRange(items);
            list.SelectedIndexChanged += (_, __) => { _lblAutoDeleteStatus.Text = list.SelectedItem?.ToString() ?? "Off"; dlg.DialogResult = DialogResult.OK; dlg.Close(); };
            dlg.Controls.Add(list);
            dlg.ShowDialog(this);
        }

        private void ChangeEmail()
        {
            _pendingEmailCode = GenerateCode();
            MessageBox.Show(this, "Login code sent to email", "Info");

            if (!PromptVerifyCode()) return;

            var (newEmailResult, newEmailValue) = PromptNewEmail();
            if (newEmailResult == DialogResult.OK && !string.IsNullOrWhiteSpace(newEmailValue))
            {
                _lblLoginEmail.Text = newEmailValue;
            }
        }

        private bool PromptVerifyCode()
        {
            using var verify = new Form
            {
                Text = "Verify Email",
                Size = new Size(320, 160),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = C_BG,
                ForeColor = C_TEXT,
                FormBorderStyle = FormBorderStyle.FixedDialog
            };
            var lbl = new Label { Text = "Enter code", ForeColor = C_TEXT, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12, 12, 12, 4) };
            var tb = new TextBox { Dock = DockStyle.Top, BackColor = Color.White, ForeColor = C_TEXT, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(12), Height = 28 };
            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = false, Width = 90, Height = 32, Anchor = AnchorStyles.Right, Padding = new Padding(6, 2, 6, 2), BackColor = C_ACCENT, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnOk.FlatAppearance.BorderSize = 0;
            var pnlBtn = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(0, 4, 12, 8) };
            pnlBtn.BackColor = C_BG;
            pnlBtn.Controls.Add(btnOk);
            btnOk.Location = new Point(pnlBtn.Width - btnOk.Width, 4);
            pnlBtn.Resize += (_, __) => btnOk.Location = new Point(pnlBtn.Width - btnOk.Width, 4);
            verify.Controls.Add(pnlBtn);
            verify.Controls.Add(tb);
            verify.Controls.Add(lbl);

            var res = verify.ShowDialog(this);
            if (res != DialogResult.OK) return false;

            var inputCode = tb.Text?.Trim();
            if (string.IsNullOrWhiteSpace(inputCode) || !string.Equals(inputCode, _pendingEmailCode, StringComparison.Ordinal))
            {
                MessageBox.Show(this, "Invalid code", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

        private (DialogResult result, string value) PromptNewEmail()
        {
            using var f = new Form
            {
                Text = "New Email",
                Size = new Size(320, 160),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = C_BG,
                ForeColor = C_TEXT,
                FormBorderStyle = FormBorderStyle.FixedDialog
            };
            var lbl = new Label { Text = "Enter new email", ForeColor = C_TEXT, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12, 12, 12, 4) };
            var tb = new TextBox { Dock = DockStyle.Top, BackColor = Color.White, ForeColor = C_TEXT, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(12), Height = 28 };
            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = false, Width = 90, Height = 32, Anchor = AnchorStyles.Right, Padding = new Padding(6, 2, 6, 2), BackColor = C_ACCENT, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnOk.FlatAppearance.BorderSize = 0;
            var pnlBtn = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(0, 4, 12, 8) };
            pnlBtn.BackColor = C_BG;
            pnlBtn.Controls.Add(btnOk);
            btnOk.Location = new Point(pnlBtn.Width - btnOk.Width, 4);
            pnlBtn.Resize += (_, __) => btnOk.Location = new Point(pnlBtn.Width - btnOk.Width, 4);
            f.Controls.Add(pnlBtn);
            f.Controls.Add(tb);
            f.Controls.Add(lbl);

            var res = f.ShowDialog(this);
            return (res, tb.Text?.Trim() ?? string.Empty);
        }

        private static Image LoadIcon(string key)
        {
            var file = key.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? key : key + ".png";
            return SettingsGlyphIcons.Create(file, 24);
        }

        private void UpdateTwoStepStatus()
        {
            if (_lblTwoStepStatus == null) return;
            _isTwoStepOn = CheckTwoStepExists();
            if (_isTwoStepOn)
            {
                _lblTwoStepStatus.Text = "On";
                _lblTwoStepStatus.ForeColor = C_STATUS_ON;
            }
            else
            {
                _lblTwoStepStatus.Text = "Off";
                _lblTwoStepStatus.ForeColor = Color.Gray;
            }
        }

        private bool CheckTwoStepExists()
        {
            var flagPath = Path.Combine(AppContext.BaseDirectory, "two_step.flag");
            return File.Exists(flagPath);
        }

        private void SaveTwoStepState(bool enabled)
        {
            var flagPath = Path.Combine(AppContext.BaseDirectory, "two_step.flag");
            try
            {
                if (enabled)
                {
                    File.WriteAllText(flagPath, "on");
                }
                else if (File.Exists(flagPath))
                {
                    File.Delete(flagPath);
                }
            }
            catch { /* ignore */ }
        }

        private static string GenerateCode()
        {
            var rnd = new Random();
            return rnd.Next(100000, 999999).ToString();
        }

        private void UpdateSecurityUI()
        {
            UpdateTwoStepStatus();
            // Add any additional security-related UI updates here if needed in the future.
        }
    }

    // Two-Step flow forms
    internal class frmTwoStepStart : Form
    {
        public frmTwoStepStart()
        {
            Text = "Two-Step Verification";
            Size = new Size(420, 230);
            MinimumSize = new Size(420, 230);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D);
            Font = new Font("Segoe UI", 10f);

            var lblTitle = new Label
            {
                Text = "Protect your account",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D),
                AutoSize = true,
                Location = new Point(20, 20)
            };

            var lblSub = new Label
            {
                Text = "Set a cloud password for two-step verification.",
                Font = new Font("Segoe UI", 9.8f),
                ForeColor = Color.FromArgb(0x7A, 0x8A, 0x99),
                AutoSize = false,
                Size = new Size(360, 48),
                Location = new Point(20, 52)
            };

            var btn = new Button { Text = "Create Password", AutoSize = false, Width = 160, Height = 34, FlatStyle = FlatStyle.Flat, Location = new Point(20, 104), BackColor = Color.FromArgb(0x33, 0x99, 0xFF), ForeColor = Color.White };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (_, __) => { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(lblTitle);
            Controls.Add(lblSub);
            Controls.Add(btn);
        }
    }

    internal class frmTwoStepPassword : Form
    {
        private TextBox _p1 = null!;
        private TextBox _p2 = null!;
        public frmTwoStepPassword()
        {
            Text = "Set Password";
            Size = new Size(360, 220);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D);
            Font = new Font("Segoe UI", 10f);
            var l1 = new Label { Text = "Enter new password", ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D), AutoSize = true, Location = new Point(12, 14) };
            _p1 = new TextBox { Location = new Point(12, 34), Width = 320, UseSystemPasswordChar = true, BackColor = Color.White, ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D), BorderStyle = BorderStyle.FixedSingle };
            var l2 = new Label { Text = "Re-enter new password", ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D), AutoSize = true, Location = new Point(12, 74) };
            _p2 = new TextBox { Location = new Point(12, 94), Width = 320, UseSystemPasswordChar = true, BackColor = Color.White, ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D), BorderStyle = BorderStyle.FixedSingle };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(220, 138), AutoSize = false, Width = 110, Height = 32, Padding = new Padding(6, 2, 6, 2), BackColor = Color.FromArgb(0x33, 0x99, 0xFF), ForeColor = Color.White };
            ok.FlatStyle = FlatStyle.Flat;
            ok.FlatAppearance.BorderSize = 0;
            ok.Click += (_, __) =>
            {
                if (_p1.Text != _p2.Text || string.IsNullOrWhiteSpace(_p1.Text))
                {
                    MessageBox.Show(this, "Passwords do not match.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.None;
                }
            };
            Controls.AddRange(new Control[] { l1, _p1, l2, _p2, ok });
        }
    }

    internal class frmTwoStepManage : Form
    {
        private readonly Action _onDisable;
        public frmTwoStepManage(Action onDisable)
        {
            _onDisable = onDisable ?? (() => { });
            Text = "Manage Password";
            Size = new Size(380, 220);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D);
            Font = new Font("Segoe UI", 10f);

            var lblTitle = new Label
            {
                Text = "Two-Step Verification",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D),
                AutoSize = true,
                Location = new Point(20, 18)
            };

            var btnChange = new Button { Text = "Change Password", AutoSize = false, Width = 170, Height = 34, FlatStyle = FlatStyle.Flat, Location = new Point(20, 58), ForeColor = Color.White, BackColor = Color.FromArgb(0x33, 0x99, 0xFF) };
            btnChange.FlatAppearance.BorderSize = 0;
            btnChange.Click += (_, __) => { using var pw = new frmTwoStepPassword(); pw.ShowDialog(this); };

            var btnDisable = new Button { Text = "Disable cloud password", AutoSize = false, Width = 170, Height = 34, FlatStyle = FlatStyle.Flat, Location = new Point(20, 102), ForeColor = Color.White, BackColor = Color.FromArgb(0xF1, 0x5B, 0x5B) };
            btnDisable.FlatAppearance.BorderSize = 0;
            btnDisable.Click += (_, __) => { _onDisable(); DialogResult = DialogResult.OK; Close(); };
            Controls.AddRange(new Control[] { lblTitle, btnChange, btnDisable });
        }

        public frmTwoStepManage() : this(() => { }) { }
    }
}
