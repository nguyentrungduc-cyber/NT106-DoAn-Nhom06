using System;
using System.Drawing;
using System.Windows.Forms;
using SecureChat.Client.Services;
using SecureChat.Client.Settings;

namespace SecureChat.Client.Forms.Settings
{
    public class frmNotificationsSounds : Form
    {
        // Colors read from TG at paint time

        private TrackBar _volume = null!;
        private Label _lblVolumeVal = null!;
        private CheckBox _chkDesktop = null!;
        private CheckBox _chkFlash = null!;
        private CheckBox _chkSound = null!;
        private CheckBox _chkPrivate = null!;
        private CheckBox _chkGroup = null!;
        private CheckBox _chkChannel = null!;
        private CheckBox _chkContactJoined = null!;
        private CheckBox _chkPinned = null!;

        public frmNotificationsSounds()
        {
            NightModeService.ThemeChanged += OnThemeChanged;
            FormClosed += (_, __) => NightModeService.ThemeChanged -= OnThemeChanged;
            InitializeComponent();
            BuildUI();
            LoadSettings();
        }

        private void InitializeComponent() { }

        private void LoadSettings()
        {
            var s = NotificationSettings.Default;
            _chkDesktop.Checked = s.DesktopNotifications;
            _chkFlash.Checked = s.FlashTaskbar;
            _chkSound.Checked = s.AllowSound;
            _volume.Value = s.Volume;
            _chkPrivate.Checked = s.PrivateChatNotifications;
            _chkGroup.Checked = s.GroupNotifications;
            _chkChannel.Checked = s.ChannelNotifications;
            _chkContactJoined.Checked = s.ContactJoinedNotifications;
            _chkPinned.Checked = s.PinnedMessageNotifications;
        }

        private void SaveSettings()
        {
            var s = NotificationSettings.Default;
            s.DesktopNotifications = _chkDesktop.Checked;
            s.FlashTaskbar = _chkFlash.Checked;
            s.AllowSound = _chkSound.Checked;
            s.Volume = _volume.Value;
            s.PrivateChatNotifications = _chkPrivate.Checked;
            s.GroupNotifications = _chkGroup.Checked;
            s.ChannelNotifications = _chkChannel.Checked;
            s.ContactJoinedNotifications = _chkContactJoined.Checked;
            s.PinnedMessageNotifications = _chkPinned.Checked;
            s.Save();
            NotificationSettings.Reload();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SaveSettings();
            base.OnFormClosed(e);
        }

        private void BuildUI()
        {
            Text = "Notifications and Sounds";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            HelpButton = false;
            ControlBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 740);
            BackColor = TG.WindowBg;
            SecureChat.Client.Services.ThemeRefreshHelper.Hook(this);
            Font = TG.FontRegular(10.5f);
            DoubleBuffered = true;

            var btnBack = FlatButton("←");
            btnBack.Location = new Point(12, 12);
            btnBack.Click += (_, __) => Close();

            var btnClose = FlatButton("✕");
            btnClose.Location = new Point(ClientSize.Width - btnClose.Width - 12, 12);
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Click += (_, __) => Close();

            var lblTitle = new Label
            {
                Text = "Notifications and Sounds",
                ForeColor = TG.TextPrimary,
                Font = TG.FontSemiBold(12.5f),
                AutoSize = true,
                Location = new Point(18, 48)
            };

            var container = new Panel
            {
                Location = new Point(12, 80),
                Size = new Size(ClientSize.Width - 24, ClientSize.Height - 92),
                AutoScroll = true,
                BackColor = TG.WindowBg,
                Padding = new Padding(12, 0, 12, 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            int y = 0;
            AddSectionHeader(container, "Global settings", ref y);
            _chkDesktop = AddToggle(container, "Desktop notifications", "notifications.png", ref y);
            _chkFlash = AddToggle(container, "Flash the taskbar icon", "notifications.png", ref y);
            _chkSound = AddToggle(container, "Allow sound", "volume_mute.png", ref y);

            _chkDesktop.CheckedChanged += (_, __) => SaveSettings();
            _chkFlash.CheckedChanged += (_, __) => SaveSettings();
            _chkSound.CheckedChanged += (_, __) => SaveSettings();

            AddSectionHeader(container, "Volume", ref y);
            AddVolume(container, ref y);

            AddSectionHeader(container, "Notifications for chats", ref y);
            _chkPrivate = AddToggle(container, "Private chats", "mode_messages.png", ref y, true);
            _chkGroup = AddToggle(container, "Groups", "stories_to_chats.png", ref y, true);
            _chkChannel = AddToggle(container, "Channels", "show_in_chat.png", ref y, true);

            _chkPrivate.CheckedChanged += (_, __) => SaveSettings();
            _chkGroup.CheckedChanged += (_, __) => SaveSettings();
            _chkChannel.CheckedChanged += (_, __) => SaveSettings();

            AddSectionHeader(container, "Events", ref y);
            _chkContactJoined = AddToggle(container, "Contact joined", "upload_chat_photo.png", ref y, true);
            _chkPinned = AddToggle(container, "Pinned messages", "saved_messages.png", ref y, true);

            _chkContactJoined.CheckedChanged += (_, __) => SaveSettings();
            _chkPinned.CheckedChanged += (_, __) => SaveSettings();

            Controls.AddRange(new Control[] { btnBack, btnClose, lblTitle, container });
        }

        private void AddSectionHeader(Control parent, string text, ref int y)
        {
            var lbl = new Label
            {
                Text = text,
                ForeColor = TG.TextPrimary,
                Font = TG.FontSemiBold(11f),
                AutoSize = true,
                Location = new Point(18, y + 12)
            };
            parent.Controls.Add(lbl);
            y += 32;
        }

        private CheckBox AddToggle(Control parent, string text, string iconFile, ref int y, bool withDivider = false)
        {
            var leftPad = parent.Padding.Left;
            var rightPad = parent.Padding.Right;
            var panel = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(parent.Width - leftPad - rightPad, 48),
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };

            var icon = new PictureBox
            {
                Size = new Size(22, 22),
                Location = new Point(leftPad, 13),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = SettingsGlyphIcons.Create(iconFile, 22),
                BackColor = Color.Transparent
            };

            var lbl = new Label
            {
                Text = text,
                ForeColor = TG.TextPrimary,
                Font = TG.FontRegular(10.5f),
                AutoSize = true,
                Location = new Point(leftPad + 30, 14)
            };

            var toggle = CreateToggle();
            toggle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            panel.Resize += (_, __) => toggle.Location = new Point(panel.Width - rightPad - toggle.Width, (panel.Height - toggle.Height) / 2);
            toggle.Location = new Point(panel.Width - rightPad - toggle.Width, (panel.Height - toggle.Height) / 2);

            panel.Controls.AddRange(new Control[] { icon, lbl, toggle });
            parent.Controls.Add(panel);
            y += panel.Height;

            if (withDivider)
            {
                var sep = new Panel
                {
                    Location = new Point(leftPad, y),
                    Size = new Size(parent.Width - leftPad - rightPad, 1),
                    BackColor = TG.Divider,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
                };
                parent.Controls.Add(sep);
                y += 10;
            }
            return toggle;
        }

        private void AddVolume(Control parent, ref int y)
        {
            var leftPad = parent.Padding.Left;
            var rightPad = parent.Padding.Right;
            int volumeY = y;

            _volume = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 100,
                TickStyle = TickStyle.None,
                SmallChange = 1,
                LargeChange = 5,
                BackColor = TG.WindowBg,
                Size = new Size(parent.Width - leftPad - rightPad - 80, 30),
                Location = new Point(leftPad, volumeY + 6),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _lblVolumeVal = new Label
            {
                Text = "100%",
                ForeColor = TG.TextSecondary,
                Font = TG.FontRegular(10.5f),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            void PositionVal() => _lblVolumeVal.Location = new Point(parent.Width - rightPad - _lblVolumeVal.Width, volumeY + 8);

            PositionVal();
            parent.Resize += (_, __) => PositionVal();
            _volume.ValueChanged += (_, __) => { _lblVolumeVal.Text = _volume.Value + "%"; PositionVal(); SaveSettings(); };

            parent.Controls.Add(_volume);
            parent.Controls.Add(_lblVolumeVal);
            y += 42;
        }

        private CheckBox CreateToggle()
        {
            var chk = new CheckBox
            {
                Appearance = Appearance.Button,
                AutoSize = false,
                Size = new Size(44, 22),
                BackColor = Color.Transparent,
                Checked = true,
                FlatStyle = FlatStyle.Flat
            };
            chk.FlatAppearance.BorderSize = 0;
            chk.Paint += (_, e) => DrawToggle(chk, e.Graphics);
            return chk;
        }

        private void DrawToggle(CheckBox chk, Graphics g)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, chk.Width - 1, chk.Height - 1);
            int r = rect.Height / 2;
            var track = chk.Checked ? TG.CAccent : TG.TextSecondary;

            using var trackBrush = new SolidBrush(track);
            using var thumbBrush = new SolidBrush(TG.WindowBg);

            g.FillEllipse(trackBrush, rect.Left, rect.Top, rect.Height, rect.Height);
            g.FillEllipse(trackBrush, rect.Right - rect.Height, rect.Top, rect.Height, rect.Height);
            g.FillRectangle(trackBrush, rect.Left + r, rect.Top, rect.Width - rect.Height, rect.Height);

            int thumbX = chk.Checked ? rect.Right - rect.Height + 2 : rect.Left + 2;
            g.FillEllipse(thumbBrush, thumbX, rect.Top + 2, rect.Height - 4, rect.Height - 4);
        }

        private static Button FlatButton(string text)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TG.TextSecondary,
                Font = TG.FontSemiBold(11f),
                TabStop = false,
                Cursor = Cursors.Hand,
                UseCompatibleTextRendering = true,
                Padding = new Padding(6, 2, 6, 2)
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = TG.SidebarHover;
            b.FlatAppearance.MouseDownBackColor = TG.SidebarHover;
            return b;
        }
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
