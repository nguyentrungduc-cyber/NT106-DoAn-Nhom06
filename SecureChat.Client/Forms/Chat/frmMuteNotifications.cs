using System.Drawing.Drawing2D;
using System.Drawing;
using SecureChat.Client.Services;

namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmMuteNotifications : Form
    {
        private readonly CheckBox _chkDisableSound;
        private readonly RadioButton _rbUnmuted;
        private readonly RadioButton _rbMuteForever;
        private readonly RadioButton _rbMuteFor;
        private readonly ComboBox _cbDuration;

        public bool DisableSound { get; private set; }
        public bool IsMuted { get; private set; }
        public DateTime? MuteUntilUtc { get; private set; }

        public frmMuteNotifications(bool disableSound, bool isMuted, DateTime? muteUntilUtc)
        {
            NightModeService.ThemeChanged += OnThemeChanged;
            FormClosed += (_, __) => NightModeService.ThemeChanged -= OnThemeChanged;
            Text = "Mute notifications";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            BackColor = TG.WindowBg;
            SecureChat.Client.Services.ThemeRefreshHelper.Hook(this);
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(440, 330);

            var lblTitle = new Label
            {
                Text = "Mute notifications",
                Font = new Font("Segoe UI Semibold", 14f),
                ForeColor = TG.TextPrimary,
                Location = new Point(18, 16),
                Size = new Size(330, 32)
            };

            var btnClose = new Button
            {
                Text = "\u2715",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TG.TextPrimary,
                Font = new Font("Segoe UI", 11f),
                Size = new Size(30, 28),
                Location = new Point(390, 14)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (_, __) => DialogResult = DialogResult.Cancel;

            _chkDisableSound = new CheckBox
            {
                Text = "\U0001F507  Disable sound",
                Font = new Font("Segoe UI Emoji", 10.5f),
                AutoSize = true,
                Location = new Point(22, 66),
                Checked = disableSound
            };

            _rbUnmuted = new RadioButton
            {
                Text = "\U0001F514  Notifications on",
                Font = new Font("Segoe UI Emoji", 10.5f),
                AutoSize = true,
                Location = new Point(22, 106)
            };

            _rbMuteForever = new RadioButton
            {
                Text = "\u26D4  Mute forever",
                Font = new Font("Segoe UI Emoji", 10.5f),
                AutoSize = true,
                Location = new Point(22, 138)
            };

            _rbMuteFor = new RadioButton
            {
                Text = "\u23F3  Mute for",
                Font = new Font("Segoe UI Emoji", 10.5f),
                AutoSize = true,
                Location = new Point(22, 171)
            };

            _cbDuration = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10f),
                Location = new Point(170, 168),
                Size = new Size(190, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            _cbDuration.Items.AddRange(new object[]
            {
                "30 minutes", "1 hour", "8 hours", "1 day", "1 week"
            });
            _cbDuration.SelectedIndex = 0;

            _rbUnmuted.CheckedChanged += (_, __) => SyncMuteForControlState();
            _rbMuteForever.CheckedChanged += (_, __) => SyncMuteForControlState();
            _rbMuteFor.CheckedChanged += (_, __) => SyncMuteForControlState();

            if (!isMuted)
            {
                _rbUnmuted.Checked = true;
            }
            else if (muteUntilUtc.HasValue)
            {
                _rbMuteFor.Checked = true;
                // best effort select nearest option from remaining duration
                var remaining = muteUntilUtc.Value - DateTime.UtcNow;
                if (remaining <= TimeSpan.FromMinutes(45)) _cbDuration.SelectedIndex = 0;
                else if (remaining <= TimeSpan.FromHours(2)) _cbDuration.SelectedIndex = 1;
                else if (remaining <= TimeSpan.FromHours(12)) _cbDuration.SelectedIndex = 2;
                else if (remaining <= TimeSpan.FromDays(2)) _cbDuration.SelectedIndex = 3;
                else _cbDuration.SelectedIndex = 4;
            }
            else
            {
                _rbMuteForever.Checked = true;
            }

            SyncMuteForControlState();

            var btnCancel = BuildActionButton("Cancel", TG.Blue, false);
            btnCancel.Location = new Point(234, 272);
            btnCancel.Click += (_, __) => DialogResult = DialogResult.Cancel;

            var btnSave = BuildActionButton("Save", TG.Blue, true);
            btnSave.Location = new Point(328, 272);
            btnSave.Click += (_, __) =>
            {
                DisableSound = _chkDisableSound.Checked;

                if (_rbUnmuted.Checked)
                {
                    IsMuted = false;
                    MuteUntilUtc = null;
                }
                else if (_rbMuteForever.Checked)
                {
                    IsMuted = true;
                    MuteUntilUtc = null;
                }
                else
                {
                    IsMuted = true;
                    MuteUntilUtc = DateTime.UtcNow.Add(GetSelectedDuration());
                }

                DialogResult = DialogResult.OK;
            };

            Controls.AddRange(new Control[]
            {
                lblTitle, btnClose, _chkDisableSound,
                _rbUnmuted, _rbMuteForever, _rbMuteFor, _cbDuration,
                btnCancel, btnSave
            });
        }

        private void SyncMuteForControlState()
        {
            _cbDuration.Enabled = _rbMuteFor.Checked;
            _cbDuration.BackColor = _cbDuration.Enabled ? TG.WindowBg : TG.SidebarHover;
        }

        private TimeSpan GetSelectedDuration()
        {
            return _cbDuration.SelectedItem?.ToString() switch
            {
                "30 minutes" => TimeSpan.FromMinutes(30),
                "1 hour" => TimeSpan.FromHours(1),
                "8 hours" => TimeSpan.FromHours(8),
                "1 day" => TimeSpan.FromDays(1),
                "1 week" => TimeSpan.FromDays(7),
                _ => TimeSpan.FromHours(1)
            };
        }

        private static Button BuildActionButton(string text, Color color, bool filled)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(88, 34),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = filled ? Color.White : color,
                BackColor = filled ? color : Color.Transparent,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = filled ? 0 : 1;
            btn.FlatAppearance.BorderColor = color;
            return btn;
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
