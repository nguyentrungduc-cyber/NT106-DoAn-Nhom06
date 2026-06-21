using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Net.Mail;
using SecureChat.Client.Forms.Shared;
using SecureChat.Client.Services;
using SecureChat.Client.Security;
using SecureChat.DTOs;

namespace SecureChat.Client
{
    public class frmLoginRegister : Form
    {
        // ── Controls ──────────────────────────────────────────────────
        private Panel _pnlLogo;
        private Panel _pnlCard;
        private Label _lblAppName, _lblTagline;
        private Label _lblEmail, _lblPassword, _lblDisplayName, _lblConfirmPass;
        private TelegramTextBox _tbEmail, _tbPassword, _tbDisplayName, _tbConfirmPass;
        private TelegramButton _btnLogin, _btnRegister;
        private Label _lblError;
        private LinkLabel _lnkForgot;

        // ── State ─────────────────────────────────────────────────────
        private bool _isRegisterMode = false;
        private bool _suppressCloseOnChatClosed = false;

        public frmLoginRegister()
        {
            // Bật DoubleBuffered để giảm flickering khi resize hoặc đổi mode
            this.DoubleBuffered = true;
            InitializeComponent();
            ThemeRefreshHelper.Hook(this);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_suppressCloseOnChatClosed)
            {
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }

        private void InitializeComponent()
        {
            // Form settings
            Text = "SecureChat";
            Size = new Size(420, 560);
            MinimumSize = new Size(380, 500);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = TG.Blue;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            Font = TG.FontRegular(9.5f);

            // ── Logo Panel ───────────────────────────────────────────
            _pnlLogo = new Panel { BackColor = TG.Blue, Height = 160, Dock = DockStyle.Top };

            var lblIcon = new Label
            {
                Text = "✈",
                Font = new Font("Segoe UI Emoji", 42f),
                ForeColor = Color.White,
                Size = new Size(80, 80),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
            };
            _pnlLogo.Controls.Add(lblIcon);

            _lblAppName = new Label
            {
                Text = "SecureChat",
                Font = TG.FontTitle(16f),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 30,
            };
            _lblTagline = new Label
            {
                Text = "Nhắn tin an toàn và mã hóa đầu cuối",
                Font = TG.FontRegular(9f),
                ForeColor = Color.FromArgb(200, 235, 255),
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 20,
            };
            _pnlLogo.Controls.AddRange(new Control[] { _lblAppName, _lblTagline });

            _pnlLogo.Resize += (s, e) =>
            {
                lblIcon.Location = new Point((_pnlLogo.Width - 80) / 2, 20);
                _lblAppName.SetBounds(0, 108, _pnlLogo.Width, 30);
                _lblTagline.SetBounds(0, 136, _pnlLogo.Width, 20);
            };

            // ── Card trắng ──────────────────────────────────────────
            _pnlCard = new RoundedPanel
            {
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                Padding = new Padding(28, 24, 28, 20),
            };

            // Khởi tạo các ô nhập liệu
            _lblDisplayName = MakeFieldLabel("Họ và tên");
            _tbDisplayName = new TelegramTextBox { Height = 44 };
            _tbDisplayName.SetPlaceholder("Nguyễn Văn A");

            _lblEmail = MakeFieldLabel("Địa chỉ Email");
            _tbEmail = new TelegramTextBox { Height = 44 };
            _tbEmail.SetPlaceholder("example@email.com");

            _lblPassword = MakeFieldLabel("Mật khẩu");
            _tbPassword = new TelegramTextBox { Height = 44, PasswordCharValue = '●' };
            _tbPassword.SetPlaceholder("Nhập mật khẩu...");

            _lblConfirmPass = MakeFieldLabel("Xác nhận mật khẩu");
            _tbConfirmPass = new TelegramTextBox { Height = 44, PasswordCharValue = '●' };
            _tbConfirmPass.SetPlaceholder("Nhập lại mật khẩu...");

            _lblError = new Label
            {
                ForeColor = Color.FromArgb(0xE2, 0x4B, 0x4A),
                Font = TG.FontRegular(8.5f),
                Visible = false,
                Height = 20
            };

            _btnLogin = new TelegramButton
            {
                Text = "ĐĂNG NHẬP",
                Height = 46,
                Font = TG.FontSemiBold(10.5f),
                Radius = TG.RadiusSmall
            };
            _btnLogin.Click += BtnLogin_Click;

            _btnRegister = new TelegramButton
            {
                Text = "TẠO TÀI KHOẢN",
                Height = 46,
                Font = TG.FontSemiBold(10.5f),
                Radius = TG.RadiusSmall,
                IsOutlined = true,
                NormalColor = TG.Blue
            };
            _btnRegister.Click += BtnRegister_Click;

            _lnkForgot = new LinkLabel
            {
                Text = "Quên mật khẩu?",
                LinkColor = TG.Blue,
                Font = TG.FontRegular(9f),
                AutoSize = true
            };
            _lnkForgot.LinkClicked += LnkForgot_LinkClicked;

            _pnlCard.Controls.AddRange(new Control[] {
                _lblDisplayName, _tbDisplayName,
                _lblEmail, _tbEmail,
                _lblPassword, _tbPassword,
                _lblConfirmPass, _tbConfirmPass,
                _lblError, _btnLogin, _btnRegister, _lnkForgot
            });

            Controls.AddRange(new Control[] { _pnlCard, _pnlLogo });

            this.Resize += (s, e) => DoLayout();
            this.Load += (s, e) => SetLoginMode();
        }

        // add this new handler method in the frmLoginRegister class (near other event handlers)
        private void LnkForgot_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                using var dlg = new frmForgot();
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.ShowDialog(this);
                // Optionally, respond to dlg results (e.g., show a toast or switch mode)
            }
            catch (Exception ex)
            {
                frmError.ShowError(this, "Không thể mở Quên mật khẩu", ex.Message);
            }
        }

        private Label MakeFieldLabel(string text) => new Label
        {
            Text = text,
            Font = TG.FontRegular(8.5f),
            ForeColor = TG.Blue,
            Height = 18,
            BackColor = Color.Transparent
        };

        private void SetLoginMode()
        {
            _isRegisterMode = false;
            Text = "SecureChat – Đăng nhập";
            _lblDisplayName.Visible = _tbDisplayName.Visible = false;
            _lblConfirmPass.Visible = _tbConfirmPass.Visible = false;

            _btnLogin.Text = "ĐĂNG NHẬP";
            _btnRegister.Text = "TẠO TÀI KHOẢN MỚI";
            _lnkForgot.Visible = true;

            this.ClientSize = new Size(420, 560);
            DoLayout();
        }

        private void SetRegisterMode()
        {
            _isRegisterMode = true;
            Text = "SecureChat – Đăng ký";
            _lblDisplayName.Visible = _tbDisplayName.Visible = true;
            _lblConfirmPass.Visible = _tbConfirmPass.Visible = true;

            _btnLogin.Text = "TẠO TÀI KHOẢN";
            _btnRegister.Text = "← Đã có tài khoản";
            _lnkForgot.Visible = false;

            this.ClientSize = new Size(420, 720);
            DoLayout();
        }

        private void DoLayout()
        {
            int pad = 28, y = 20;
            int w = _pnlCard.Width - pad * 2;

            void Row(Control lbl, Control input)
            {
                if (!lbl.Visible) return;
                lbl.SetBounds(pad, y, w, 18); y += 22;
                input.SetBounds(pad, y, w, 44); y += 58;
            }

            Row(_lblDisplayName, _tbDisplayName);
            Row(_lblEmail, _tbEmail);
            Row(_lblPassword, _tbPassword);
            Row(_lblConfirmPass, _tbConfirmPass);

            _lblError.SetBounds(pad, y, w, 20); y += 25;
            _btnLogin.SetBounds(pad, y, w, 46); y += 54;
            _btnRegister.SetBounds(pad, y, w, 46); y += 54;
            _lnkForgot.Location = new Point((_pnlCard.Width - _lnkForgot.Width) / 2, y);
        }

        private async void BtnLogin_Click(object sender, EventArgs e)
        {
            SetLoading(true);
            HideError();

            try
            {
                if (_isRegisterMode) await HandleRegistration();
                else await HandleLogin();
            }
            catch (Exception ex)
            {
                frmError.ShowError(this, "Lỗi kết nối", ex.Message);
            }
            finally
            {
                SetLoading(false);
            }
        }

        private async Task HandleRegistration()
        {
            // Validation
            if (string.IsNullOrWhiteSpace(_tbEmail.Text) || string.IsNullOrWhiteSpace(_tbPassword.Text) ||
                string.IsNullOrWhiteSpace(_tbDisplayName.Text))
            {
                ShowError("Vui lòng nhập đầy đủ thông tin.");
                return;
            }
            if (!IsValidEmail(_tbEmail.Text))
            {
                ShowError("Định dạng email không hợp lệ.");
                return;
            }
            if (_tbPassword.Text != _tbConfirmPass.Text)
            {
                ShowError("Mật khẩu xác nhận không khớp.");
                return;
            }

            // Cryptography (chạy Task.Run để không lag UI)
            var keys = await Task.Run(() => RSAKeyManager.GenerateRSAKeys());
            string passwordForServer = _tbPassword.Text;

            var req = new RegisterRequest(
                // Username: _tbEmail.Text.Split('@')[0], // Lấy phần trước @ làm username tạm
                Username: GenerateRandomUsername(),
                DisplayName: _tbDisplayName.Text,
                Email: _tbEmail.Text,
                HashedPassword: passwordForServer,
                HashedBKey: "TBD",
                KeySalt: "TBD",
                PublicKey: keys.PublicKey
            );

            var (ok, _, err) = await ApiClient.Instance.PostAsync<RegisterRequest, object>("api/auth/register", req);
            if (ok)
            {
                frmError.ShowSuccess(this, "Đăng ký thành công", "Tài khoản của bạn đã sẵn sàng. Hãy đăng nhập để bắt đầu.");
                SetLoginMode();
            }
            else
            {
                frmError.ShowApi(this, err, "Không thể tạo tài khoản. Vui lòng thử lại.");
            }
        }

        private string GenerateRandomUsername()
        {
            // Cắt lấy 8 ký tự ngẫu nhiên từ GUID, kết hợp với tiền tố "user_"
            // Kết quả ra 13 ký tự (Ví dụ: "user_1a2b3c4d"), hoàn toàn hợp lệ với MaxLength(16)
            return "user_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private async Task HandleLogin()
        {
            if (string.IsNullOrWhiteSpace(_tbEmail.Text)) { ShowError("Vui lòng nhập Email."); return; }

            var req = new LoginRequest(_tbEmail.Text, _tbPassword.Text, Environment.MachineName);
            var (ok, resElem, err) = await ApiClient.Instance.PostAsync<LoginRequest, System.Text.Json.JsonElement>("api/auth/login", req);

            if (!ok)
            {
                frmError.ShowApi(this, err, "Đăng nhập thất bại. Vui lòng kiểm tra lại thông tin.");
                return;
            }

            try
            {
                if (resElem.ValueKind == System.Text.Json.JsonValueKind.Object && resElem.TryGetProperty("requires2FA", out var r) && r.GetBoolean())
                {
                    // open two-factor form; pass the identifier (username or email user entered)
                    using var dlg = new frmTwoFA(_tbEmail.Text);
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        // token should have been set by frmTwoFA
                        _suppressCloseOnChatClosed = true;
                        try
                        {
                            var oldChat = Application.OpenForms.OfType<frmMainChat>().FirstOrDefault();
                            oldChat?.Close();
                        }
                        finally
                        {
                            _suppressCloseOnChatClosed = false;
                        }

                        var chat = new frmMainChat();
                        chat.Show();
                        chat.FormClosed += (_, __) => { if (!_suppressCloseOnChatClosed) this.Close(); };
                        this.Hide();
                        return;
                    }
                    else
                    {
                        ShowError("Xác minh 2 bước không thành công hoặc bị hủy.");
                        return;
                    }
                }

                // Fallback: try to parse as AuthResponse (legacy path)
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var auth = System.Text.Json.JsonSerializer.Deserialize<SecureChat.DTOs.AuthResponse>(resElem.GetRawText(), options);
                if (auth != null)
                {
                    ApiClient.Instance.SetAccessToken(auth.AccessToken);

                    _suppressCloseOnChatClosed = true;
                    try
                    {
                        var oldChat = Application.OpenForms.OfType<frmMainChat>().FirstOrDefault();
                        oldChat?.Close();
                    }
                    finally
                    {
                        _suppressCloseOnChatClosed = false;
                    }

                    var chat = new frmMainChat();
                    chat.Show();
                    chat.FormClosed += (_, __) => { if (!_suppressCloseOnChatClosed) this.Close(); };
                    this.Hide();
                    return;
                }

                frmError.ShowError(this, "Đăng nhập thất bại", "Phản hồi từ máy chủ không hợp lệ.");
            }
            catch (Exception ex)
            {
                frmError.ShowError(this, "Lỗi xử lý phản hồi", ex.Message);
            }
        }

        private void BtnRegister_Click(object sender, EventArgs e)
        {
            if (_isRegisterMode) SetLoginMode();
            else SetRegisterMode();
        }

        private void SetLoading(bool loading)
        {
            _btnLogin.Enabled = _btnRegister.Enabled = !loading;
            _btnLogin.Text = loading ? "ĐANG XỬ LÝ..." : (_isRegisterMode ? "TẠO TÀI KHOẢN" : "ĐĂNG NHẬP");
        }

        private bool IsValidEmail(string email)
        {
            try { return new MailAddress(email).Address == email; }
            catch { return false; }
        }

        private void ShowError(string msg) { _lblError.Text = msg; _lblError.Visible = true; }
        private void HideError() => _lblError.Visible = false;
    }
}
