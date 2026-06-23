using System;
using SecureChat.Client.Services;
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
            NightModeService.ThemeChanged += OnThemeChanged;
            FormClosed += (_, __) => NightModeService.ThemeChanged -= OnThemeChanged;
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
            BackColor = TG.WindowBg;
            SecureChat.Client.Services.ThemeRefreshHelper.Hook(this);
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(454, 740);
            Opacity = 0;
            Shown += (_, __) => StartFadeIn();

            var lblTitle = new Label
            {
                Text = "Appoint New Admin",
                Font = new Font("Segoe UI Semibold", 16f),
                ForeColor = TG.TextPrimary,
                Location = new Point(24, 20),
                Size = new Size(300, 34),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var pnlSearch = new Panel
            {
                Location = new Point(0, 58),
                Size = new Size(ClientSize.Width, 56),
                BackColor = TG.WindowBg
            };
            EnableDoubleBuffer(pnlSearch);

            var icoSearch = new SearchIconControl
            {
                Location = new Point(24, 16),
                Size = new Size(22, 22),
                BackColor = TG.WindowBg
            };

            _txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 14f),
                ForeColor = TG.TextPrimary,
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
                BackColor = TG.InputBg
            };

            var pnlGroupHeader = new Panel
            {
                Location = new Point(0, 113),
                Size = new Size(ClientSize.Width, 34),
                BackColor = TG.SidebarHover
            };

            var lblGroupMembers = new Label
            {
                Text = "Group members",
                Font = new Font("Segoe UI Semibold", 10.5f),
                ForeColor = TG.TextSecondary,
                Location = new Point(24, 5),
                Size = new Size(220, 24)
            };
            pnlGroupHeader.Controls.Add(lblGroupMembers);

            _pnlMembers = new Panel
            {
                Location = new Point(0, 147),
                Size = new Size(ClientSize.Width, 500),
                BackColor = TG.WindowBg,
                AutoScroll = true
            };
            EnableDoubleBuffer(_pnlMembers);
            _pnlMembers.Resize += (_, __) => BuildMembersList();

            _btnAppointAndLeave = new Button
            {
                Text = "Appoint and Leave Group",
                FlatStyle = FlatStyle.Flat,
                BackColor = TG.Blue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 15f),
                Size = new Size(396, 42),
                Location = new Point(29, 684),
                Cursor = Cursors.Hand
            };
            _btnAppointAndLeave.FlatAppearance.BorderSize = 0;
            _btnAppointAndLeave.FlatAppearance.MouseOverBackColor = TG.BlueHover;
            _btnAppointAndLeave.FlatAppearance.MouseDownBackColor = TG.BlueActive;
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
                    ForeColor = TG.TextSecondary,
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
            var baseColor = selected ? TG.SidebarHover : TG.WindowBg;

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
                ForeColor = TG.TextPrimary,
                Location = new Point(94, 10),
                Size = new Size(260, 30),
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblStatus = new Label
            {
                Text = member.Status,
                Font = new Font("Segoe UI", 10f),
                ForeColor = TG.TextSecondary,
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
                    row.BackColor = TG.SidebarHover;
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
                ? TG.Blue
                : TG.TextHint;
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
                var color = TG.TextHint;
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
                using var brush = new SolidBrush(TG.Blue);
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
