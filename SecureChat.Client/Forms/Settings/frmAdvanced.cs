using System;
using System.Drawing;
using System.Windows.Forms;
using SecureChat.Client.Services;
using SecureChat.Client.Settings;

namespace SecureChat.Client.Forms.Settings
{
    public class frmAdvanced : Form
    {
        // Colors read from TG at paint time

        private Panel _content = null!;

        private Panel _header = null!;
        private Label _secDataStorage = null!;
        private Panel _rowDownloadPath = null!;
        private Panel _rowDownloads = null!;
        private Panel _rowAskEachFile = null!;
        private Panel _divider1 = null!;

        private Label _secWindowTitle = null!;
        private ToggleCheckRow _chkShowChatName = null!;
        private ToggleCheckRow _chkTotalUnreadCount = null!;
        private ToggleCheckRow _chkUseSystemWindowFrame = null!;
        private Panel _divider2 = null!;

        private Label _secSystem = null!;
        private ToggleCheckRow _chkShowTaskbarIcon = null!;
        private ToggleCheckRow _chkUseMonochromeIcon = null!;

        private Label _lblDownloadPath = null!;
        private CheckBox _chkAskDownloadPath = null!;

        public frmAdvanced()
        {
            NightModeService.ThemeChanged += OnThemeChanged;
            FormClosed += (_, __) => NightModeService.ThemeChanged -= OnThemeChanged;
            InitializeComponent();
            BuildUI();
            Load += (_, __) => LoadSettings();
        }

        private void InitializeComponent() { }

        private void BuildUI()
        {
            Text = "Advanced";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            HelpButton = false;
            ControlBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 760);
            BackColor = TG.WindowBg;
            SecureChat.Client.Services.ThemeRefreshHelper.Hook(this);
            Font = new Font("Segoe UI", 10f);
            DoubleBuffered = true;

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = TG.WindowBg,
                AutoScroll = true
            };
            Controls.Add(scroll);

            _content = new Panel
            {
                Dock = DockStyle.Top,
                Height = 900,
                BackColor = TG.WindowBg
            };
            scroll.Controls.Add(_content);

            _header = CreateHeaderRow();
            _secDataStorage = CreateSectionLabel("Data and storage");
            _rowDownloadPath = CreateActionRow("Download path", "folders.png", OpenDownloadPathOptions, out _lblDownloadPath, "Default folder");
            _rowDownloads = CreateActionRow("Downloads", "downloads_arrow.png", OpenDownloads, out _);
            _rowAskEachFile = CreateAskDownloadToggleRow(out _chkAskDownloadPath);
            _divider1 = CreateSectionDivider();

            _secWindowTitle = CreateSectionLabel("Window title bar");
            _chkShowChatName = new ToggleCheckRow("Show chat name", true, TG.WindowBg, TG.TextPrimary, TG.CAccent, TG.Divider);
            _chkTotalUnreadCount = new ToggleCheckRow("Total unread count", true, TG.WindowBg, TG.TextPrimary, TG.CAccent, TG.Divider);
            _chkUseSystemWindowFrame = new ToggleCheckRow("Use system window frame", false, TG.WindowBg, TG.TextPrimary, TG.CAccent, TG.Divider);
            _divider2 = CreateSectionDivider();

            _secSystem = CreateSectionLabel("System integration");
            _chkShowTaskbarIcon = new ToggleCheckRow("Show taskbar icon", true, TG.WindowBg, TG.TextPrimary, TG.CAccent, TG.Divider);
            _chkUseMonochromeIcon = new ToggleCheckRow("Use monochrome icon", true, TG.WindowBg, TG.TextPrimary, TG.CAccent, TG.Divider);

            _content.Controls.AddRange(new Control[]
            {
                _header,
                _secDataStorage,
                _rowDownloadPath,
                _rowDownloads,
                _rowAskEachFile,
                _divider1,
                _secWindowTitle,
                _chkShowChatName,
                _chkTotalUnreadCount,
                _chkUseSystemWindowFrame,
                _divider2,
                _secSystem,
                _chkShowTaskbarIcon,
                _chkUseMonochromeIcon
            });

            Resize += (_, __) => LayoutRows();
            _content.Resize += (_, __) => LayoutRows();

            _chkAskDownloadPath.CheckedChanged += (_, __) =>
            {
                UpdateDownloadPathVisibility();
                SaveSettings();
            };
            _chkShowChatName.CheckedChanged += (_, __) => SaveSettings();
            _chkTotalUnreadCount.CheckedChanged += (_, __) => SaveSettings();
            _chkUseSystemWindowFrame.CheckedChanged += (_, __) => SaveSettings();
            _chkShowTaskbarIcon.CheckedChanged += (_, __) => SaveSettings();
            _chkUseMonochromeIcon.CheckedChanged += (_, __) => SaveSettings();

            LayoutRows();
            UiLocalization.ApplyToForm(this);
        }

        private void LayoutRows()
        {
            int width = _content.ClientSize.Width;
            int y = 0;

            Place(_header, 0, y, width, 74); y += 74;
            y += 12;

            Place(_secDataStorage, 0, y, width, 42); y += 42;

            if (_rowDownloadPath.Visible)
            {
                Place(_rowDownloadPath, 0, y, width, 50);
                y += 50;
            }

            Place(_rowDownloads, 0, y, width, 50); y += 50;
            Place(_rowAskEachFile, 0, y, width, 54); y += 54;

            Place(_divider1, 0, y, width, 10); y += 10;

            Place(_secWindowTitle, 0, y, width, 42); y += 42;
            Place(_chkShowChatName, 0, y, width, 52); y += 52;
            Place(_chkTotalUnreadCount, 0, y, width, 52); y += 52;
            Place(_chkUseSystemWindowFrame, 0, y, width, 52); y += 52;

            Place(_divider2, 0, y, width, 10); y += 10;

            Place(_secSystem, 0, y, width, 42); y += 42;
            Place(_chkShowTaskbarIcon, 0, y, width, 52); y += 52;
            Place(_chkUseMonochromeIcon, 0, y, width, 52); y += 52;

            _content.Height = y + 18;
        }

        private static void Place(Control control, int x, int y, int w, int h)
        {
            control.Location = new Point(x, y);
            control.Size = new Size(w, h);
        }

        private Panel CreateHeaderRow()
        {
            var header = new Panel { BackColor = TG.WindowBg };

            var back = new PictureBox
            {
                Size = new Size(24, 24),
                Location = new Point(10, 22),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = SettingsGlyphIcons.Create("title_back.png", 24),
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            back.Click += (_, __) => Close();

            var title = new Label
            {
                Text = "Advanced",
                Font = new Font("Segoe UI Semibold", 13f),
                ForeColor = TG.TextPrimary,
                AutoSize = true,
                Location = new Point(46, 20),
                BackColor = Color.Transparent
            };

            var close = new Button
            {
                Text = "X",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TG.TextSecondary,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                TabStop = false,
                Padding = new Padding(6, 2, 6, 2)
            };
            close.FlatAppearance.BorderSize = 0;
            close.Click += (_, __) => Close();
            header.Resize += (_, __) => close.Location = new Point(header.Width - close.Width - 14, 16);

            var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = TG.Divider };

            header.Controls.Add(back);
            header.Controls.Add(title);
            header.Controls.Add(close);
            header.Controls.Add(sep);
            close.Location = new Point(header.Width - close.Width - 14, 16);
            return header;
        }

        private Label CreateSectionLabel(string text)
        {
            return new Label
            {
                Text = text,
                ForeColor = TG.CAccent,
                Font = new Font("Segoe UI Semibold", 11f),
                BackColor = TG.WindowBg,
                Padding = new Padding(28, 10, 0, 0)
            };
        }

        private static Panel CreateSectionDivider()
        {
            return new Panel { BackColor = TG.Divider };
        }

        private Panel CreateActionRow(string text, string iconFile, Action onClick, out Label trailingLabel, string trailing = "")
        {
            var row = new Panel { BackColor = TG.WindowBg, Cursor = Cursors.Hand };

            var icon = new PictureBox
            {
                Size = new Size(22, 22),
                Location = new Point(28, 14),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = SettingsGlyphIcons.Create(iconFile, 22),
                BackColor = Color.Transparent
            };

            var lbl = new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = TG.TextPrimary,
                Location = new Point(70, 14),
                BackColor = Color.Transparent
            };

            trailingLabel = new Label
            {
                Text = trailing,
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = TG.CAccent,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                AutoSize = false
            };
            var right = trailingLabel;

            row.Resize += (_, __) =>
            {
                int rw = Math.Min(220, Math.Max(120, row.Width / 2));
                right.SetBounds(row.Width - rw - 18, 12, rw, 24);
            };

            var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = TG.Divider };

            foreach (Control c in new Control[] { row, icon, lbl, right })
            {
                c.Click += (_, __) => onClick();
                c.MouseEnter += (_, __) => row.BackColor = TG.SidebarHover;
                c.MouseLeave += (_, __) => row.BackColor = TG.WindowBg;
            }

            row.Controls.Add(icon);
            row.Controls.Add(lbl);
            row.Controls.Add(right);
            row.Controls.Add(sep);
            return row;
        }

        private Panel CreateAskDownloadToggleRow(out CheckBox toggle)
        {
            var row = new Panel { BackColor = TG.WindowBg };

            var lbl = new Label
            {
                Text = "Ask download path for each file",
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = TG.TextPrimary,
                Location = new Point(28, 16),
                BackColor = Color.Transparent
            };

            toggle = new CheckBox
            {
                Appearance = Appearance.Button,
                AutoSize = false,
                Size = new Size(44, 24),
                FlatStyle = FlatStyle.Flat,
                Checked = false,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            var t = toggle;
            t.FlatAppearance.BorderSize = 0;
            t.Paint += (_, e) => DrawToggle(t, e.Graphics);
            row.Resize += (_, __) => t.Location = new Point(row.Width - t.Width - 24, (row.Height - t.Height) / 2);

            var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = TG.Divider };

            row.Controls.Add(lbl);
            row.Controls.Add(t);
            row.Controls.Add(sep);
            return row;
        }

        private static void DrawToggle(CheckBox chk, Graphics g)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, chk.Width - 1, chk.Height - 1);
            int r = rect.Height / 2;
            var track = chk.Checked ? TG.CAccent : Color.FromArgb(0xC7, 0xD2, 0xDE);

            using var trackBrush = new SolidBrush(track);
            using var thumbBrush = new SolidBrush(TG.WindowBg);

            g.FillEllipse(trackBrush, rect.Left, rect.Top, rect.Height, rect.Height);
            g.FillEllipse(trackBrush, rect.Right - rect.Height, rect.Top, rect.Height, rect.Height);
            g.FillRectangle(trackBrush, rect.Left + r, rect.Top, rect.Width - rect.Height, rect.Height);

            int thumbX = chk.Checked ? rect.Right - rect.Height + 2 : rect.Left + 2;
            g.FillEllipse(thumbBrush, thumbX, rect.Top + 2, rect.Height - 4, rect.Height - 4);
        }

        private void OpenDownloads()
        {
            using var dlg = new frmDownloads();
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.ShowDialog(this);
        }

        private void OpenDownloadPathOptions()
        {
            var s = AdvancedSettings.Default;

            using var dlg = new Form
            {
                Text = "Choose download path",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                HelpButton = false,
                ShowIcon = false,
                ShowInTaskbar = false,
                ClientSize = new Size(520, 280),
                BackColor = TG.WindowBg,
                Font = new Font("Segoe UI", 10.5f)
            };

            var lblTitle = new Label
            {
                Text = "Choose download path",
                Font = new Font("Segoe UI Semibold", 17f),
                ForeColor = TG.TextPrimary,
                AutoSize = true,
                Location = new Point(28, 22),
                BackColor = Color.Transparent
            };

            var rbSystem = CreatePathOption("Default app folder", new Point(28, 76), s.DownloadPathMode == 0);
            var rbTemp = CreatePathOption("Temp folder, cleared on logout or uninstall", new Point(28, 122), s.DownloadPathMode == 1);
            var rbCustom = CreatePathOption("Custom folder, cleared only manually", new Point(28, 168), s.DownloadPathMode == 2);

            var btnCancel = new LinkLabel
            {
                Text = "Cancel",
                AutoSize = true,
                LinkBehavior = LinkBehavior.NeverUnderline,
                LinkColor = TG.CAccent,
                ActiveLinkColor = TG.CAccent,
                VisitedLinkColor = TG.CAccent,
                Font = new Font("Segoe UI Semibold", 10.8f),
                Location = new Point(352, 236),
                BackColor = Color.Transparent
            };
            btnCancel.Click += (_, __) => dlg.DialogResult = DialogResult.Cancel;

            var btnSave = new LinkLabel
            {
                Text = "Save",
                AutoSize = true,
                LinkBehavior = LinkBehavior.NeverUnderline,
                LinkColor = TG.CAccent,
                ActiveLinkColor = TG.CAccent,
                VisitedLinkColor = TG.CAccent,
                Font = new Font("Segoe UI Semibold", 10.8f),
                Location = new Point(448, 236),
                BackColor = Color.Transparent
            };
            btnSave.Click += (_, __) => dlg.DialogResult = DialogResult.OK;

            dlg.Controls.Add(lblTitle);
            dlg.Controls.Add(rbSystem);
            dlg.Controls.Add(rbTemp);
            dlg.Controls.Add(rbCustom);
            dlg.Controls.Add(btnCancel);
            dlg.Controls.Add(btnSave);

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            if (rbSystem.Checked)
            {
                s.DownloadPathMode = 0;
            }
            else if (rbTemp.Checked)
            {
                s.DownloadPathMode = 1;
            }
            else if (rbCustom.Checked)
            {
                using var fbd = new FolderBrowserDialog { Description = "Choose custom download folder" };
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    s.DownloadPathMode = 2;
                    s.CustomDownloadPath = fbd.SelectedPath;
                }
            }

            s.Save();
            LoadSettings();
        }

        private RadioButton CreatePathOption(string text, Point location, bool isChecked)
        {
            return new RadioButton
            {
                Text = text,
                AutoSize = true,
                Location = location,
                ForeColor = TG.TextPrimary,
                BackColor = TG.WindowBg,
                Font = new Font("Segoe UI", 11f),
                Checked = isChecked
            };
        }

        private void UpdateDownloadPathVisibility()
        {
            _rowDownloadPath.Visible = !_chkAskDownloadPath.Checked;
            LayoutRows();
        }

        private void LoadSettings()
        {
            var s = AdvancedSettings.Default;

            _chkAskDownloadPath.Checked = s.AskDownloadPathEachFile;
            _chkShowChatName.Checked = s.ShowChatName;
            _chkTotalUnreadCount.Checked = s.TotalUnreadCount;
            _chkUseSystemWindowFrame.Checked = s.UseSystemWindowFrame;
            _chkShowTaskbarIcon.Checked = s.ShowTaskbarIcon;
            _chkUseMonochromeIcon.Checked = s.UseMonochromeIcon;

            _lblDownloadPath.Text = GetDownloadPathDisplay(s);
            UpdateDownloadPathVisibility();
        }

        private static string GetDownloadPathDisplay(AdvancedSettings s)
        {
            return s.DownloadPathMode switch
            {
                1 => "Temp folder",
                2 => "Custom folder",
                _ => "Default folder"
            };
        }

        private void SaveSettings()
        {
            var s = AdvancedSettings.Default;
            s.AskDownloadPathEachFile = _chkAskDownloadPath.Checked;
            s.ShowChatName = _chkShowChatName.Checked;
            s.TotalUnreadCount = _chkTotalUnreadCount.Checked;
            s.UseSystemWindowFrame = _chkUseSystemWindowFrame.Checked;
            s.ShowTaskbarIcon = _chkShowTaskbarIcon.Checked;
            s.UseMonochromeIcon = _chkUseMonochromeIcon.Checked;
            s.Save();
        }

        private sealed class ToggleCheckRow : Panel
        {
            private readonly Panel _box;
            private readonly Color _accent;
            private bool _checked;

            public event EventHandler? CheckedChanged;

            public bool Checked
            {
                get => _checked;
                set
                {
                    if (_checked == value) return;
                    _checked = value;
                    _box.Invalidate();
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            public ToggleCheckRow(string text, bool initial, Color bg, Color textColor, Color accent, Color divider)
            {
                _accent = accent;
                _checked = initial;

                BackColor = bg;
                Cursor = Cursors.Hand;

                _box = new Panel
                {
                    Size = new Size(26, 26),
                    Location = new Point(26, 13),
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                _box.Paint += Box_Paint;

                var label = new Label
                {
                    Text = text,
                    AutoSize = true,
                    Font = new Font("Segoe UI", 10.5f),
                    ForeColor = textColor,
                    Location = new Point(70, 14),
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };

                var sep = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 1,
                    BackColor = divider
                };

                Controls.Add(_box);
                Controls.Add(label);
                Controls.Add(sep);

                Click += (_, __) => Checked = !Checked;
                _box.Click += (_, __) => Checked = !Checked;
                label.Click += (_, __) => Checked = !Checked;
            }

            private void Box_Paint(object? sender, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, _box.Width - 1, _box.Height - 1);

                var fill = Checked ? _accent : Color.White;
                var border = Checked ? _accent : Color.FromArgb(0xBF, 0xC8, 0xD3);

                using (var b = new SolidBrush(fill)) e.Graphics.FillRectangle(b, rect);
                using (var p = new Pen(border, 1.2f)) e.Graphics.DrawRectangle(p, rect);

                if (!Checked) return;
                using var checkPen = new Pen(Color.White, 2f);
                e.Graphics.DrawLines(checkPen, new[]
                {
                    new Point(6, 14),
                    new Point(11, 18),
                    new Point(20, 8)
                });
            }
        }

        // AdvancedSettings is now in SecureChat.Client.Settings namespace
        private void OnThemeChanged()
        {
            if (InvokeRequired) { Invoke(new Action(OnThemeChanged)); return; }
            BackColor = TG.WindowBg;
            Invalidate(true);
            ApplyThemeToControls(Controls);
        }

        private static void ApplyThemeToControls(System.Windows.Forms.Control.ControlCollection controls)
        {
            foreach (Control c in controls)
            {
                if (c.BackColor != Color.Transparent &&
                    c.BackColor != TG.Blue &&
                    c.BackColor != TG.SidebarActive &&
                    c.BackColor != TG.TitleBarBg &&
                    c.Tag as string != "accent")
                    c.BackColor = TG.WindowBg;
                if (c.ForeColor != Color.White && c.Tag as string != "white-fg")
                    c.ForeColor = TG.TextPrimary;
                c.Invalidate();
                ApplyThemeToControls(c.Controls);
            }
        }

    }
}
