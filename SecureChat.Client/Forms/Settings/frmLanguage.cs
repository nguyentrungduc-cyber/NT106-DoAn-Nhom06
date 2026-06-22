using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using SecureChat.Client.Services;

namespace SecureChat.Client.Forms.Settings
{
    public class frmLanguage : Form
    {
        // Colors read from TG at paint time

        private readonly List<LanguageOption> _allLanguages = LanguageOption.Defaults();
        private List<LanguageOption> _filteredLanguages = new();

        private Panel _header = null!;
        private Panel _rowShowTranslate = null!;
        private CheckBox _chkShowTranslate = null!;
        private Panel _rowDoNotTranslate = null!;
        private Label _lblDoNotTranslateValue = null!;
        private Panel _infoPanel = null!;
        private Panel _rowSearch = null!;
        private TextBox _txtSearch = null!;
        private ListBox _lstLanguages = null!;
        private Panel _bottom = null!;

        public frmLanguage()
        {
            InitializeComponent();
            ThemeRefreshHelper.Hook(this);
            BuildUI();
            Load += (_, __) => LoadFromSettings();
            Resize += (_, __) => Relayout();
        }

        private void InitializeComponent() { }

        private void BuildUI()
        {
            Text = "Language";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            HelpButton = false;
            ControlBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 760);
            BackColor = TG.WindowBg;
            Font = new Font("Segoe UI", 10f);
            DoubleBuffered = true;

            _header = BuildHeader();
            Controls.Add(_header);

            _rowShowTranslate = BuildToggleRow("Show Translate Button", out _chkShowTranslate);
            Controls.Add(_rowShowTranslate);

            _rowDoNotTranslate = BuildValueRow("Do Not Translate", out _lblDoNotTranslateValue, OpenDoNotTranslateDialog);
            Controls.Add(_rowDoNotTranslate);

            _infoPanel = BuildInfoPanel();
            Controls.Add(_infoPanel);

            _rowSearch = BuildSearchRow(out _txtSearch);
            Controls.Add(_rowSearch);

            _lstLanguages = new ListBox
            {
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 62,
                BorderStyle = BorderStyle.None,
                BackColor = TG.WindowBg,
                ForeColor = TG.TextPrimary,
                IntegralHeight = false
            };
            _lstLanguages.DrawItem += DrawLanguageItem;
            _lstLanguages.Click += (_, __) =>
            {
                if (_lstLanguages.SelectedItem is not LanguageOption opt) return;
                LanguagePrefs.SetCurrentLanguage(opt.Code, opt.NativeName, opt.EnglishName);
                _lstLanguages.Invalidate();
                UiLocalization.ApplyToOpenForms();
            };
            Controls.Add(_lstLanguages);

            _bottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                BackColor = TG.WindowBg
            };
            var btnOk = new LinkLabel
            {
                Text = "OK",
                AutoSize = true,
                LinkBehavior = LinkBehavior.NeverUnderline,
                LinkColor = TG.CAccent,
                ActiveLinkColor = TG.CAccent,
                VisitedLinkColor = TG.CAccent,
                Font = new Font("Segoe UI Semibold", 11f)
            };
            _bottom.Resize += (_, __) => btnOk.Location = new Point(_bottom.Width - btnOk.Width - 20, 14);
            btnOk.Click += (_, __) =>
            {
                SaveTranslateSettings();
                DialogResult = DialogResult.OK;
                Close();
            };
            _bottom.Controls.Add(btnOk);
            Controls.Add(_bottom);

            _txtSearch.TextChanged += (_, __) => ApplyLanguageFilter();
            _chkShowTranslate.CheckedChanged += (_, __) =>
            {
                UpdateTranslateVisibility();
                SaveTranslateSettings();
            };

            Relayout();
            UiLocalization.ApplyToForm(this);
        }

        private Panel BuildHeader()
        {
            var panel = new Panel
            {
                Height = 74,
                BackColor = TG.WindowBg
            };

            var title = new Label
            {
                Text = "Language",
                Font = new Font("Segoe UI Semibold", 13f),
                ForeColor = TG.TextPrimary,
                AutoSize = true,
                Location = new Point(28, 20)
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
            panel.Resize += (_, __) => close.Location = new Point(panel.Width - close.Width - 14, 16);

            panel.Controls.Add(title);
            panel.Controls.Add(close);
            panel.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = TG.Divider });
            return panel;
        }

        private Panel BuildToggleRow(string text, out CheckBox toggle)
        {
            var row = new Panel { Height = 54, BackColor = TG.WindowBg };
            var lbl = new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = TG.TextPrimary,
                Location = new Point(28, 16)
            };

            toggle = new CheckBox
            {
                Appearance = Appearance.Button,
                AutoSize = false,
                Size = new Size(44, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            var t = toggle;
            t.FlatAppearance.BorderSize = 0;
            t.Paint += (_, e) => DrawToggle(t, e.Graphics, true);
            row.Resize += (_, __) => t.Location = new Point(row.Width - 68, 15);

            row.Click += (_, __) => t.Checked = !t.Checked;
            lbl.Click += (_, __) => t.Checked = !t.Checked;

            row.Controls.Add(lbl);
            row.Controls.Add(t);
            row.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = TG.Divider });
            return row;
        }

        private Panel BuildValueRow(string text, out Label valueLabel, Action onClick)
        {
            var row = new Panel { Height = 54, BackColor = TG.WindowBg, Cursor = Cursors.Hand };
            var lbl = new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = TG.TextPrimary,
                Location = new Point(28, 16)
            };

            valueLabel = new Label
            {
                AutoSize = false,
                Width = 180,
                Height = 24,
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = TG.CAccent,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true
            };
            var value = valueLabel;
            row.Resize += (_, __) => value.Location = new Point(row.Width - value.Width - 20, 14);

            foreach (Control c in new Control[] { row, lbl, value })
            {
                c.Click += (_, __) => onClick();
                c.MouseEnter += (_, __) => row.BackColor = TG.SidebarHover;
                c.MouseLeave += (_, __) => row.BackColor = TG.WindowBg;
            }

            row.Controls.Add(lbl);
            row.Controls.Add(value);
            row.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = TG.Divider });
            return row;
        }

        private Panel BuildInfoPanel()
        {
            var panel = new Panel
            {
                Height = 72,
                BackColor = TG.WindowBg
            };
            var lblInfo = new Label
            {
                Text = "The 'Translate' button will appear in the context menu of messages containing text.",
                ForeColor = TG.TextSecondary,
                Font = new Font("Segoe UI", 9.5f),
                AutoSize = false,
                Location = new Point(28, 12),
                Size = new Size(ClientSize.Width - 56, 46)
            };
            panel.Resize += (_, __) => lblInfo.Width = panel.Width - 56;
            panel.Controls.Add(lblInfo);
            panel.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = TG.Divider });
            return panel;
        }

        private Panel BuildSearchRow(out TextBox search)
        {
            var row = new Panel
            {
                Height = 54,
                BackColor = TG.WindowBg
            };

            var icon = new Label
            {
                Text = "\uE721",
                Font = new Font("Segoe MDL2 Assets", 14f),
                ForeColor = TG.TextSecondary,
                AutoSize = true,
                Location = new Point(28, 14)
            };

            search = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 12f),
                ForeColor = TG.TextPrimary,
                Location = new Point(62, 16),
                Width = ClientSize.Width - 90,
                BackColor = TG.WindowBg
            };
            SetCueBanner(search, "Search");
            var tb = search;

            row.Resize += (_, __) => tb.Width = Math.Max(120, row.Width - 90);

            row.Controls.Add(icon);
            row.Controls.Add(search);
            row.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = TG.Divider });
            return row;
        }

        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private static void SetCueBanner(TextBox textBox, string cueText)
        {
            if (textBox.IsHandleCreated)
            {
                SendMessage(textBox.Handle, EM_SETCUEBANNER, (IntPtr)1, cueText);
                return;
            }

            textBox.HandleCreated += (_, __) => SendMessage(textBox.Handle, EM_SETCUEBANNER, (IntPtr)1, cueText);
        }

        private void DrawLanguageItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _filteredLanguages.Count) return;

            var item = _filteredLanguages[e.Index];
            bool selected = string.Equals(item.Code, LanguagePrefs.CurrentLanguageCode, StringComparison.OrdinalIgnoreCase);

            using var bgBrush = new SolidBrush(TG.WindowBg);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            var circleRect = new Rectangle(e.Bounds.Left + 28, e.Bounds.Top + 18, 24, 24);
            using (var p = new Pen(selected ? TG.CAccent : TG.TextSecondary, 2f))
                e.Graphics.DrawEllipse(p, circleRect);
            if (selected)
            {
                using var b = new SolidBrush(TG.CAccent);
                e.Graphics.FillEllipse(b, circleRect.Left + 6, circleRect.Top + 6, 12, 12);
            }

            using var fNative = new Font("Segoe UI Semibold", 11f);
            using var fEng = new Font("Segoe UI", 10f);
            using var bText = new SolidBrush(TG.TextPrimary);
            using var bSub = new SolidBrush(TG.TextSecondary);

            e.Graphics.DrawString(item.NativeName, fNative, bText, e.Bounds.Left + 70, e.Bounds.Top + 10);
            e.Graphics.DrawString(item.EnglishName, fEng, bSub, e.Bounds.Left + 70, e.Bounds.Top + 33);

            using var sep = new Pen(TG.Divider, 1f);
            e.Graphics.DrawLine(sep, e.Bounds.Left + 24, e.Bounds.Bottom - 1, e.Bounds.Right - 24, e.Bounds.Bottom - 1);
        }

        private void LoadFromSettings()
        {
            _chkShowTranslate.Checked = LanguagePrefs.ShowTranslateButton;
            _lblDoNotTranslateValue.Text = LanguagePrefs.GetDoNotTranslateDisplay();
            _txtSearch.Text = string.Empty;
            ApplyLanguageFilter();
            UpdateTranslateVisibility();
        }

        private void SaveTranslateSettings()
        {
            LanguagePrefs.ShowTranslateButton = _chkShowTranslate.Checked;
            LanguagePrefs.Save();
        }

        private void ApplyLanguageFilter()
        {
            var q = _txtSearch.Text.Trim();
            _filteredLanguages = string.IsNullOrWhiteSpace(q)
                ? _allLanguages.ToList()
                : _allLanguages.Where(x =>
                    x.NativeName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.EnglishName.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

            _lstLanguages.DataSource = null;
            _lstLanguages.DataSource = _filteredLanguages;
            _lstLanguages.Invalidate();
        }

        private void Relayout()
        {
            int y = 0;
            _header.SetBounds(0, y, ClientSize.Width, 74);
            y += 74;

            _rowShowTranslate.SetBounds(0, y, ClientSize.Width, 54);
            y += 54;

            if (_rowDoNotTranslate.Visible)
            {
                _rowDoNotTranslate.SetBounds(0, y, ClientSize.Width, 54);
                y += 54;

                _infoPanel.SetBounds(0, y, ClientSize.Width, 72);
                y += 72;
            }

            _rowSearch.SetBounds(0, y, ClientSize.Width, 54);
            y += 54;

            _lstLanguages.SetBounds(0, y, ClientSize.Width, ClientSize.Height - y - _bottom.Height);
        }

        private void UpdateTranslateVisibility()
        {
            bool show = _chkShowTranslate.Checked;
            _rowDoNotTranslate.Visible = show;
            _infoPanel.Visible = show;
            Relayout();
        }

        private void OpenDoNotTranslateDialog()
        {
            if (!_chkShowTranslate.Checked) return;

            using var dlg = new frmDoNotTranslate(_allLanguages);
            dlg.StartPosition = FormStartPosition.CenterParent;
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _lblDoNotTranslateValue.Text = LanguagePrefs.GetDoNotTranslateDisplay();
            }
        }

        private static void DrawToggle(CheckBox chk, Graphics g, bool enabled)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, chk.Width - 1, chk.Height - 1);
            int r = rect.Height / 2;
            var active = enabled && chk.Checked;
            var track = active ? TG.CAccent : TG.TextSecondary;

            using var trackBrush = new SolidBrush(track);
            using var thumbBrush = new SolidBrush(Color.White);
            g.FillEllipse(trackBrush, rect.Left, rect.Top, rect.Height, rect.Height);
            g.FillEllipse(trackBrush, rect.Right - rect.Height, rect.Top, rect.Height, rect.Height);
            g.FillRectangle(trackBrush, rect.Left + r, rect.Top, rect.Width - rect.Height, rect.Height);

            int thumbX = active ? rect.Right - rect.Height + 2 : rect.Left + 2;
            g.FillEllipse(thumbBrush, thumbX, rect.Top + 2, rect.Height - 4, rect.Height - 4);
        }
    }

    internal class frmDoNotTranslate : Form
    {
        private readonly List<LanguageOption> _all;
        private CheckedListBox _list = null!;

        public frmDoNotTranslate(List<LanguageOption> all)
        {
            _all = all;
            BuildUI();
        }

        private void BuildUI()
        {
            Text = "Do Not Translate";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(460, 520);
            BackColor = TG.WindowBg;
            Font = new Font("Segoe UI", 10f);

            var title = new Label
            {
                Text = "Do Not Translate",
                Font = new Font("Segoe UI Semibold", 13f),
                AutoSize = true,
                Location = new Point(20, 16)
            };

            var search = new TextBox
            {
                Location = new Point(20, 52),
                Width = 420,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10.5f)
            };

            _list = new CheckedListBox
            {
                Location = new Point(20, 88),
                Size = new Size(420, 374),
                CheckOnClick = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10.5f)
            };

            var selected = new HashSet<string>(LanguagePrefs.DoNotTranslateCodes, StringComparer.OrdinalIgnoreCase);
            void Reload(string q)
            {
                _list.Items.Clear();
                foreach (var item in _all.Where(x =>
                    string.IsNullOrWhiteSpace(q) ||
                    x.NativeName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.EnglishName.Contains(q, StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = _list.Items.Add(item);
                    if (selected.Contains(item.Code)) _list.SetItemChecked(idx, true);
                }
            }

            Reload(string.Empty);

            search.TextChanged += (_, __) => Reload(search.Text.Trim());

            var btnOk = new LinkLabel
            {
                Text = "OK",
                AutoSize = true,
                LinkBehavior = LinkBehavior.NeverUnderline,
                LinkColor = TG.CAccent,
                ActiveLinkColor = TG.CAccent,
                VisitedLinkColor = TG.CAccent,
                Font = new Font("Segoe UI Semibold", 11f),
                Location = new Point(416, 480)
            };
            btnOk.Click += (_, __) =>
            {
                var codes = _list.CheckedItems
                    .OfType<LanguageOption>()
                    .Select(x => x.Code)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                LanguagePrefs.DoNotTranslateCodes = codes;
                LanguagePrefs.Save();
                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.Add(title);
            Controls.Add(search);
            Controls.Add(_list);
            Controls.Add(btnOk);
        }
    }

    internal sealed class LanguageOption
    {
        public string Code { get; set; } = string.Empty;
        public string NativeName { get; set; } = string.Empty;
        public string EnglishName { get; set; } = string.Empty;

        public override string ToString() => $"{NativeName} ({EnglishName})";

        public static List<LanguageOption> Defaults() => new()
        {
            new LanguageOption { Code = "en", NativeName = "English", EnglishName = "English" },
            new LanguageOption { Code = "be", NativeName = "\u0411\u0435\u043B\u0430\u0440\u0443\u0441\u043A\u0430\u044F", EnglishName = "Belarusian" },
            new LanguageOption { Code = "ca", NativeName = "Catal\u00E0", EnglishName = "Catalan" },
            new LanguageOption { Code = "hr", NativeName = "Hrvatski", EnglishName = "Croatian" },
            new LanguageOption { Code = "nl", NativeName = "Nederlands", EnglishName = "Dutch" },
            new LanguageOption { Code = "fr", NativeName = "Fran\u00E7ais", EnglishName = "French" },
            new LanguageOption { Code = "de", NativeName = "Deutsch", EnglishName = "German" },
            new LanguageOption { Code = "it", NativeName = "Italiano", EnglishName = "Italian" },
            new LanguageOption { Code = "es", NativeName = "Espa\u00F1ol", EnglishName = "Spanish" },
            new LanguageOption { Code = "vi", NativeName = "Ti\u1EBFng Vi\u1EC7t", EnglishName = "Vietnamese" },
            new LanguageOption { Code = "ja", NativeName = "\u65E5\u672C\u8A9E", EnglishName = "Japanese" },
            new LanguageOption { Code = "ko", NativeName = "\uD55C\uAD6D\uC5B4", EnglishName = "Korean" },
            new LanguageOption { Code = "zh", NativeName = "\u4E2D\u6587", EnglishName = "Chinese" }
        };
    }

    internal static class LanguagePrefs
    {
        private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "language.config");

        public static string CurrentLanguageCode { get; private set; } = "en";
        public static string CurrentNativeName { get; private set; } = "English";
        public static string CurrentEnglishName { get; private set; } = "English";
        public static bool ShowTranslateButton { get; set; } = true;
        public static List<string> DoNotTranslateCodes { get; set; } = new() { "en" };

        static LanguagePrefs() => Load();

        public static string GetDisplayLanguageName() => string.IsNullOrWhiteSpace(CurrentNativeName) ? "English" : CurrentNativeName;

        public static string GetDoNotTranslateDisplay()
        {
            if (DoNotTranslateCodes.Count == 0) return "None";
            if (DoNotTranslateCodes.Count == 1)
            {
                var one = LanguageOption.Defaults().FirstOrDefault(x => x.Code.Equals(DoNotTranslateCodes[0], StringComparison.OrdinalIgnoreCase));
                return one?.NativeName ?? "1 language";
            }
            return $"{DoNotTranslateCodes.Count} languages";
        }

        public static void SetCurrentLanguage(string code, string nativeName, string englishName)
        {
            CurrentLanguageCode = code;
            CurrentNativeName = nativeName;
            CurrentEnglishName = englishName;
            Save();
        }

        public static void Save()
        {
            try
            {
                var data = new LanguagePrefsData
                {
                    CurrentLanguageCode = CurrentLanguageCode,
                    CurrentNativeName = CurrentNativeName,
                    CurrentEnglishName = CurrentEnglishName,
                    ShowTranslateButton = ShowTranslateButton,
                    DoNotTranslateCodes = DoNotTranslateCodes
                };
                File.WriteAllText(FilePath, JsonSerializer.Serialize(data), Encoding.UTF8);
            }
            catch { }
        }

        private static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                var json = File.ReadAllText(FilePath, Encoding.UTF8);
                var data = JsonSerializer.Deserialize<LanguagePrefsData>(json);
                if (data == null) return;

                CurrentLanguageCode = string.IsNullOrWhiteSpace(data.CurrentLanguageCode) ? "en" : data.CurrentLanguageCode;
                CurrentNativeName = string.IsNullOrWhiteSpace(data.CurrentNativeName) ? "English" : data.CurrentNativeName;
                CurrentEnglishName = string.IsNullOrWhiteSpace(data.CurrentEnglishName) ? "English" : data.CurrentEnglishName;
                ShowTranslateButton = data.ShowTranslateButton;
                DoNotTranslateCodes = data.DoNotTranslateCodes ?? new List<string> { "en" };
            }
            catch { }
        }

        private sealed class LanguagePrefsData
        {
            public string CurrentLanguageCode { get; set; } = "en";
            public string CurrentNativeName { get; set; } = "English";
            public string CurrentEnglishName { get; set; } = "English";
            public bool ShowTranslateButton { get; set; } = true;
            public List<string> DoNotTranslateCodes { get; set; } = new();
        }
    }
}
