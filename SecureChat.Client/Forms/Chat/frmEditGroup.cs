using SecureChat.Client.Services;
using System.Drawing;

namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmEditGroup : Form
    {
        private readonly TextBox _txtName;
        private readonly TextBox _txtDescription;
        private readonly PictureBox _avatar;

        private readonly Label _lblDescPlaceholder;
        private readonly Label _lblGroupTypeValue;
        private readonly Label _lblChatHistoryValue;
        private readonly Label _lblAdminsValue;
        private readonly Label _lblMembersValue;

        private readonly System.Windows.Forms.Timer _fadeTimer;
        private bool _disposed;

        private string _groupType = "Private";
        private string _chatHistory = "Hidden";
        private int _adminsCount = 0;
        private int _membersCount = 0;
        private readonly string _conversationId; // Thêm biến lưu ID nhóm

        public string GroupName { get; private set; }
        public string? NewAvatarPath { get; private set; }
        public string DescriptionText => _txtDescription.Text.Trim();
        public string GroupType => _groupType;
        public string ChatHistoryMode => _chatHistory;
        public int AdminsCount => _adminsCount;
        public int MembersCount => _membersCount;
        public Image? GroupAvatar => _avatar.Image;

        public frmEditGroup(string conversationId, string currentName)
        {
            NightModeService.ThemeChanged += OnThemeChanged;
            FormClosed += (_, __) => NightModeService.ThemeChanged -= OnThemeChanged;
            _conversationId = conversationId; // Gán ID nhóm
            GroupName = currentName;

            Text = "Edit group";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            BackColor = TG.WindowBg;
            SecureChat.Client.Services.ThemeRefreshHelper.Hook(this);
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(520, 720);
            DoubleBuffered = true;
            Opacity = 0;

            _fadeTimer = new System.Windows.Forms.Timer { Interval = 14 };
            _fadeTimer.Tick += (_, __) =>
            {
                if (Opacity >= 1)
                {
                    _fadeTimer.Stop();
                    return;
                }
                Opacity = Math.Min(1, Opacity + 0.12);
            };
            Shown += (_, __) => _fadeTimer.Start();

            var lblTitle = new Label
            {
                Text = "Edit group",
                Font = new Font("Segoe UI Semibold", 18f),
                ForeColor = TG.TextPrimary,
                Location = new Point(20, 18),
                Size = new Size(260, 38)
            };

            var avatarHost = new Panel
            {
                Location = new Point(28, 78),
                Size = new Size(92, 92),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            _avatar = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = TG.Blue,
                // Fill the full circular frame when user selects an image.
                SizeMode = PictureBoxSizeMode.StretchImage,
                Cursor = Cursors.Hand
            };
            _avatar.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                if (_avatar.Image == null)
                {
                    e.Graphics.FillEllipse(new SolidBrush(TG.Blue), 0, 0, _avatar.Width - 1, _avatar.Height - 1);
                    TextRenderer.DrawText(e.Graphics, "\U0001F4F7", new Font("Segoe UI Emoji", 24f), _avatar.ClientRectangle, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }

                using var path = new System.Drawing.Drawing2D.GraphicsPath();
                path.AddEllipse(0, 0, _avatar.Width, _avatar.Height);
                _avatar.Region = new Region(path);
            };
            _avatar.Click += (_, __) => PickAvatarImage();
            avatarHost.Click += (_, __) => PickAvatarImage();
            avatarHost.Controls.Add(_avatar);

            var lblName = new Label
            {
                Text = "Group name",
                Font = new Font("Segoe UI", 11f),
                ForeColor = TG.TextBlue,
                Location = new Point(150, 86),
                Size = new Size(170, 28)
            };

            _txtName = new TextBox
            {
                Text = currentName,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 16f),
                ForeColor = TG.TextPrimary,
                Location = new Point(150, 116),
                Size = new Size(320, 36),
                BackColor = TG.WindowBg
            };

            var nameUnderline = new Panel
            {
                BackColor = TG.TextBlue,
                Location = new Point(150, 156),
                Size = new Size(280, 2)
            };

            _lblDescPlaceholder = new Label
            {
                Text = "Description (optional)",
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = TG.TextSecondary,
                Location = new Point(28, 186),
                Size = new Size(240, 26),
                BackColor = Color.Transparent,
                Cursor = Cursors.IBeam
            };

            _txtDescription = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 11f),
                ForeColor = TG.TextPrimary,
                Location = new Point(28, 214),
                Size = new Size(460, 58),
                Multiline = true,
                BackColor = TG.WindowBg
            };
            _txtDescription.TextChanged += (_, __) => _lblDescPlaceholder.Visible = string.IsNullOrWhiteSpace(_txtDescription.Text);
            _lblDescPlaceholder.Click += (_, __) => _txtDescription.Focus();

            var section = new Panel
            {
                Location = new Point(0, 286),
                Size = new Size(520, 312),
                BackColor = TG.WindowBg
            };
            section.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(520, 1), BackColor = TG.Divider });

            var rowGroupType = BuildSettingsRow("\u2699\uFE0F  Group type", _groupType, out _lblGroupTypeValue);
            rowGroupType.Location = new Point(0, 8);
            BindRowAction(rowGroupType, OpenGroupTypeSettings);

            var rowHistory = BuildSettingsRow("\U0001F4AC  Chat history for new members", _chatHistory, out _lblChatHistoryValue);
            rowHistory.Location = new Point(0, 54);
            BindRowAction(rowHistory, OpenChatHistorySettings);

            var rowAdmins = BuildSettingsRow("\U0001F6E1\uFE0F  Administrators", _adminsCount.ToString(), out _lblAdminsValue);
            rowAdmins.Location = new Point(0, 100);
            BindRowAction(rowAdmins, OpenAdministratorsSettings);

            var rowMembers = BuildSettingsRow("\U0001F465  Members", _membersCount.ToString(), out _lblMembersValue);
            rowMembers.Location = new Point(0, 146);
            BindRowAction(rowMembers, OpenMembersSettings);

            section.Controls.AddRange(new Control[] { rowGroupType, rowHistory, rowAdmins, rowMembers });

            var btnCancel = BuildBottomButton("Cancel", TG.TextBlue);
            btnCancel.Location = new Point(300, 676);
            btnCancel.Click += (_, __) => DialogResult = DialogResult.Cancel;

            var btnSave = BuildBottomButton("Save", TG.TextBlue, bold: true);
            btnSave.Location = new Point(392, 676);
            btnSave.Click += (_, __) =>
            {
                var n = _txtName.Text.Trim();
                if (string.IsNullOrWhiteSpace(n))
                {
                    MessageBox.Show(this, "Group name cannot be empty.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _txtName.Focus();
                    return;
                }

                GroupName = n;
                DialogResult = DialogResult.OK;
            };

            Controls.AddRange(new Control[]
            {
                lblTitle, avatarHost, lblName, _txtName, nameUnderline,
                _lblDescPlaceholder, _txtDescription, section,
                btnCancel, btnSave
            });
            _ = LoadGroupInfoAsync();
        }

        private Panel BuildSettingsRow(string leftText, string rightText, out Label rightValue)
        {
            var pnl = new Panel
            {
                Size = new Size(520, 46),
                BackColor = TG.WindowBg,
                Cursor = Cursors.Hand
            };

            var left = new Label
            {
                Text = leftText,
                Font = new Font("Segoe UI Emoji", 10.5f),
                ForeColor = TG.TextPrimary,
                Location = new Point(30, 8),
                Size = new Size(330, 30),
                BackColor = Color.Transparent
            };

            rightValue = new Label
            {
                Text = rightText,
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = TG.TextBlue,
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(360, 8),
                Size = new Size(130, 30),
                BackColor = Color.Transparent
            };

            var sep = new Panel
            {
                Location = new Point(30, 45),
                Size = new Size(460, 1),
                BackColor = TG.Divider
            };

            pnl.Controls.AddRange(new Control[] { left, rightValue, sep });

            pnl.MouseEnter += (_, __) => pnl.BackColor = TG.SidebarHover;
            pnl.MouseLeave += (_, __) => pnl.BackColor = TG.WindowBg;

            return pnl;
        }

        private static void BindRowAction(Panel row, Action action)
        {
            row.Click += (_, __) => action();
            foreach (Control c in row.Controls)
                c.Click += (_, __) => action();
        }

        private void OpenGroupTypeSettings()
        {
            using var dlg = new frmGroupTypeSettings(_groupType);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            _groupType = dlg.GroupType;
            _lblGroupTypeValue.Text = _groupType;
        }

        private void OpenChatHistorySettings()
        {
            using var dlg = new frmChatHistorySettings(_chatHistory);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            _chatHistory = dlg.ChatHistoryMode;
            _lblChatHistoryValue.Text = _chatHistory;
        }

        private void OpenAdministratorsSettings()
        {
            using var dlg = new frmAdministratorsSettings(_conversationId, _adminsCount);
            dlg.ShowDialog(this);

            _ = LoadGroupInfoAsync();
        }
        private void OpenMembersSettings()
        {
            using var dlg = new frmMembersSettings(_conversationId);
            dlg.ShowDialog(this);

            _ = LoadGroupInfoAsync();
        }

        private void PickAvatarImage()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Choose group avatar",
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
                Multiselect = false
            };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                using var fs = new FileStream(ofd.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var img = Image.FromStream(fs);
                _avatar.Image?.Dispose();
                _avatar.Image = new Bitmap(img);
                _avatar.Invalidate();
                NewAvatarPath = ofd.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load image.\n{ex.Message}", "Image", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static Button BuildBottomButton(string text, Color color, bool bold = false)
        {
            var btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = color,
                Font = new Font("Segoe UI", 11f, bold ? FontStyle.Bold : FontStyle.Regular),
                Size = new Size(90, 34),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _disposed = true;
            _fadeTimer.Stop();
            _fadeTimer.Dispose();
            _avatar.Image?.Dispose();
            _avatar.Image = null;
            base.OnFormClosed(e);
        }

        private async Task LoadGroupInfoAsync()
        {
            var (ok, view, err) = await SecureChat.Client.Services.ApiClient.Instance
                .GetAsync<SecureChat.DTOs.ConversationViewResponse>($"api/conversations/{_conversationId}/view");

            if (!ok || view?.Metadata == null || !this.IsHandleCreated)
                return;

            var meta = view.Metadata;
            this.Invoke(new Action(() =>
            {
                if (_disposed) return;

                if (!string.IsNullOrWhiteSpace(meta.Description))
                {
                    _txtDescription.Text = meta.Description;
                    _lblDescPlaceholder.Visible = false;
                }

                if (meta.GroupType.HasValue)
                {
                    _groupType = meta.GroupType.Value == SecureChat.Models.GroupVisibility.Public ? "Public" : "Private";
                    _lblGroupTypeValue.Text = _groupType;
                }

                if (meta.ChatHistoryMode.HasValue)
                {
                    _chatHistory = meta.ChatHistoryMode.Value == SecureChat.Models.HistoryMode.Visible ? "Visible" : "Hidden";
                    _lblChatHistoryValue.Text = _chatHistory;
                }

                _membersCount = meta.MemberCount;
                _lblMembersValue.Text = _membersCount.ToString();
                _adminsCount = meta.AdminCount;
                _lblAdminsValue.Text = _adminsCount.ToString();

                if (!string.IsNullOrWhiteSpace(meta.AvatarURL))
                {
                    _ = LoadAvatarAsync(meta.AvatarURL);
                }
            }));
        }

        private async Task LoadAvatarAsync(string avatarUrl)
        {
            try
            {
                var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                var resolvedUrl = avatarUrl.StartsWith("http")
                    ? avatarUrl
                    : $"{SecureChat.Client.Services.ApiClient.Instance.GetBaseUrl()}/{avatarUrl.TrimStart('/')}";
                var imgRes = await http.GetAsync(resolvedUrl);
                if (!imgRes.IsSuccessStatusCode || _disposed) return;
                using var imgStream = await imgRes.Content.ReadAsStreamAsync();
                var img = new System.Drawing.Bitmap(imgStream);
                if (_disposed) { img.Dispose(); return; }
                this.Invoke(new Action(() =>
                {
                    if (_disposed) { img.Dispose(); return; }
                    _avatar.Image?.Dispose();
                    _avatar.Image = img;
                    _avatar.Invalidate();
                }));
            }
            catch { }
        }


        private void OnThemeChanged()
        {
            if (InvokeRequired) { Invoke(new Action(OnThemeChanged)); return; }
            ThemeRefreshHelper.ApplyTo(this);  // ← delegate hết cho helper
        }
    }
}
