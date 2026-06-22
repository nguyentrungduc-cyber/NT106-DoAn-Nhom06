using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using SecureChat.Client.Services;

namespace SecureChat.Client.Forms.Settings
{
    public class frmDownloads : Form
    {
        // Colors read from TG at paint time

        private Panel _content = null!;

        public frmDownloads()
        {
            ThemeRefreshHelper.Hook(this);
            InitializeComponent();
            BuildUI();
            Load += (_, __) => LoadFiles();
        }

        private void InitializeComponent() { }

        private void BuildUI()
        {
            Text = "Downloads";
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

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 74,
                BackColor = TG.WindowBg
            };

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
                Text = "Downloads",
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

            var sep = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = TG.Divider
            };

            header.Controls.Add(back);
            header.Controls.Add(title);
            header.Controls.Add(close);
            header.Controls.Add(sep);

            _content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = TG.WindowBg
            };

            Controls.Add(_content);
            Controls.Add(header);

            UiLocalization.ApplyToForm(this);
        }

        private void LoadFiles()
        {
            _content.Controls.Clear();

            var entries = DownloadHistoryStore.GetExistingDownloads();
            if (entries.Count == 0)
            {
                ShowEmptyState();
                return;
            }

            var list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BorderStyle = BorderStyle.None,
                BackColor = TG.WindowBg,
                ForeColor = TG.TextPrimary,
                Font = new Font("Segoe UI", 10.2f)
            };

            list.Columns.Add("Name", 240);
            list.Columns.Add("Size", 70, HorizontalAlignment.Right);
            list.Columns.Add("Source", 90);
            list.Columns.Add("Downloaded", 105);

            foreach (var entry in entries)
            {
                var fi = new FileInfo(entry.FilePath);
                var item = new ListViewItem(fi.Name);
                item.SubItems.Add(FormatSize(fi.Length));
                item.SubItems.Add(entry.Source);
                item.SubItems.Add(entry.DownloadedAtUtc.ToLocalTime().ToString("dd/MM HH:mm"));
                item.Tag = fi.FullName;
                list.Items.Add(item);
            }

            list.DoubleClick += (_, __) =>
            {
                if (list.SelectedItems.Count == 0) return;
                var fullPath = list.SelectedItems[0].Tag as string;
                if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath)) return;

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = fullPath,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    MessageBox.Show(this, "Cannot open file.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            _content.Controls.Add(list);
        }

        private void ShowEmptyState()
        {
            var icon = new PictureBox
            {
                Size = new Size(74, 74),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = SettingsGlyphIcons.Create("downloads_arrow.png", 74),
                BackColor = Color.Transparent
            };

            var txt = new Label
            {
                Text = "No files here yet",
                AutoSize = true,
                ForeColor = TG.TextSecondary,
                Font = new Font("Segoe UI", 10.8f),
                BackColor = Color.Transparent
            };

            _content.Controls.Add(icon);
            _content.Controls.Add(txt);
            _content.Resize += (_, __) =>
            {
                icon.Location = new Point((_content.Width - icon.Width) / 2, (_content.Height - icon.Height) / 2 - 24);
                txt.Location = new Point((_content.Width - txt.Width) / 2, icon.Bottom + 14);
            };

            icon.Location = new Point((_content.Width - icon.Width) / 2, (_content.Height - icon.Height) / 2 - 24);
            txt.Location = new Point((_content.Width - txt.Width) / 2, icon.Bottom + 14);
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024d).ToString("0.#") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / 1024d / 1024).ToString("0.#") + " MB";
            return (bytes / 1024d / 1024 / 1024).ToString("0.#") + " GB";
        }
    }

    internal static class DownloadHistoryStore
    {
        private static readonly string StoreDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecureChat");
        private static readonly string StorePath = Path.Combine(StoreDirectory, "download-history.json");

        public static void RegisterDownloadedFile(string filePath, string source = "chat")
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            try
            {
                Directory.CreateDirectory(StoreDirectory);

                var list = ReadAllInternal();
                var fullPath = Path.GetFullPath(filePath);

                list.RemoveAll(x => string.Equals(x.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
                list.Insert(0, new DownloadHistoryItem
                {
                    FilePath = fullPath,
                    Source = string.IsNullOrWhiteSpace(source) ? "chat" : source,
                    DownloadedAtUtc = DateTime.UtcNow
                });

                WriteAllInternal(list);
            }
            catch
            {
                // ignore
            }
        }

        public static List<DownloadHistoryItem> GetExistingDownloads()
        {
            try
            {
                var list = ReadAllInternal()
                    .Where(x => !string.IsNullOrWhiteSpace(x.FilePath) && File.Exists(x.FilePath))
                    .OrderByDescending(x => x.DownloadedAtUtc)
                    .ToList();

                // keep history clean
                WriteAllInternal(list);
                return list;
            }
            catch
            {
                return new List<DownloadHistoryItem>();
            }
        }

        private static List<DownloadHistoryItem> ReadAllInternal()
        {
            if (!File.Exists(StorePath)) return new List<DownloadHistoryItem>();

            var json = File.ReadAllText(StorePath);
            var list = JsonSerializer.Deserialize<List<DownloadHistoryItem>>(json);
            return list ?? new List<DownloadHistoryItem>();
        }

        private static void WriteAllInternal(List<DownloadHistoryItem> list)
        {
            Directory.CreateDirectory(StoreDirectory);
            var json = JsonSerializer.Serialize(list);
            File.WriteAllText(StorePath, json);
        }
    }

    internal sealed class DownloadHistoryItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string Source { get; set; } = "chat";
        public DateTime DownloadedAtUtc { get; set; }
    }
}
