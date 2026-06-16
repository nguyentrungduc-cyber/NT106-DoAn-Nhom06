using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SecureChat.Client.Forms.Settings
{
    public class frmChatSettings : Form
    {
        // Colors
        private static readonly Color C_BG = Color.White;
        private static readonly Color C_TEXT = Color.FromArgb(0x1F, 0x2D, 0x3D);
        private static readonly Color C_SUB = Color.FromArgb(0x7A, 0x8A, 0x99);
        private static readonly Color C_ACCENT_DEFAULT = Color.FromArgb(0x33, 0x99, 0xFF);
        private static readonly Color C_HOVER = Color.FromArgb(0xF2, 0xF5, 0xF9);

        // UI
        private RadioButton _rbClassic = null!;
        private RadioButton _rbDay = null!;
        private RadioButton _rbTinted = null!;
        private RadioButton _rbNight = null!;
        private FlowLayoutPanel _palette = null!;
        private Label _lblAutoNight = null!;
        private Label _lblFont = null!;

        // State
        private Color _accent = C_ACCENT_DEFAULT;
        private string _theme = "Tinted";
        private string _autoNight = "System";
        private Font _currentFont = new Font("Segoe UI", 10f);

        public frmChatSettings()
        {
            InitializeComponent();
            BuildUI();
            Load += (_, __) => LoadSettingsAndApply();
        }

        private void InitializeComponent() { }

        // UI Layout
        private void BuildUI()
        {
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            HelpButton = false;
            ControlBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 780);
            BackColor = C_BG;
            Font = _currentFont;
            DoubleBuffered = true;

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = C_BG,
                Padding = new Padding(12, 8, 12, 12)
            };
            Controls.Add(scroll);

            var table = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = C_BG,
            };
            table.RowStyles.Clear();
            scroll.Controls.Add(table);

            table.Controls.Add(BuildHeader());
            table.Controls.Add(SectionLabel("Themes"));
            table.Controls.Add(BuildThemes());
            table.Controls.Add(BuildPalette());
            table.Controls.Add(Divider());
            table.Controls.Add(SectionLabel("Theme settings"));
            table.Controls.Add(BuildSettingsRows());
        }

        private Control BuildHeader()
        {
            var header = new TableLayoutPanel
            {
                ColumnCount = 3,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 8)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var back = new PictureBox
            {
                Size = new Size(24, 24),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = LoadIcon("title_back"),
                Cursor = Cursors.Hand,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 8, 0),
                BackColor = Color.Transparent
            };
            back.Click += (_, __) => Close();

            var title = new Label
            {
                Text = "Chat Settings",
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
                Padding = new Padding(6, 2, 6, 2),
                TabStop = false
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (_, __) => Close();

            header.Controls.Add(back, 0, 0);
            header.Controls.Add(title, 1, 0);
            header.Controls.Add(btnClose, 2, 0);
            return header;
        }

        private Control SectionLabel(string text)
        {
            return new Label
            {
                Text = text,
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI Semibold", 11f),
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 6, 0, 6)
            };
        }

        private Control BuildThemes()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 0, 0, 4)
            };

            _rbClassic = CreateThemeCard(panel, "Classic", Color.FromArgb(152, 199, 122), Color.FromArgb(74, 187, 184), Color.FromArgb(109, 202, 109));
            _rbDay = CreateThemeCard(panel, "Day", Color.FromArgb(125, 182, 232), Color.FromArgb(255, 255, 255), Color.FromArgb(95, 174, 232));
            _rbTinted = CreateThemeCard(panel, "Tinted", Color.FromArgb(109, 137, 151), Color.FromArgb(84, 108, 125), Color.FromArgb(76, 157, 190));
            _rbNight = CreateThemeCard(panel, "Night", Color.FromArgb(92, 107, 119), Color.FromArgb(68, 82, 92), Color.FromArgb(86, 130, 134));

            return panel;
        }

        private RadioButton CreateThemeCard(FlowLayoutPanel host, string title, Color baseColor, Color block1, Color accent)
        {
            var cardPanel = new Panel
            {
                Size = new Size(108, 132),
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 12, 0),
                Tag = title
            };

            var rect = new Panel
            {
                Size = new Size(108, 100),
                BackColor = baseColor,
                Padding = new Padding(8, 8, 8, 8),
                Margin = new Padding(0, 0, 0, 4)
            };
            rect.Paint += (s, e) =>
            {
                using var gp = new System.Drawing.Drawing2D.GraphicsPath();
                gp.AddRoundedRect(new Rectangle(0, 0, rect.Width - 1, rect.Height - 1), 8);
                rect.Region = new Region(gp);
            };
            var blockTop = new Panel { Height = 18, Dock = DockStyle.Top, BackColor = block1, Margin = new Padding(0, 0, 0, 6) };
            var blockMid = new Panel { Height = 14, Dock = DockStyle.Top, BackColor = block1, Margin = new Padding(0, 0, 0, 6) };
            var blockAccent = new Panel { Height = 10, Dock = DockStyle.Bottom, BackColor = accent, Margin = new Padding(0, 6, 0, 0) };
            rect.Controls.Add(blockAccent);
            rect.Controls.Add(blockMid);
            rect.Controls.Add(blockTop);

            var rb = new RadioButton
            {
                Text = title,
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 10f),
                AutoSize = true,
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleCenter,
                Appearance = Appearance.Button,
                FlatStyle = FlatStyle.Flat,
                Height = 26,
                BackColor = Color.Transparent,
                Padding = new Padding(4, 2, 4, 2),
                Margin = new Padding(0, 2, 0, 0),
                Tag = title
            };
            rb.FlatAppearance.BorderSize = 0;
            rb.FlatAppearance.CheckedBackColor = Color.Transparent;
            rb.FlatAppearance.MouseOverBackColor = C_HOVER;

            void SelectTheme(object? sender, EventArgs e)
            {
                _theme = title;
                SaveAndApplyTheme();
            }
            rb.CheckedChanged += (s, e) => { if (rb.Checked) SelectTheme(s, e); };
            rect.Click += (s, e) => { rb.Checked = true; SelectTheme(s, e); };
            cardPanel.Click += (s, e) => { rb.Checked = true; SelectTheme(s, e); };

            cardPanel.Controls.Add(rect);
            cardPanel.Controls.Add(rb);
            host.Controls.Add(cardPanel);
            return rb;
        }

        private Control BuildPalette()
        {
            var colors = new[]
            {
                Color.FromArgb(74, 170, 233),
                Color.FromArgb(104, 177, 69),
                Color.FromArgb(131, 75, 128),
                Color.FromArgb(193, 146, 64),
                Color.FromArgb(139, 100, 143),
                Color.FromArgb(111, 126, 148),
                Color.FromArgb(218, 181, 61),
                Color.FromArgb(229, 92, 92),
                Color.FromArgb(102, 102, 102),
                Color.FromArgb(95, 174, 232)
            };

            _palette = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 6, 0, 6),
                WrapContents = false
            };

            foreach (var c in colors)
            {
                var btn = new Button
                {
                    Width = 30,
                    Height = 30,
                    BackColor = c,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(6, 0, 0, 0),
                    TabStop = false,
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.Paint += (s, e) =>
                {
                    using var gp = new System.Drawing.Drawing2D.GraphicsPath();
                    gp.AddEllipse(0, 0, btn.Width - 1, btn.Height - 1);
                    btn.Region = new Region(gp);
                };
                btn.Click += (s, e) => SelectPalette(btn);
                _palette.Controls.Add(btn);
            }

            return _palette;
        }

        private void SelectPalette(Button btn)
        {
            foreach (Control c in _palette.Controls)
            {
                if (c is Button b)
                {
                    b.FlatAppearance.BorderSize = 0;
                }
            }
            btn.FlatAppearance.BorderSize = 2;
            btn.FlatAppearance.BorderColor = C_ACCENT_DEFAULT;
            _accent = btn.BackColor;
            SaveAndApplyTheme();
        }

        private Control BuildSettingsRows()
        {
            var table = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent
            };

            _lblAutoNight = CreateSettingRow(table, "Auto-night mode", "moon", "System", OnAutoNightClick);
            _lblFont = CreateSettingRow(table, "Font family", "font", "Default", OnFontClick);
            return table;
        }

        private Label CreateSettingRow(TableLayoutPanel host, string text, string iconKey, string value, EventHandler onClick)
        {
            var row = new TableLayoutPanel
            {
                ColumnCount = 3,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 6, 0, 6),
                Margin = new Padding(0, 0, 0, 2)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var icon = new PictureBox
            {
                Size = new Size(24, 24),
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = LoadIcon(iconKey),
                BackColor = Color.Transparent,
                Margin = new Padding(6, 0, 6, 0)
            };

            var lblText = new Label
            {
                Text = text,
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 10.5f),
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblValue = new Label
            {
                Text = value,
                ForeColor = C_ACCENT_DEFAULT,
                Font = new Font("Segoe UI", 10.5f),
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight
            };

            foreach (Control c in new Control[] { row, icon, lblText, lblValue })
            {
                c.Cursor = Cursors.Hand;
                c.Click += onClick;
                c.MouseEnter += (_, __) => row.BackColor = C_HOVER;
                c.MouseLeave += (_, __) => row.BackColor = Color.Transparent;
            }

            row.Controls.Add(icon, 0, 0);
            row.Controls.Add(lblText, 1, 0);
            row.Controls.Add(lblValue, 2, 0);
            host.Controls.Add(row);
            return lblValue;
        }

        private Control Divider()
        {
            return new Panel
            {
                Height = 1,
                Dock = DockStyle.Top,
                BackColor = TG.Divider,
                Margin = new Padding(0, 8, 0, 8)
            };
        }

        // Event Handlers
        private void OnAutoNightClick(object? sender, EventArgs e)
        {
            var menu = new ContextMenuStrip { BackColor = C_BG, ForeColor = C_TEXT, ShowImageMargin = false }; // simple context
            menu.Items.Add("System", null, (_, __) => SetAutoNight("System"));
            menu.Items.Add("Off", null, (_, __) => SetAutoNight("Off"));
            menu.Show(Cursor.Position);
        }

        private void OnFontClick(object? sender, EventArgs e)
        {
            using (FontDialog fd = new FontDialog())
            {
                fd.Font = _currentFont;
                fd.ShowColor = false;
                if (fd.ShowDialog(this) == DialogResult.OK)
                {
                    _currentFont = fd.Font;
                    _lblFont.Text = _currentFont.Name;
                    ApplyFontToAll(this, _currentFont);
                    SaveSettings();
                }
            }
        }

        // Logic Methods
        private void LoadSettingsAndApply()
        {
            var s = AppSettings.Default;
            _theme = s.Theme;
            _accent = Color.FromArgb(s.AccentArgb);
            _autoNight = s.AutoNight;
            _currentFont = new Font(s.FontName, s.FontSize <= 0 ? 10f : s.FontSize);

            ApplyFontToAll(this, _currentFont);
            SelectThemeRadio(_theme);
            SelectPaletteColor(_accent);
            _lblAutoNight.Text = _autoNight;
            _lblFont.Text = _currentFont.Name;
            ApplyAutoNightIfSystem();
        }

        private void SelectThemeRadio(string theme)
        {
            switch (theme)
            {
                case "Classic": _rbClassic.Checked = true; break;
                case "Day": _rbDay.Checked = true; break;
                case "Night": _rbNight.Checked = true; break;
                default: _rbTinted.Checked = true; break;
            }
        }

        private void SelectPaletteColor(Color color)
        {
            foreach (Control c in _palette.Controls)
            {
                if (c is Button b && b.BackColor.ToArgb() == color.ToArgb())
                {
                    SelectPalette(b);
                    return;
                }
            }
            // if not found, just set accent
            _accent = color;
        }

        private void SaveAndApplyTheme()
        {
            SaveSettings();
            // Here you could propagate theme/accent to the app; for now update labels/colors
            _lblAutoNight.ForeColor = C_ACCENT_DEFAULT;
        }

        private void SetAutoNight(string mode)
        {
            _autoNight = mode;
            _lblAutoNight.Text = mode;
            ApplyAutoNightIfSystem();
            SaveSettings();
        }

        private void ApplyAutoNightIfSystem()
        {
            if (!string.Equals(_autoNight, "System", StringComparison.OrdinalIgnoreCase)) return;
            bool isLight = true;
            try
            {
                using var personalize = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
                if (personalize != null)
                {
                    var val = personalize.GetValue("AppsUseLightTheme");
                    if (val is int i)
                    {
                        isLight = i != 0;
                    }
                }
            }
            catch { }

            if (isLight)
            {
                _theme = "Day";
                _rbDay.Checked = true;
            }
            else
            {
                _theme = "Night";
                _rbNight.Checked = true;
            }
            SaveSettings();
        }

        private void ApplyFontToAll(Control parent, Font font)
        {
            foreach (Control c in parent.Controls)
            {
                c.Font = font;
                if (c.HasChildren)
                    ApplyFontToAll(c, font);
            }
        }

        private void SaveSettings()
        {
            var s = AppSettings.Default;
            s.Theme = _theme;
            s.AccentArgb = _accent.ToArgb();
            s.AutoNight = _autoNight;
            s.FontName = _currentFont.Name;
            s.FontSize = _currentFont.Size;
            s.Save();
        }

        // Data persistence (local file to mimic Properties.Settings)
        private sealed class AppSettings
        {
            private const string FileName = "chatsettings.config";
            public static AppSettings Default { get; } = Load();

            public string Theme { get; set; } = "Tinted";
            public int AccentArgb { get; set; } = C_ACCENT_DEFAULT.ToArgb();
            public string AutoNight { get; set; } = "System";
            public string FontName { get; set; } = "Segoe UI";
            public float FontSize { get; set; } = 10f;

            public void Save()
            {
                try
                {
                    var path = Path.Combine(AppContext.BaseDirectory, FileName);
                    var data = string.Join("\u001F", Theme, AccentArgb, AutoNight, FontName, FontSize);
                    File.WriteAllText(path, data, Encoding.UTF8);
                }
                catch { }
            }

            private static AppSettings Load()
            {
                var s = new AppSettings();
                try
                {
                    var path = Path.Combine(AppContext.BaseDirectory, FileName);
                    if (!File.Exists(path)) return s;
                    var text = File.ReadAllText(path, Encoding.UTF8);
                    var parts = text.Contains('\u001F') ? text.Split('\u001F') : text.Split('|');
                    if (parts.Length >= 5)
                    {
                        s.Theme = parts[0];
                        if (int.TryParse(parts[1], out var argb)) s.AccentArgb = argb;
                        s.AutoNight = parts[2];
                        s.FontName = parts[3];
                        if (float.TryParse(parts[4], out var fs)) s.FontSize = fs;
                    }
                }
                catch { }
                return s;
            }
        }

        // Icon loader
        private static Image LoadIcon(string key)
        {
            var file = key.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? key : key + ".png";
            return SettingsGlyphIcons.Create(file, 24);
        }
    }

    internal static class GraphicsPathExtensions
    {
        public static void AddRoundedRect(this System.Drawing.Drawing2D.GraphicsPath path, Rectangle rect, int radius)
        {
            var d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
        }
    }
}
