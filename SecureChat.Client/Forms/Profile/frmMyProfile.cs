using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using SecureChat.Client.Models;
using SecureChat.Client.Services;

namespace SecureChat.Client.Forms.Profile
{
    public partial class frmMyProfile : Form
    {
        private void InitializeComponent() { /* built in code */ }

        // Colors read from TG at paint time

        private readonly ProfileModel _profile;

        private PictureBox _avatar = null!;
        private Label _lblInitial = null!;
        private Label _lblName = null!;
        private Label _lblStatus = null!;
        private Label _lblEmail = null!;
        private Label _lblEmailType = null!;
        private Label _lblUsername = null!;
        private Label _lblUsernameType = null!;
        private Button _btnEdit = null!;
        private Button _btnClose = null!;

        public frmMyProfile(ProfileModel profile)
        {
            NightModeService.ThemeChanged += OnThemeChanged;
            FormClosed += (_, __) => NightModeService.ThemeChanged -= OnThemeChanged;
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            InitializeComponent();
            BuildUI();
            LoadProfile(profile);
            Resize += (_, __) => LayoutDynamic();
            LayoutDynamic();
        }

        private void BuildUI()
        {
            Text = "My Profile";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            HelpButton = false;
            ControlBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 480);
            BackColor = TG.WindowBg;
            SecureChat.Client.Services.ThemeRefreshHelper.Hook(this);
            Font = new Font("Segoe UI", 10f, GraphicsUnit.Point);
            DoubleBuffered = true;

            _avatar = new PictureBox
            {
                Size = new Size(112, 112),
                BackColor = TG.GetAvatarColor(_profile.FullName),
                SizeMode = PictureBoxSizeMode.Zoom,
            };
            _avatar.Paint += (_, __) => ClipCircle(_avatar);

            _lblInitial = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 32f, GraphicsUnit.Point),
                BackColor = Color.Transparent,
                AutoSize = false,
            };
            _avatar.Controls.Add(_lblInitial);

            _btnEdit = FlatIconButton("Edit");
            _btnEdit.Image = LoadIcon("profile_manage.png");
            _btnEdit.ImageAlign = ContentAlignment.MiddleLeft;
            _btnEdit.TextImageRelation = TextImageRelation.ImageBeforeText;
            _btnEdit.Click += (_, __) => OpenDetails();

            _btnClose = FlatIconButton("X");
            _btnClose.Click += (_, __) => Close();

            _lblName = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 13.5f, GraphicsUnit.Point),
                ForeColor = TG.TextPrimary,
                BackColor = Color.Transparent,
            };
            _lblStatus = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f, GraphicsUnit.Point),
                ForeColor = TG.TextBlue,
                BackColor = Color.Transparent,
            };

            _lblEmail = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f, GraphicsUnit.Point),
                ForeColor = TG.TextPrimary,
                BackColor = Color.Transparent,
            };

            _lblEmailType = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, GraphicsUnit.Point),
                ForeColor = TG.TextSecondary,
                BackColor = Color.Transparent,
                Text = "Email",
            };

            _lblUsername = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f, GraphicsUnit.Point),
                ForeColor = TG.TextPrimary,
                BackColor = Color.Transparent,
            };

            _lblUsernameType = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, GraphicsUnit.Point),
                ForeColor = TG.TextSecondary,
                BackColor = Color.Transparent,
                Text = "Username",
            };

            Controls.AddRange(new Control[]
            {
                _avatar, _btnEdit, _btnClose,
                _lblName, _lblStatus,
                _lblEmail, _lblEmailType,
                _lblUsername, _lblUsernameType,
            });
        }

        private void LoadProfile(ProfileModel profile)
        {
            _lblName.Text = profile.FullName;
            _lblStatus.Text = profile.StatusText;
            _lblEmail.Text = string.IsNullOrWhiteSpace(profile.Email) ? "No email" : profile.Email;
            _lblUsername.Text = FormatUsername(profile.Username);
            _lblInitial.Text = GetInitials(profile.FullName);
            _avatar.BackColor = TG.GetAvatarColor(profile.FullName);
            ApplyAvatarImage();
            LayoutDynamic();
        }

        private void OpenDetails()
        {
            try
            {
                using var dlg = new frmProfileInfo(_profile);
                dlg.StartPosition = FormStartPosition.CenterParent;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    LoadProfile(_profile); // reload updated info
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyAvatarImage()
        {
            try
            {
                _avatar.Image?.Dispose();
                _avatar.Image = null;

                if (!string.IsNullOrWhiteSpace(_profile.AvatarPath) && File.Exists(_profile.AvatarPath))
                {
                    using var fs = new FileStream(_profile.AvatarPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var img = Image.FromStream(fs);
                    _avatar.Image = new Bitmap(img);
                    _lblInitial.Visible = false;
                    return;
                }
            }
            catch
            {
                // ignore and fallback to initials
            }

            _lblInitial.Visible = true;
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
            var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text);
            return enumerator.MoveNext() ? enumerator.GetTextElement() : string.Empty;
        }

        private static string FormatUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return "@";

            // Remove @ if already present and add it back
            string clean = username.TrimStart('@').Trim();
            return string.IsNullOrEmpty(clean) ? "@" : "@" + clean;
        }

        private static void ClipCircle(PictureBox pb)
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, pb.Width, pb.Height);
            pb.Region = new Region(path);
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
                ForeColor = TG.TextPrimary,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                TabStop = false,
                Cursor = Cursors.Hand,
                UseCompatibleTextRendering = true,
                Padding = new Padding(8, 2, 8, 2),
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = TG.SidebarHover;
            b.FlatAppearance.MouseDownBackColor = TG.SidebarHover;
            return b;
        }

        private static Image? LoadIcon(string fileName)
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Resources", "Icons", "profile", fileName);
                if (!File.Exists(path)) return null;
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var img = Image.FromStream(fs);
                return new Bitmap(img);
            }
            catch
            {
                return null;
            }
        }

        private void LayoutDynamic()
        {
            int topMargin = 24;
            int centerX = ClientSize.Width / 2;

            _btnClose.Location = new Point(ClientSize.Width - _btnClose.Width - 14, 10);
            _btnEdit.Location = new Point(_btnClose.Left - _btnEdit.Width - 12, 10);

            _avatar.Location = new Point(centerX - _avatar.Width / 2, topMargin + 28);

            _lblName.Location = new Point(centerX - (_lblName.PreferredWidth / 2), _avatar.Bottom + 14);
            _lblStatus.Location = new Point(centerX - (_lblStatus.PreferredWidth / 2), _lblName.Bottom + 3);

            // Email section
            _lblEmail.Location = new Point(40, _lblStatus.Bottom + 48);
            if (_lblEmailType != null)
            {
                _lblEmailType.Location = new Point(_lblEmail.Left, _lblEmail.Bottom + 6);
            }

            // Username section
            int usernameTop = _lblEmailType.Bottom + 32;
            _lblUsername.Location = new Point(40, usernameTop);
            if (_lblUsernameType != null)
            {
                _lblUsernameType.Location = new Point(_lblUsername.Left, _lblUsername.Bottom + 6);
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
