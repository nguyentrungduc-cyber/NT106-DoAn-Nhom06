using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmAppointAdmin : Form
    {
        private readonly List<MemberItem> _allMembers;
        private readonly Panel _pnlMembers;
        private readonly TextBox _txtSearch;
        private readonly Button _btnAppointAndLeave;

        private string _selectedMemberName;

        public string SelectedAdminName => _selectedMemberName;
        public bool AppointAndLeaveConfirmed { get; private set; }

        public frmAppointAdmin(IEnumerable<string> memberNames, string currentAdmin)
        {
            _allMembers = memberNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(x => new MemberItem(x.Trim(), "Select as admin"))
                .ToList();

            if (_allMembers.Count == 0)
                _allMembers.Add(new MemberItem(string.IsNullOrWhiteSpace(currentAdmin) ? "Group member" : currentAdmin.Trim(), "Select as admin"));

            _selectedMemberName = _allMembers.Any(x => string.Equals(x.Name, currentAdmin, StringComparison.OrdinalIgnoreCase))
                ? _allMembers.First(x => string.Equals(x.Name, currentAdmin, StringComparison.OrdinalIgnoreCase)).Name
                : _allMembers[0].Name;

            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            DoubleBuffered = true;

            Text = "Appoint New Admin";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(454, 740);
            Opacity = 0;
            Shown += (_, __) => StartFadeIn();

            var lblTitle = new Label
            {
                Text = "Appoint New Admin",
                Font = new Font("Segoe UI Semibold", 16f),
                ForeColor = Color.FromArgb(0x10, 0x28, 0x45),
                Location = new Point(24, 20),
                Size = new Size(300, 34),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var pnlSearch = new Panel
            {
                Location = new Point(0, 58),
                Size = new Size(ClientSize.Width, 56),
                BackColor = Color.White
            };
            EnableDoubleBuffer(pnlSearch);

            var icoSearch = new SearchIconControl
            {
                Location = new Point(24, 16),
                Size = new Size(22, 22),
                BackColor = Color.White
            };

            _txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 14f),
                ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D),
                Location = new Point(58, 16),
                Size = new Size(360, 26),
                PlaceholderText = "Search"
            };
            _txtSearch.TextChanged += (_, __) => BuildMembersList();

            pnlSearch.Controls.Add(icoSearch);
            pnlSearch.Controls.Add(_txtSearch);

            var dividerTop = new Panel
            {
                Location = new Point(0, 112),
                Size = new Size(ClientSize.Width, 1),
                BackColor = Color.FromArgb(0xE6, 0xEC, 0xF2)
            };

            var pnlGroupHeader = new Panel
            {
                Location = new Point(0, 113),
                Size = new Size(ClientSize.Width, 34),
                BackColor = Color.FromArgb(0xF4, 0xF6, 0xF8)
            };

            var lblGroupMembers = new Label
            {
                Text = "Group members",
                Font = new Font("Segoe UI Semibold", 10.5f),
                ForeColor = Color.FromArgb(0x7D, 0x8B, 0x98),
                Location = new Point(24, 5),
                Size = new Size(220, 24)
            };
            pnlGroupHeader.Controls.Add(lblGroupMembers);

            _pnlMembers = new Panel
            {
                Location = new Point(0, 147),
                Size = new Size(ClientSize.Width, 500),
                BackColor = Color.White,
                AutoScroll = true
            };
            EnableDoubleBuffer(_pnlMembers);
            _pnlMembers.Resize += (_, __) => BuildMembersList();

            _btnAppointAndLeave = new Button
            {
                Text = "Appoint and Leave Group",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0x45, 0xA1, 0xD6),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 15f),
                Size = new Size(396, 42),
                Location = new Point(29, 684),
                Cursor = Cursors.Hand
            };
            _btnAppointAndLeave.FlatAppearance.BorderSize = 0;
            _btnAppointAndLeave.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x3D, 0x97, 0xCC);
            _btnAppointAndLeave.FlatAppearance.MouseDownBackColor = Color.FromArgb(0x36, 0x8E, 0xC2);
            _btnAppointAndLeave.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedPanel.GetRoundedPath(new Rectangle(0, 0, _btnAppointAndLeave.Width - 1, _btnAppointAndLeave.Height - 1), 21);
                _btnAppointAndLeave.Region = new Region(path);
            };
            _btnAppointAndLeave.Click += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(_selectedMemberName))
                    return;

                AppointAndLeaveConfirmed = true;
                DialogResult = DialogResult.OK;
            };

            Controls.AddRange(new Control[]
            {
                lblTitle,
                pnlSearch,
                dividerTop,
                pnlGroupHeader,
                _pnlMembers,
                _btnAppointAndLeave
            });

            BuildMembersList();
            UpdateBottomButtonState();
        }

        private void StartFadeIn()
        {
            var timer = new System.Windows.Forms.Timer { Interval = 12 };
            timer.Tick += (_, __) =>
            {
                Opacity = Math.Min(1, Opacity + 0.14);
                if (Opacity >= 1)
                {
                    timer.Stop();
                    timer.Dispose();
                }
            };
            timer.Start();
        }

        private void BuildMembersList()
        {
            _pnlMembers.SuspendLayout();
            _pnlMembers.Controls.Clear();

            var q = _txtSearch.Text.Trim();
            var filtered = string.IsNullOrWhiteSpace(q)
                ? _allMembers
                : _allMembers.Where(x => x.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

            var y = 0;
            foreach (var member in filtered)
            {
                var row = BuildMemberRow(member);
                row.Location = new Point(0, y);
                row.Width = _pnlMembers.ClientSize.Width;
                row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                _pnlMembers.Controls.Add(row);
                y += row.Height;
            }

            if (filtered.Count == 0)
            {
                var empty = new Label
                {
                    Text = "No members found",
                    Font = new Font("Segoe UI", 11f),
                    ForeColor = Color.FromArgb(0x8E, 0x9A, 0xA7),
                    AutoSize = false,
                    Location = new Point(24, 18),
                    Size = new Size(_pnlMembers.ClientSize.Width - 48, 24),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                _pnlMembers.Controls.Add(empty);
            }

            _pnlMembers.ResumeLayout();
            UpdateBottomButtonState();
        }

        private Panel BuildMemberRow(MemberItem member)
        {
            var selected = string.Equals(_selectedMemberName, member.Name, StringComparison.OrdinalIgnoreCase);
            var baseColor = selected ? Color.FromArgb(0xE9, 0xF4, 0xFC) : Color.White;

            var row = new Panel
            {
                Height = 74,
                BackColor = baseColor,
                Cursor = Cursors.Hand
            };
            EnableDoubleBuffer(row);

            var avatar = new AvatarControl
            {
                Location = new Point(24, 12),
                Size = new Size(48, 48)
            };
            avatar.SetName(member.Name);

            var lblName = new Label
            {
                Text = member.Name,
                Font = new Font("Segoe UI Semibold", 15f),
                ForeColor = Color.FromArgb(0x0F, 0x1B, 0x2A),
                Location = new Point(94, 10),
                Size = new Size(260, 30),
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblStatus = new Label
            {
                Text = member.Status,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(0x8E, 0x9A, 0xA7),
                Location = new Point(94, 41),
                Size = new Size(260, 22),
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var selectionMark = new SelectionMarkControl
            {
                Size = new Size(24, 24),
                Visible = selected,
                BackColor = baseColor
            };

            row.Resize += (_, __) =>
            {
                var nameWidth = Math.Max(120, row.Width - 150);
                lblName.Width = nameWidth;
                lblStatus.Width = nameWidth;
                selectionMark.Location = new Point(Math.Max(0, row.Width - 36), 23);
            };

            row.Controls.Add(avatar);
            row.Controls.Add(lblName);
            row.Controls.Add(lblStatus);
            row.Controls.Add(selectionMark);

            void SelectMember()
            {
                _selectedMemberName = member.Name;
                BuildMembersList();
            }

            row.MouseEnter += (_, __) =>
            {
                if (!selected)
                    row.BackColor = Color.FromArgb(0xF7, 0xFA, 0xFD);
            };
            row.MouseLeave += (_, __) => row.BackColor = baseColor;

            row.Click += (_, __) => SelectMember();
            avatar.Click += (_, __) => SelectMember();
            lblName.Click += (_, __) => SelectMember();
            lblStatus.Click += (_, __) => SelectMember();

            row.PerformLayout();
            return row;
        }

        private void UpdateBottomButtonState()
        {
            var canSubmit = !string.IsNullOrWhiteSpace(_selectedMemberName);
            _btnAppointAndLeave.Enabled = canSubmit;
            _btnAppointAndLeave.BackColor = canSubmit
                ? Color.FromArgb(0x45, 0xA1, 0xD6)
                : Color.FromArgb(0xA8, 0xC7, 0xDB);
        }

        private static void EnableDoubleBuffer(Control control)
        {
            typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(control, true, null);
        }

        private sealed record MemberItem(string Name, string Status);

        private sealed class SearchIconControl : Control
        {
            public SearchIconControl()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var color = Color.FromArgb(0x97, 0xA4, 0xB2);
                using var pen = new Pen(color, 2f);
                e.Graphics.DrawEllipse(pen, 3, 3, 11, 11);
                e.Graphics.DrawLine(pen, 12, 12, 18, 18);
            }
        }

        private sealed class SelectionMarkControl : Control
        {
            public SelectionMarkControl()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(Color.FromArgb(0x45, 0xA1, 0xD6));
                e.Graphics.FillEllipse(brush, 2, 2, 20, 20);

                using var pen = new Pen(Color.White, 2f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                e.Graphics.DrawLine(pen, 8, 13, 11, 16);
                e.Graphics.DrawLine(pen, 11, 16, 17, 9);
            }
        }
    }
}
