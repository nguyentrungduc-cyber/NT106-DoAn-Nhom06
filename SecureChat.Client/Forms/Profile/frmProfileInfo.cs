using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using SecureChat.Client.Models;
using SecureChat.Client.Services;
using System.IO;

namespace SecureChat.Client.Forms.Profile
{
    public partial class frmProfileInfo : Form
    {
        private void InitializeComponent() { /* built in code */ }

        // Colors read from TG at paint time

        private readonly ProfileModel _profile;

        private PictureBox _avatar = null!;
        private Label _lblInitial = null!;
        private Label _lblName = null!;
        private Label _lblStatus = null!;
        private TextBox _txtName = null!;
        private TextBox _txtEmail = null!;
        private TextBox _txtUsername = null!;
        private Button _btnAvatar = null!;
        private DateTimePicker _dtBirthday = null!;
        private Label _lblError = null!;
        private Button _btnBack = null!;
        private Button _btnClose = null!;

        public frmProfileInfo(ProfileModel profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            InitializeComponent();
            BuildUI();
            ThemeRefreshHelper.Hook(this);
            LoadProfile(profile);
            Resize += (_, __) => LayoutDynamic();
            LayoutDynamic();
        }

        private void BuildUI()
        {
            Text = "Info";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            HelpButton = false;
            ControlBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(540, 720);
            BackColor = TG.WindowBg;
            Font = new Font("Segoe UI", 10f);
            DoubleBuffered = true;

            _btnBack = FlatIconButton("<< Back");
            _btnBack.Image = null;
            _btnBack.TextImageRelation = TextImageRelation.Overlay;
            _btnBack.Click += (_, __) => Close();

            _btnClose = FlatIconButton("X");
            _btnClose.Image = null;
            _btnClose.TextImageRelation = TextImageRelation.Overlay;
            _btnClose.Click += (_, __) => Close();

            _avatar = new PictureBox
            {
                Size = new Size(120, 120),
                BackColor = TG.GetAvatarColor(_profile.FullName),
                SizeMode = PictureBoxSizeMode.Zoom,
            };
            _avatar.Paint += (_, __) => ClipCircle(_avatar);

            _lblInitial = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 30f),
                BackColor = Color.Transparent,
            };
            _avatar.Controls.Add(_lblInitial);

            _btnAvatar = new Button
            {
                Text = "Change photo",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TG.CAccent,
                Font = new Font("Segoe UI", 9.5f),
                TabStop = false
            };
            _btnAvatar.FlatAppearance.BorderSize = 0;
            _btnAvatar.Click += (_, __) => SelectAvatarPhoto();

            _lblName = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 13f),
                ForeColor = TG.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point((ClientSize.Width / 2) - 60, 170),
            };

            _lblStatus = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = TG.CAccent,
                BackColor = Color.Transparent,
                Location = new Point((ClientSize.Width / 2) - 20, 195),
            };

            int fieldTop = 240;
            var nameField = InputField("Name", fieldTop);
            fieldTop += 74;
            var phoneField = InputField("Email", fieldTop);
            fieldTop += 74;
            var userField = InputField("t.me/username", fieldTop);
            fieldTop += 74;

            _dtBirthday = new DateTimePicker
            {
                Location = new Point(26, fieldTop + 26),
                Size = new Size(ClientSize.Width - 52, 32),
                Font = new Font("Segoe UI", 10.5f),
                CalendarForeColor = TG.TextPrimary,
                CalendarMonthBackground = TG.WindowBg,
                CalendarTitleBackColor = TG.WindowBg,
                CalendarTitleForeColor = TG.TextPrimary,
                CalendarTrailingForeColor = TG.TextHint,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd/MM/yyyy",
                ShowUpDown = false,
            };
            var lblBirth = new Label
            {
                Text = "Birthday",
                ForeColor = TG.TextPrimary,
                Font = new Font("Segoe UI Semibold", 10.5f),
                AutoSize = true,
                Location = new Point(26, fieldTop),
                BackColor = Color.Transparent,
            };
            fieldTop += 70;

            _lblError = new Label
            {
                ForeColor = TG.Blue,
                Font = new Font("Segoe UI", 9.5f),
                AutoSize = false,
                Size = new Size(ClientSize.Width - 52, 40),
                Location = new Point(26, fieldTop),
                BackColor = Color.Transparent,
            };
            fieldTop += 50;

            var btnSave = new Button
            {
                Text = "Save",
                Size = new Size(110, 34),
                Location = new Point(ClientSize.Width - 26 - 110, fieldTop),
                FlatStyle = FlatStyle.Flat,
                BackColor = TG.Blue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10.5f),
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (_, __) => SaveProfile();

            Controls.AddRange(new Control[]
            {
                _btnBack, _btnClose, _avatar, _btnAvatar, _lblName, _lblStatus,
                nameField.Label, nameField.TextBox, nameField.Underline,
                phoneField.Label, phoneField.TextBox, phoneField.Underline,
                userField.Label, userField.TextBox, userField.Underline,
                lblBirth, _dtBirthday, _lblError, btnSave,
            });

            _txtName = nameField.TextBox;
            _txtEmail = phoneField.TextBox;
            _txtUsername = userField.TextBox;
        }

        private (Label Label, TextBox TextBox, Panel Underline) InputField(string label, int top)
        {
            var lbl = new Label
            {
                Text = label,
                ForeColor = TG.TextPrimary,
                Font = new Font("Segoe UI Semibold", 10.5f),
                AutoSize = true,
                Location = new Point(26, top),
                BackColor = Color.Transparent,
            };

            var txt = new TextBox
            {
                Location = new Point(26, top + 24),
                Size = new Size(ClientSize.Width - 52, 28),
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = TG.TextPrimary,
                BackColor = TG.WindowBg,
                BorderStyle = BorderStyle.None,
            };

            var underline = new Panel
            {
                Location = new Point(txt.Left, txt.Bottom + 2),
                Size = new Size(txt.Width, 1),
                BackColor = TG.Divider,
            };

            txt.GotFocus += (_, __) => underline.BackColor = TG.CAccent;
            txt.LostFocus += (_, __) => underline.BackColor = TG.Divider;

            return (lbl, txt, underline);
        }

        private void LoadProfile(ProfileModel profile)
        {
            _txtName.Text = profile.FullName;
            _txtEmail.Text = profile.Email;
            _txtUsername.Text = profile.Username;
            if (profile.Birthday.HasValue)
                _dtBirthday.Value = profile.Birthday.Value;
            else
                _dtBirthday.Value = DateTime.Today;

            _lblName.Text = profile.FullName;
            _lblStatus.Text = profile.StatusText;
            _lblInitial.Text = GetInitials(profile.FullName);
            _avatar.BackColor = TG.GetAvatarColor(profile.FullName);
            ApplyAvatarImage();
            LayoutDynamic();
        }

        private void LayoutDynamic()
        {
            int centerX = ClientSize.Width / 2;

            _btnClose.Location = new Point(ClientSize.Width - _btnClose.Width - 14, 12);
            _btnBack.Location = new Point(14, 12);

            _avatar.Location = new Point(centerX - _avatar.Width / 2, 52);
            _btnAvatar.Location = new Point(centerX - _btnAvatar.Width / 2, _avatar.Bottom + 4);
            _lblName.Location = new Point(centerX - (_lblName.PreferredWidth / 2), _btnAvatar.Bottom + 6);
            _lblStatus.Location = new Point(centerX - (_lblStatus.PreferredWidth / 2), _lblName.Bottom + 2);
        }

        private async void SaveProfile()
        {
            try
            {
                _lblError.Text = string.Empty;
                var name = _txtName.Text.Trim();
                var username = _txtUsername.Text.Trim().TrimStart('@');
                var email = _txtEmail.Text.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    throw new InvalidOperationException("Name is required.");
                if (!string.IsNullOrEmpty(username) && !Regex.IsMatch(username, "^[a-zA-Z0-9_]{5,32}$"))
                    throw new InvalidOperationException("Username must be 5-32 chars [a-zA-Z0-9_].");

                var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();

                // Upload avatar nếu đã chọn ảnh mới
                string? avatarUrl = null;
                if (!string.IsNullOrWhiteSpace(_profile.AvatarPath) && File.Exists(_profile.AvatarPath))
                {
                    try
                    {
                        using var fs = new FileStream(_profile.AvatarPath, FileMode.Open, FileAccess.Read);
                        using var ms = new MemoryStream();
                        await fs.CopyToAsync(ms);
                        ms.Position = 0;

                        using var formContent = new MultipartFormDataContent();
                        formContent.Add(new ByteArrayContent(ms.ToArray()), "File", "avatar.jpg");
                        var uploadRes = await http.PostAsync("api/files/upload", formContent);
                        if (uploadRes.IsSuccessStatusCode)
                        {
                            var uploadJson = await uploadRes.Content.ReadAsStringAsync();
                            using var uploadDoc = System.Text.Json.JsonDocument.Parse(uploadJson);
                            avatarUrl = uploadDoc.RootElement.GetProperty("url").GetString();
                        }
                    }
                    catch { }
                }

                // Update avatar trên server nếu có
                if (avatarUrl != null)
                {
                    var avatarPayload = new { avatarUrl };
                    var avatarJson = System.Text.Json.JsonSerializer.Serialize(avatarPayload);
                    var avatarContent = new StringContent(avatarJson, System.Text.Encoding.UTF8, "application/json");
                    await http.PatchAsync("api/users/me/avatar", avatarContent);
                }

                // Update profile (name, email, username)
                var profilePayload = new { displayName = name, email, username };
                var profileJson = System.Text.Json.JsonSerializer.Serialize(profilePayload);
                var profileContent = new StringContent(profileJson, System.Text.Encoding.UTF8, "application/json");
                var res = await http.PatchAsync("api/users/me", profileContent);
                if (!res.IsSuccessStatusCode)
                {
                    var err = await res.Content.ReadAsStringAsync();
                    if ((int)res.StatusCode == 409)
                        throw new InvalidOperationException("Username đã được sử dụng. Vui lòng chọn username khác.");
                    throw new InvalidOperationException($"Lỗi lưu hồ sơ: {err}");
                }

                _profile.FullName = name;
                _profile.Email = email;
                _profile.Username = username;
                _profile.Birthday = _dtBirthday.Value;
                if (avatarUrl != null)
                {
                    _profile.AvatarUrl = avatarUrl;
                    _profile.AvatarPath = DownloadAndCacheAvatar(avatarUrl) ?? avatarUrl;
                }

                _lblInitial.Text = GetInitials(name);
                _avatar.BackColor = TG.GetAvatarColor(name);
                ApplyAvatarImage();

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _lblError.Text = ex.Message;
            }
        }

        private void SelectAvatarPhoto()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select avatar image",
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
                Multiselect = false
            };

            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                using var source = new Bitmap(ofd.FileName);
                using var cropper = new frmAvatarCropper(source);
                if (cropper.ShowDialog(this) != DialogResult.OK || cropper.CroppedImage == null) return;

                using var cropped = new Bitmap(cropper.CroppedImage);
                var savedPath = SaveAvatarToLocal(cropped);
                if (string.IsNullOrWhiteSpace(savedPath)) return;

                _profile.AvatarPath = savedPath;
                ApplyAvatarImage();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string SaveAvatarToLocal(Image image)
        {
            try
            {
                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecureChat", "Avatars");
                Directory.CreateDirectory(root);
                var path = Path.Combine(root, $"avatar_{Guid.NewGuid():N}.png");
                image.Save(path, ImageFormat.Png);
                return path;
            }
            catch
            {
                return string.Empty;
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
                // ignore and fallback to initials avatar
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

        private static string? DownloadAndCacheAvatar(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                var http = Services.ApiClient.Instance.GetHttpClient();
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
                img.Save(cachePath, ImageFormat.Png);
                return cachePath;
            }
            catch
            {
                return null;
            }
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
                Padding = new Padding(6, 2, 6, 2),
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, 255, 255, 255);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 255, 255, 255);
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

    internal sealed class frmAvatarCropper : Form
    {
        private const int MinSelectionSide = 56;

        private readonly Bitmap _source;
        private readonly Panel _canvas;
        private readonly TrackBar _zoomBar;
        private readonly Label _lblZoom;

        private Rectangle _imageRect;
        private Rectangle _selection;
        private bool _movingSelection;
        private Point _moveOffset;
        private float _zoom = 1f;

        public Bitmap? CroppedImage { get; private set; }

        public frmAvatarCropper(Bitmap source)
        {
            _source = new Bitmap(source);

            Text = "Crop avatar";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(760, 660);
            BackColor = TG.WindowBg;

            var lblHint = new Label
            {
                Text = "Drag the square to choose area, use zoom for precision.",
                AutoSize = true,
                ForeColor = TG.TextPrimary,
                Font = new Font("Segoe UI", 10f),
                Location = new Point(16, 12)
            };

            _canvas = new Panel
            {
                Location = new Point(16, 40),
                Size = new Size(728, 500),
                BackColor = TG.SidebarHover,
                BorderStyle = BorderStyle.FixedSingle
            };

            _lblZoom = new Label
            {
                Text = "Zoom: 100%",
                AutoSize = true,
                ForeColor = TG.TextPrimary,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(16, 552)
            };

            _zoomBar = new TrackBar
            {
                Location = new Point(100, 548),
                Size = new Size(300, 34),
                Minimum = 100,
                Maximum = 300,
                Value = 100,
                TickStyle = TickStyle.None,
                SmallChange = 5,
                LargeChange = 10
            };
            _zoomBar.Scroll += (_, __) =>
            {
                _zoom = _zoomBar.Value / 100f;
                _lblZoom.Text = $"Zoom: {_zoomBar.Value}%";
                UpdateSelectionForZoom();
                _canvas.Invalidate();
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(96, 34),
                Location = new Point(ClientSize.Width - 210, 614),
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(0xD6, 0xDE, 0xE7);

            var btnApply = new Button
            {
                Text = "Apply",
                Size = new Size(96, 34),
                Location = new Point(ClientSize.Width - 108, 614),
                FlatStyle = FlatStyle.Flat,
                BackColor = TG.CAccent,
                ForeColor = Color.White
            };
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.Click += (_, __) => ApplyCrop();

            _canvas.Paint += Canvas_Paint;
            _canvas.MouseDown += Canvas_MouseDown;
            _canvas.MouseMove += Canvas_MouseMove;
            _canvas.MouseUp += (_, __) => _movingSelection = false;
            MouseWheel += Cropper_MouseWheel;

            _canvas.Resize += (_, __) =>
            {
                RecalculateImageRect();
                UpdateSelectionForZoom();
                _canvas.Invalidate();
            };

            Controls.Add(lblHint);
            Controls.Add(_canvas);
            Controls.Add(_lblZoom);
            Controls.Add(_zoomBar);
            Controls.Add(btnCancel);
            Controls.Add(btnApply);

            RecalculateImageRect();
            UpdateSelectionForZoom();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _source.Dispose();
            }

            base.Dispose(disposing);
        }

        private void Cropper_MouseWheel(object? sender, MouseEventArgs e)
        {
            var p = PointToClient(Cursor.Position);
            if (!_canvas.Bounds.Contains(p)) return;

            int step = e.Delta > 0 ? 10 : -10;
            int value = Math.Max(_zoomBar.Minimum, Math.Min(_zoomBar.Maximum, _zoomBar.Value + step));
            if (value == _zoomBar.Value) return;

            _zoomBar.Value = value;
            _zoom = value / 100f;
            _lblZoom.Text = $"Zoom: {value}%";
            UpdateSelectionForZoom();
            _canvas.Invalidate();
        }

        private void RecalculateImageRect()
        {
            if (_source.Width <= 0 || _source.Height <= 0 || _canvas.Width <= 0 || _canvas.Height <= 0)
            {
                _imageRect = Rectangle.Empty;
                return;
            }

            float ratio = Math.Min((float)_canvas.Width / _source.Width, (float)_canvas.Height / _source.Height);
            int drawW = Math.Max(1, (int)Math.Round(_source.Width * ratio));
            int drawH = Math.Max(1, (int)Math.Round(_source.Height * ratio));
            int x = (_canvas.Width - drawW) / 2;
            int y = (_canvas.Height - drawH) / 2;
            _imageRect = new Rectangle(x, y, drawW, drawH);
        }

        private void UpdateSelectionForZoom()
        {
            if (_imageRect.Width == 0 || _imageRect.Height == 0) return;

            var center = _selection.Width > 0
                ? new Point(_selection.X + _selection.Width / 2, _selection.Y + _selection.Height / 2)
                : new Point(_imageRect.X + _imageRect.Width / 2, _imageRect.Y + _imageRect.Height / 2);

            int maxSide = Math.Max(MinSelectionSide, Math.Min(_imageRect.Width, _imageRect.Height) - 8);
            int side = (int)Math.Round(maxSide / _zoom);
            side = Math.Max(MinSelectionSide, Math.Min(maxSide, side));

            var rect = new Rectangle(center.X - side / 2, center.Y - side / 2, side, side);
            _selection = ClampToImage(rect);
        }

        private void Canvas_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (_imageRect.Width <= 0 || _imageRect.Height <= 0) return;

            e.Graphics.DrawImage(_source, _imageRect);

            using var overlayBrush = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
            var top = new Rectangle(0, 0, _canvas.Width, Math.Max(0, _selection.Top));
            var left = new Rectangle(0, _selection.Top, Math.Max(0, _selection.Left), Math.Max(0, _selection.Height));
            var right = new Rectangle(_selection.Right, _selection.Top, Math.Max(0, _canvas.Width - _selection.Right), Math.Max(0, _selection.Height));
            var bottom = new Rectangle(0, _selection.Bottom, _canvas.Width, Math.Max(0, _canvas.Height - _selection.Bottom));
            e.Graphics.FillRectangle(overlayBrush, top);
            e.Graphics.FillRectangle(overlayBrush, left);
            e.Graphics.FillRectangle(overlayBrush, right);
            e.Graphics.FillRectangle(overlayBrush, bottom);

            using var pen = new Pen(Color.White, 2f);
            e.Graphics.DrawRectangle(pen, _selection);
        }

        private void Canvas_MouseDown(object? sender, MouseEventArgs e)
        {
            if (!_selection.Contains(e.Location)) return;
            _movingSelection = true;
            _moveOffset = new Point(e.X - _selection.X, e.Y - _selection.Y);
        }

        private void Canvas_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_movingSelection) return;

            var moved = new Rectangle(e.X - _moveOffset.X, e.Y - _moveOffset.Y, _selection.Width, _selection.Height);
            _selection = ClampToImage(moved);
            _canvas.Invalidate();
        }

        private Rectangle ClampToImage(Rectangle rect)
        {
            if (_imageRect.Width == 0 || _imageRect.Height == 0 || rect.Width <= 0) return Rectangle.Empty;

            int side = Math.Min(rect.Width, Math.Min(_imageRect.Width, _imageRect.Height));
            side = Math.Max(MinSelectionSide, side);

            int x = rect.X;
            int y = rect.Y;
            if (x < _imageRect.Left) x = _imageRect.Left;
            if (y < _imageRect.Top) y = _imageRect.Top;
            if (x + side > _imageRect.Right) x = _imageRect.Right - side;
            if (y + side > _imageRect.Bottom) y = _imageRect.Bottom - side;

            return new Rectangle(x, y, side, side);
        }

        private void ApplyCrop()
        {
            if (_selection.Width <= 0 || _selection.Height <= 0 || _imageRect.Width <= 0 || _imageRect.Height <= 0)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            float scaleX = (float)_source.Width / _imageRect.Width;
            float scaleY = (float)_source.Height / _imageRect.Height;

            int sx = (int)Math.Round((_selection.X - _imageRect.X) * scaleX);
            int sy = (int)Math.Round((_selection.Y - _imageRect.Y) * scaleY);
            int sw = (int)Math.Round(_selection.Width * scaleX);
            int sh = (int)Math.Round(_selection.Height * scaleY);

            sx = Math.Max(0, Math.Min(sx, _source.Width - 1));
            sy = Math.Max(0, Math.Min(sy, _source.Height - 1));
            sw = Math.Max(1, Math.Min(sw, _source.Width - sx));
            sh = Math.Max(1, Math.Min(sh, _source.Height - sy));

            int finalSide = Math.Min(sw, sh);
            var srcRect = new Rectangle(sx, sy, finalSide, finalSide);

            var target = new Bitmap(512, 512);
            using (var g = Graphics.FromImage(target))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(_source, new Rectangle(0, 0, 512, 512), srcRect, GraphicsUnit.Pixel);
            }

            CroppedImage = target;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
        private void OnThemeChanged()
        {
            if (InvokeRequired) { Invoke(new Action(OnThemeChanged)); return; }
            BackColor = TG.WindowBg;
            this.Invalidate(true);
            foreach (Control c in this.Controls)
                ApplyThemeRecursive(c);
        }

        private static void ApplyThemeRecursive(Control c)
        {
            if (c.BackColor != Color.Transparent &&
                c.BackColor != TG.Blue &&
                c.BackColor != TG.SidebarActive)
                c.BackColor = TG.WindowBg;
            if (c.Tag is string t && t == "secondary-text")
                c.ForeColor = TG.TextSecondary;
            c.Invalidate();
            foreach (Control child in c.Controls)
                ApplyThemeRecursive(child);
        }

    }
}
