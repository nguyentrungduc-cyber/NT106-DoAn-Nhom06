using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using SecureChat.Client.Forms.Profile;
using SecureChat.Client.Models;
using SecureChat.Client.Services;

namespace SecureChat.Client.Forms.Settings
{
    public class frmSettings : Form
    {
        // Colors read from TG at paint time
        private static readonly int ITEM_HEIGHT = 54;
        private static readonly int HEADER_PADDING_X = 16;
        private static readonly int AVATAR_SIZE = 88;

        private readonly ProfileModel _profile;
        private Panel _root = null!;
        private Panel _headerPanel = null!;
        private Label _lblName = null!;
        private Label _lblEmail = null!;
        private Label _lblUsername = null!;
        private Panel _avatarPanel = null!;
        private Label? _lblLanguageMenu;
        private readonly List<Panel> _menuItems = new();

        public frmSettings(ProfileModel profile)
        {
            NightModeService.ThemeChanged += OnThemeChanged;
            FormClosed += (_, __) => NightModeService.ThemeChanged -= OnThemeChanged;
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            InitializeComponent();
            BuildUI();
        }

        private void InitializeComponent() { }

        private void BuildUI()
        {
            Text = "Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            HelpButton = false;
            ControlBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 740);
            BackColor = TG.WindowBg;
            SecureChat.Client.Services.ThemeRefreshHelper.Hook(this);
            Font = TG.FontRegular(10f);
            DoubleBuffered = true;
            Resize += (_, __) => LayoutHeaderProfileText();

            _root = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = TG.WindowBg,
                AutoScroll = true,
                Padding = new Padding(0, 0, 0, 12)
            };
            Controls.Add(_root);

            int y = 0;
            y = BuildHeader(y);
            y += 8;

            AddMenuItem(ref y, "My Account", "my_account.png", OpenProfile);
            AddMenuItem(ref y, "Notifications and Sounds", "notifications.png", OpenNotifications);
            AddMenuItem(ref y, "Privacy and Security", "privacy.png", OpenPrivacy);
            AddMenuItem(ref y, "Advanced", "advanced.png", OpenAdvanced);
            AddMenuItem(ref y, "Speakers and Camera", "devices.png", OpenSpeakersCamera);
            AddMenuItem(ref y, "Language", "language.png", OpenLanguage, true, LanguagePrefs.GetDisplayLanguageName(), lbl => _lblLanguageMenu = lbl);

            // Add separator before logout
            y += 8;
            var logoutSeparator = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(ClientSize.Width, 1),
                BackColor = TG.Divider,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            _root.Controls.Add(logoutSeparator);
            y += 1;

            // Add logout button with emoji
            AddMenuItem(ref y, "🔓 Logout", "", OnLogout);

            UiLocalization.ApplyToForm(this);
        }

        private int BuildHeader(int y)
        {
            _headerPanel = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(ClientSize.Width, 176),
                BackColor = TG.WindowBg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblTitle = new Label
            {
                Text = "Settings",
                Font = TG.FontSemiBold(12.5f),
                ForeColor = TG.TextPrimary,
                AutoSize = true,
                Location = new Point(16, 14),
                BackColor = Color.Transparent
            };

            var btnClose = FlatIconButton("✕");
            btnClose.Location = new Point(_headerPanel.Width - btnClose.Width - 14, 10);
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Click += (_, __) => Close();

            _avatarPanel = new Panel
            {
                Size = new Size(AVATAR_SIZE, AVATAR_SIZE),
                Location = new Point(HEADER_PADDING_X, 56),
                BackColor = TG.GetAvatarColor(_profile.FullName)
            };
            _avatarPanel.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = new GraphicsPath();
                path.AddEllipse(0, 0, _avatarPanel.Width, _avatarPanel.Height);
                _avatarPanel.Region = new Region(path);

                var avatarImage = LoadAvatarImage(_profile.AvatarPath);
                if (avatarImage != null)
                {
                    e.Graphics.SetClip(path);
                    e.Graphics.DrawImage(avatarImage, new Rectangle(0, 0, _avatarPanel.Width, _avatarPanel.Height));
                    e.Graphics.ResetClip();
                    avatarImage.Dispose();
                    return;
                }

                using var f = TG.FontSemiBold(28f);
                var initials = GetInitials(_profile.FullName);
                var sz = e.Graphics.MeasureString(initials, f);
                e.Graphics.DrawString(initials, f, Brushes.White,
                    (_avatarPanel.Width - sz.Width) / 2,
                    (_avatarPanel.Height - sz.Height) / 2);
            };

            _lblName = new Label
            {
                Font = TG.FontSemiBold(17f),
                ForeColor = TG.TextPrimary,
                AutoSize = false,
                AutoEllipsis = true,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _lblEmail = new Label
            {
                Font = TG.FontRegular(11.2f),
                ForeColor = TG.TextSecondary,
                AutoSize = false,
                AutoEllipsis = true,
                UseCompatibleTextRendering = true,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.TopLeft
            };

            _lblUsername = new Label
            {
                Font = TG.FontRegular(12f),
                AutoSize = false,
                AutoEllipsis = true,
                UseCompatibleTextRendering = true,
                ForeColor = TG.CAccent,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.TopLeft
            };

            var headerSep = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = TG.Divider
            };

            _headerPanel.Controls.AddRange(new Control[]
            {
                lblTitle, btnClose, _avatarPanel, _lblName, _lblEmail, _lblUsername, headerSep
            });

            _root.Controls.Add(_headerPanel);
            RefreshHeader();
            return _headerPanel.Bottom;
        }

        private void AddMenuItem(ref int y, string text, string iconFile, Action onClick, bool showExtraText = false, string extraText = "", Action<Label>? onTrailingCreated = null)
        {
            var panel = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(ClientSize.Width, ITEM_HEIGHT),
                BackColor = TG.WindowBg,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };

            panel.MouseEnter += (_, __) => panel.BackColor = TG.SidebarHover;
            panel.MouseLeave += (_, __) => panel.BackColor = TG.WindowBg;
            panel.Click += (_, __) => onClick();

            int labelX = 20; // Default position if no icon

            // Only add icon if iconFile is not empty
            if (!string.IsNullOrEmpty(iconFile))
            {
                var icon = new PictureBox
                {
                    Size = new Size(24, 24),
                    Location = new Point(20, (ITEM_HEIGHT - 24) / 2),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = SettingsGlyphIcons.Create(iconFile, 24),
                    BackColor = Color.Transparent
                };
                icon.MouseEnter += (_, __) => panel.BackColor = TG.SidebarHover;
                icon.MouseLeave += (_, __) => panel.BackColor = TG.WindowBg;
                icon.Click += (_, __) => onClick();
                panel.Controls.Add(icon);
                labelX = 68; // Position after icon
            }

            var lbl = new Label
            {
                Text = text,
                Font = text.Contains("🔓") ? TG.FontSemiBold(12.2f) : TG.FontRegular(12.2f),
                ForeColor = text.Contains("🔓") ? Color.FromArgb(0xE7, 0x2C, 0x3C) : TG.TextPrimary, // Red for logout
                AutoSize = true,
                Location = new Point(labelX, (ITEM_HEIGHT - 22) / 2),
                BackColor = Color.Transparent
            };
            lbl.MouseEnter += (_, __) => panel.BackColor = TG.SidebarHover;
            lbl.MouseLeave += (_, __) => panel.BackColor = TG.WindowBg;
            lbl.Click += (_, __) => onClick();

            panel.Controls.Add(lbl);

            if (showExtraText)
            {
                var trailing = new Label
                {
                    Text = extraText,
                    Font = TG.FontRegular(12f),
                    ForeColor = TG.CAccent,
                    AutoSize = true,
                    BackColor = Color.Transparent
                };
                trailing.MouseEnter += (_, __) => panel.BackColor = TG.SidebarHover;
                trailing.MouseLeave += (_, __) => panel.BackColor = TG.WindowBg;
                trailing.Click += (_, __) => onClick();
                panel.Controls.Add(trailing);
                onTrailingCreated?.Invoke(trailing);

                panel.Resize += (_, __) =>
                    trailing.Location = new Point(panel.Width - trailing.Width - 16, (ITEM_HEIGHT - trailing.Height) / 2);
                trailing.Location = new Point(panel.Width - trailing.Width - 16, (ITEM_HEIGHT - trailing.Height) / 2);
            }

            var sep = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = TG.Divider
            };
            panel.Controls.Add(sep);

            _root.Controls.Add(panel);
            _menuItems.Add(panel);
            y += ITEM_HEIGHT;
        }

        private void OpenProfile()
        {
            using var dlg = new frmProfileInfo(_profile);
            dlg.StartPosition = FormStartPosition.CenterParent;
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                RefreshHeader();
            }
        }

        private void OpenNotifications()
        {
            using var dlg = new frmNotificationsSounds();
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.ShowDialog(this);
        }

        private void OpenPrivacy()
        {
            using var dlg = new frmPrivacySecurity();
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.ShowDialog(this);
        }

        private void OpenAdvanced()
        {
            using var dlg = new frmAdvanced();
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.ShowDialog(this);
        }

        private void OpenSpeakersCamera()
        {
            using var dlg = new frmSpeakersCamera();
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.ShowDialog(this);
        }

        private void OpenLanguage()
        {
            using var dlg = new frmLanguage();
            dlg.StartPosition = FormStartPosition.CenterParent;
            if (dlg.ShowDialog(this) == DialogResult.OK && _lblLanguageMenu != null)
            {
                _lblLanguageMenu.Text = LanguagePrefs.GetDisplayLanguageName();
            }
        }

        private async void OnLogout()
        {
            var confirmResult = MessageBox.Show(
                this,
                "Are you sure you want to logout?",
                "Confirm Logout",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes)
            {
                return;
            }

            try
            {
                // Disable logout button to prevent double-click
                Enabled = false;

                // Call logout API
                await ApiClient.Instance.LogoutAsync();

                // Close this form and signal logout
                DialogResult = DialogResult.No; // Special value to signal logout
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"Logout error: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Enabled = true;
            }
        }

        private void ShowPending()
        {
            MessageBox.Show(this, "Feature coming soon", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static Button FlatIconButton(string text)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TG.TextSecondary,
                Font = TG.FontSemiBold(12f),
                TabStop = false,
                Cursor = Cursors.Hand,
                UseCompatibleTextRendering = true,
                Padding = new Padding(8, 3, 8, 3)
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = TG.SidebarHover;
            b.FlatAppearance.MouseDownBackColor = TG.SidebarHover;
            return b;
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                var first = GetFirstGrapheme(parts[0]);
                var second = parts[0].Length > first.Length ? GetFirstGrapheme(parts[0].Substring(first.Length)) : string.Empty;
                return (first + second).ToUpperInvariant();
            }

            var firstWord = GetFirstGrapheme(parts[0]);
            var lastWord = GetFirstGrapheme(parts[^1]);
            return (firstWord + lastWord).ToUpperInvariant();
        }

        private static string GetFirstGrapheme(string text)
        {
            var e = System.Globalization.StringInfo.GetTextElementEnumerator(text);
            return e.MoveNext() ? e.GetTextElement() : string.Empty;
        }

        private void RefreshHeader()
        {
            _lblName.Text = string.IsNullOrWhiteSpace(_profile.FullName) ? "Unknown User" : _profile.FullName;
            _lblEmail.Text = string.IsNullOrWhiteSpace(_profile.Email) ? "---" : _profile.Email;
            _avatarPanel.BackColor = TG.GetAvatarColor(_profile.FullName);

            _lblUsername.Text = string.IsNullOrWhiteSpace(_profile.Username) ? "Add username" : _profile.Username;
            _lblUsername.ForeColor = string.IsNullOrWhiteSpace(_profile.Username) ? TG.TextSecondary : TG.CAccent;

            LayoutHeaderProfileText();

            _avatarPanel.Invalidate();
        }

        private static Image? LoadAvatarImage(string avatarPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(avatarPath)) return null;
                if (File.Exists(avatarPath))
                {
                    using var fs = new FileStream(avatarPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var img = Image.FromStream(fs);
                    return new Bitmap(img);
                }
                if (avatarPath.StartsWith("/") || avatarPath.StartsWith("http"))
                {
                    var local = DownloadAndCacheAvatar(avatarPath);
                    if (local != null && File.Exists(local))
                    {
                        using var fs = new FileStream(local, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var img = Image.FromStream(fs);
                        return new Bitmap(img);
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string? DownloadAndCacheAvatar(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                var http = ApiClient.Instance.GetHttpClient();
                var cacheKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url)));
                var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecureChat", "AvatarCache");
                Directory.CreateDirectory(cacheDir);
                var cachePath = Path.Combine(cacheDir, cacheKey + ".png");
                if (File.Exists(cachePath))
                    return cachePath;

                using var response = http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result;
                if (!response.IsSuccessStatusCode) return null;
                using var stream = response.Content.ReadAsStreamAsync().Result;
                using var img = Image.FromStream(stream);
                img.Save(cachePath, System.Drawing.Imaging.ImageFormat.Png);
                return cachePath;
            }
            catch
            {
                return null;
            }
        }

        private void LayoutHeaderProfileText()
        {
            if (_headerPanel == null || _lblName == null || _lblEmail == null || _lblUsername == null)
                return;

            int textLeft = _avatarPanel.Right + 18;
            int textWidth = Math.Max(120, _headerPanel.Width - textLeft - HEADER_PADDING_X);

            int nameHeight;
            using (var g = _headerPanel.CreateGraphics())
            {
                nameHeight = TextRenderer.MeasureText(
                    g,
                    _lblName.Text,
                    _lblName.Font,
                    new Size(textWidth, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;
            }

            nameHeight = Math.Max(32, Math.Min(nameHeight, 56));
            int nameTop = _avatarPanel.Top + 4;

            int emailHeight;
            int usernameHeight;
            using (var g = _headerPanel.CreateGraphics())
            {
                emailHeight = TextRenderer.MeasureText(g, _lblEmail.Text, _lblEmail.Font, new Size(textWidth, int.MaxValue), TextFormatFlags.NoPadding).Height;
                usernameHeight = TextRenderer.MeasureText(g, _lblUsername.Text, _lblUsername.Font, new Size(textWidth, int.MaxValue), TextFormatFlags.NoPadding).Height;
            }

            emailHeight = Math.Max(22, emailHeight + 4);
            usernameHeight = Math.Max(24, usernameHeight + 6);

            _lblName.SetBounds(textLeft, nameTop, textWidth, nameHeight);
            _lblEmail.SetBounds(textLeft, _lblName.Bottom + 6, textWidth, emailHeight);
            _lblUsername.SetBounds(textLeft, _lblEmail.Bottom + 6, textWidth, usernameHeight);

            int neededHeaderHeight = Math.Max(_avatarPanel.Bottom + 24, _lblUsername.Bottom + 24);
            if (_headerPanel.Height != neededHeaderHeight)
            {
                _headerPanel.Height = neededHeaderHeight;
                RelayoutMenuItems();
            }
        }

        private void RelayoutMenuItems()
        {
            if (_root == null || _headerPanel == null) return;

            int y = _headerPanel.Bottom + 8;
            foreach (var panel in _menuItems)
            {
                panel.Location = new Point(0, y);
                y += ITEM_HEIGHT;
            }
        }



        private void OnThemeChanged()
        {
            if (InvokeRequired) { Invoke(new Action(OnThemeChanged)); return; }
            ThemeRefreshHelper.ApplyTo(this);  // ← delegate hết cho helper
        }
    }
}
