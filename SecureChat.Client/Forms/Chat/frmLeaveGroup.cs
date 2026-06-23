using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using SecureChat.Client.Services;


namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmLeaveGroup : Form
    {
        private readonly string _groupName;
        private readonly List<string> _groupMembers;
        private readonly List<string> _groupMemberIds;
        private readonly string _currentUserName;
        private string _appointedAdminName;
        private string _appointedAdminMemberId;

        private Label _lblInfo = null!;
        private AvatarControl _rightAvatar = null!;

        public bool LeaveConfirmed { get; private set; }
        public string AppointedAdminName => _appointedAdminName;
        public string AppointedAdminMemberId => _appointedAdminMemberId;

        public frmLeaveGroup(string groupName, string nextOwnerName, IEnumerable<string>? groupMembers = null, IEnumerable<string>? groupMemberIds = null)
        {
            ThemeRefreshHelper.Hook(this);
            _groupName = string.IsNullOrWhiteSpace(groupName) ? "this group" : groupName.Trim();
            _appointedAdminName = nextOwnerName?.Trim() ?? string.Empty;

            var memberList = (groupMembers ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            _groupMembers = memberList;
            _groupMemberIds = (groupMemberIds ?? Enumerable.Empty<string>()).ToList();

            if (_groupMembers.Count == 0 && !string.IsNullOrWhiteSpace(_appointedAdminName))
                _groupMembers.Add(_appointedAdminName);

            if (!string.IsNullOrWhiteSpace(_appointedAdminName) &&
                !_groupMembers.Any(x => string.Equals(x, _appointedAdminName, StringComparison.OrdinalIgnoreCase)))
                _groupMembers.Insert(0, _appointedAdminName);

            // Khớp appointedAdminName với memberId
            var idx = _groupMembers.IndexOf(_appointedAdminName);
            _appointedAdminMemberId = idx >= 0 && idx < _groupMemberIds.Count ? _groupMemberIds[idx] : string.Empty;

            _currentUserName = _groupMembers.FirstOrDefault(x => !string.Equals(x, _appointedAdminName, StringComparison.OrdinalIgnoreCase))
                ?? (string.IsNullOrWhiteSpace(_appointedAdminName) ? "You" : _appointedAdminName);

            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            DoubleBuffered = true;

            Text = "Leave Group";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            BackColor = TG.WindowBg;
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(430, 360);

            var header = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(430, 120),
                BackColor = TG.WindowBg
            };
            EnableDoubleBuffer(header);

            var leftAvatar = new AvatarControl
            {
                Location = new Point(108, 26),
                Size = new Size(84, 84)
            };
            leftAvatar.SetName(_currentUserName);

            _rightAvatar = new AvatarControl
            {
                Location = new Point(228, 26),
                Size = new Size(84, 84)
            };
            _rightAvatar.SetName(_appointedAdminName);

            var arrow = new ArrowSwapControl
            {
                Location = new Point(196, 52),
                Size = new Size(30, 18),
                BackColor = TG.WindowBg
            };

            var badge = new Label
            {
                Text = "A",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0xF4, 0x9B, 0x3B),
                Font = new Font("Segoe UI Semibold", 9f),
                Size = new Size(30, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(270, 72)
            };
            badge.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = new GraphicsPath();
                path.AddEllipse(0, 0, badge.Width, badge.Height);
                badge.Region = new Region(path);
            };

            header.Controls.AddRange(new Control[] { leftAvatar, _rightAvatar, arrow, badge });

            var lblTitle = new Label
            {
                Text = "Leave Group?",
                Font = new Font("Segoe UI Semibold", 16f),
                ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D),
                AutoSize = false,
                Size = new Size(430, 36),
                Location = new Point(0, 124),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _lblInfo = new Label
            {
                Font = new Font("Segoe UI", 11f),
                ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D),
                AutoSize = false,
                Size = new Size(430, 62),
                Location = new Point(0, 168),
                TextAlign = ContentAlignment.TopCenter
            };

            var btnAppoint = BuildActionButton("Appoint Another Admin", Color.FromArgb(0x2A, 0xAB, 0xEE));
            btnAppoint.Location = new Point((ClientSize.Width - btnAppoint.Width) / 2, 240);
            btnAppoint.Click += (_, __) => OpenAppointAdminDialog();

            var btnCancel = BuildActionButton("Cancel", Color.FromArgb(0x2A, 0xAB, 0xEE));
            btnCancel.Location = new Point((ClientSize.Width - btnCancel.Width) / 2, 280);
            btnCancel.Click += (_, __) => DialogResult = DialogResult.Cancel;

            var btnLeave = BuildActionButton("Leave group", Color.FromArgb(0xE2, 0x4B, 0x4A), bold: true);
            btnLeave.Location = new Point((ClientSize.Width - btnLeave.Width) / 2, 318);
            btnLeave.Click += (_, __) =>
            {
                LeaveConfirmed = true;
                DialogResult = DialogResult.OK;
            };

            Controls.AddRange(new Control[] { header, lblTitle, _lblInfo, btnAppoint, btnCancel, btnLeave });
            RefreshOwnerPreview();
        }

        private void OpenAppointAdminDialog()
        {
            using var dlg = new frmAppointAdmin(_groupMembers, _appointedAdminName);
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            if (string.IsNullOrWhiteSpace(dlg.SelectedAdminName))
                return;

            _appointedAdminName = dlg.SelectedAdminName.Trim();
            var nameIdx = _groupMembers.IndexOf(_appointedAdminName);
            _appointedAdminMemberId = nameIdx >= 0 && nameIdx < _groupMemberIds.Count ? _groupMemberIds[nameIdx] : string.Empty;
            RefreshOwnerPreview();

            if (!dlg.AppointAndLeaveConfirmed)
                return;

            LeaveConfirmed = true;
            DialogResult = DialogResult.OK;
        }

        private void RefreshOwnerPreview()
        {
            _lblInfo.Text = $"If you leave, {_appointedAdminName} will immediately\r\nbecome the new owner of {_groupName}.";
            _rightAvatar.SetName(_appointedAdminName);
        }

        private static void EnableDoubleBuffer(Control control)
        {
            typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(control, true, null);
        }

        private sealed class ArrowSwapControl : Control
        {
            public ArrowSwapControl()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(Color.FromArgb(0x95, 0xA2, 0xB0), 2f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };

                e.Graphics.DrawLine(pen, 2, Height / 2, Width - 10, Height / 2);
                e.Graphics.DrawLine(pen, Width - 14, Height / 2 - 4, Width - 10, Height / 2);
                e.Graphics.DrawLine(pen, Width - 14, Height / 2 + 4, Width - 10, Height / 2);
            }
        }

        private static Button BuildActionButton(string text, Color color, bool bold = false)
        {
            var btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = color,
                Font = new Font("Segoe UI", 11f, bold ? FontStyle.Bold : FontStyle.Regular),
                Size = new Size(230, 34),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }
    }
}
