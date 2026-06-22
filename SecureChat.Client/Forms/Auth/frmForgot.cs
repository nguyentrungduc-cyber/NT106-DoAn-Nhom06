using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

using SecureChat.Client.Forms.Shared;
using SecureChat.Client.Helpers;
using SecureChat.Client.Services;
using SecureChat.Client.Services.Api;

namespace SecureChat.Client
{

    /// Màn hình quên mật khẩu - 3 bước: Email → OTP → Mật khẩu mới

    public class frmForgot : Form
    {
        private readonly IAuthService _authService;
        private int _step = 1; // 1=email, 2=otp, 3=newpass
        private string? _resetToken;

        private Panel _pnlMain; // thêm vào chỗ khai báo field

        // Mảng 3 chấm tròn hiển thị tiến trình (Step 1 → 2 → 3). Mỗi chấm là một Panel tự vẽ.
        private Panel[] _stepDots = new Panel[3];

        // Tiêu đề lớn và mô tả nhỏ thay đổi theo từng bước.
        private Label _lblStepTitle, _lblStepDesc;

        // Step 1
        private TelegramTextBox _tbEmail; // _tbEmail: ô nhập địa chỉ email
        private Label _lblEmailHint; // _lblEmailHint: thông báo màu xanh lá "Link có hiệu lực 15 phút…" (ẩn ban đầu)

        // Step 2
        private Panel _pnlOtp; // panel chứa 6 ô nhập OTP
        private TextBox[] _otpBoxes = new TextBox[6]; // mảng 6 ô TextBox, mỗi ô 1 chữ số
        private System.Windows.Forms.Timer _timer; // đồng hồ đếm ngược 60 giây
        private int _countdown = 60; // giá trị đếm ngược hiện tại
        private Label _lblCountdown; // nhãn hiển thị "Gửi lại sau (Xs)"

        // Step 3: Hai ô nhập mật khẩu mới và xác nhận mật khẩu.
        private TelegramTextBox _tbNewPass, _tbConfirmPass;

        // Common
        private TelegramButton _btnNext, _btnBack;
        private Label _lblError;
        private Panel _pnlContent;
        private TelegramHeader _header;
        private bool _isBusy;

        public frmForgot()
            : this(new AuthService(ApiClient.Create(), message => Debug.WriteLine(message)))
        {
        }

        public frmForgot(IAuthService authService)
        {
            _authService = authService;
            InitializeComponent();
            ThemeRefreshHelper.Hook(this);
            ShowStep(1);
        }

        private void InitializeComponent()
        {
            Text = "Đặt lại mật khẩu"; // tên form
            Size = new Size(400, 520);
            MinimumSize = new Size(380, 490);
            // StartPosition = FormStartPosition.CenterParent;
            //  FormBorderStyle = FormBorderStyle.FixedSingle;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            BackColor = TG.WindowBg;
            Font = TG.FontRegular(9.5f);

            // Header
            _header = new TelegramHeader { Title = "Đặt lại mật khẩu" };
            _header.ShowBack = true; // Hiển thị nút Back (mũi tên quay lại)
            _header.BackClicked += (s, e) =>
            {
                if (_step > 1) ShowStep(_step - 1); // nếu đang ở bước 2 hoặc 3 thì quay về bước trước
                else Close(); // nếu đang ở bước 1 thì đóng form
            };
            Controls.Add(_header); // Thêm header vào form

            // Step indicator
            // Panel nằm ngang chứa 3 chấm tròn, cao 48px
            var pnlSteps = new Panel { Height = 48, BackColor = TG.WindowBg };
            for (int i = 0; i < 3; i++) // Vòng lặp tạo 3 chấm
            {
                int idx = i; // tránh bug closure
                // Mỗi chấm là một Panel 28×28px, trong suốt (để tự vẽ hình tròn bên trong).
                var dot = new Panel
                {
                    Size = new Size(28, 28),
                    BackColor = Color.Transparent
                };
                // Thay vì dùng giao diện mặc định, mình can thiệp vào quá trình vẽ của  dot.
                dot.Paint += (s, e) =>
                {
                    // e.Graphics: Là "cây bút vẽ" chính để thao tác trên bề mặt của Control.
                    // SmoothingMode.AntiAlias: Bật chế độ khử răng cưa để hình tròn trông mượt mà, không bị vỡ nét ở rìa.
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    bool active = _step == idx + 1; //  (Đang thực hiện): Nếu bước hiện tại khớp với vị trí của chấm này.
                    bool done = _step > idx + 1; // (Đã hoàn thành): Nếu bước hiện tại đã vượt qua vị trí của chấm này.

                    // Nếu đã xong hoặc đang làm: Tô màu xanh(TG.Blue).
                    // Nếu chưa tới: Tô màu xám chia cắt(TG.Divider).
                    Color bg = done ? TG.Blue : active ? TG.Blue : TG.Divider;

                    // Vẽ hình tròn đặc màu bg.
                    // Vẽ tại tọa độ (0,0) với kích thước 27x27 pixels.
                    e.Graphics.FillEllipse(new SolidBrush(bg), 0, 0, 27, 27);

                    // Nếu đã xong(done): Hiển thị dấu tích "✓".
                    // Nếu chưa xong: Hiển thị số thứ tự (idx + 1): (1, 2, 3).
                    string txt = done ? "✓" : (idx + 1).ToString();

                    // Vẽ chữ màu trắng, canh giữa cả ngang lẫn dọc trong ô 28×28.
                    using var sf = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center };
                    e.Graphics.DrawString(txt, TG.FontSemiBold(9f), System.Drawing.Brushes.White, new Rectangle(0, 0, 28, 28), sf);
                };
                // Lưu vào mảng và thêm vào panel.
                _stepDots[i] = dot;
                pnlSteps.Controls.Add(dot);
            }

            // Vẽ đường nối giữa các chấm + đổi màu
            pnlSteps.Paint += (s, e) =>
            {
                int lineY = 24 + pnlSteps.Padding.Top; // tọa độ y để vẽ ngang giữa Dot = 
                int[] xs = GetDotXs(pnlSteps.Width); // trả về một mảng tọa độ X (ngang) của 3 chấm tròn

                // Nếu _step > 1(đã xong bước 1), đường kẻ màu xanh(TG.Blue), ngược lại màu xám(TG.Divider)
                // Bắt đầu từ mép phải của chấm 1 (xs[0] + 28) và kéo dài đến mép trái của chấm 2 (xs[1])
                e.Graphics.DrawLine(new System.Drawing.Pen(_step > 1 ? TG.Blue : TG.Divider, 2), xs[0] + 28, lineY, xs[1], lineY);

                e.Graphics.DrawLine(new System.Drawing.Pen(_step > 2 ? TG.Blue : TG.Divider, 2), xs[1] + 28, lineY, xs[2], lineY);
            };

            // Khi panel thay đổi kích thước, tính lại vị trí 3 chấm và vẽ lại.
            pnlSteps.Resize += (s, e) =>
            {
                int[] xs = GetDotXs(pnlSteps.Width);
                for (int i = 0; i < 3; i++) _stepDots[i].Location = new Point(xs[i], 10);
                pnlSteps.Invalidate(); // Invalidate() sẽ kích hoạt lại sự kiện Paint
            };

            // Step labels
            _lblStepTitle = new Label // lưu tiêu đề lớn của từng bước
            {
                AutoSize = false,
                Height = 26,
                Font = TG.FontSemiBold(12f),
                ForeColor = TG.TextPrimary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            _lblStepDesc = new Label //  lưu mô tả nhỏ bên dưới tiêu đề
            {
                AutoSize = false,
                Height = 44,
                Font = TG.FontRegular(9f),
                ForeColor = TG.TextSecondary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.TopCenter,
            };

            // Content panel
            _pnlContent = new Panel { BackColor = Color.Transparent };

            // ── Step 1: Email ─────────────────────────
            var lblEmail = new Label { Text = "Địa chỉ email đã đăng ký:", Font = TG.FontRegular(8.5f), ForeColor = TG.Blue, AutoSize = false, Height = 22, BackColor = Color.Transparent };

            // Ô nhập email cao 44px, có placeholder gợi ý.
            _tbEmail = new TelegramTextBox { Height = 44 };
            _tbEmail.SetPlaceholder("user@example.com");

            _lblEmailHint = new Label
            {
                Text = "📧  Link đặt lại có hiệu lực trong 15 phút. Kiểm tra cả thư mục spam.",
                Font = TG.FontRegular(8.5f),
                ForeColor = Color.FromArgb(0x2E, 0x7D, 0x32), // chữ xanh lá đậm
                BackColor = Color.FromArgb(0xE8, 0xF5, 0xE9), // xanh lá nhạt (nền)
                AutoSize = false,
                Height = 52,
                Padding = new Padding(10, 8, 10, 0),
                Visible = false,
            };

            // ── Step 2: OTP ───────────────────────────
            _pnlOtp = new Panel { Height = 62, BackColor = Color.Transparent };
            for (int i = 0; i < 6; i++)
            {
                int idx = i;
                var box = new TextBox
                {
                    MaxLength = 1, // giới hạn mỗi ô chỉ nhận đúng 1 ký tự.
                    Font = TG.FontTitle(16f),
                    ForeColor = TG.Blue,
                    TextAlign = HorizontalAlignment.Center,
                    BorderStyle = BorderStyle.FixedSingle,
                    Size = new Size(42, 50),
                    BackColor = TG.InputBg,
                };

                box.KeyPress += (s, e) =>
                {
                    if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                    {
                        e.Handled = true;
                    }
                };

                // Khi gõ 1 ký tự vào ô hiện tại và chưa phải ô cuối(idx < 5) → tự động chuyển focus sang ô kế tiếp.
                box.TextChanged += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(box.Text) && idx < 5) _otpBoxes[idx + 1].Focus();
                };

                // Khi nhấn Backspace mà ô đang trống và không phải ô đầu → quay lại ô trước (xóa lùi tự nhiên)
                box.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Back && string.IsNullOrEmpty(box.Text) && idx > 0) _otpBoxes[idx - 1].Focus();
                };

                _pnlOtp.Controls.Add(box);
                _otpBoxes[i] = box;
            }
            Action layoutOtp = () =>
            {
                if (_pnlOtp.Width == 0) return;
                int total = 6 * 42 + 5 * 6, startX = (_pnlOtp.Width - total) / 2;
                for (int i = 0; i < 6; i++) _otpBoxes[i].Location = new Point(startX + i * 48, 4);
                // total = tổng chiều rộng: 6 ô × 42px + 5 khoảng cách × 6px = 282px
                // startX = điểm bắt đầu để canh giữa trong panel
                // Mỗi ô cách nhau 48px (42px ô + 6px gap), dịch xuống 4px từ trên
            };

            _pnlOtp.Resize += (s, e) => layoutOtp();
            _pnlOtp.VisibleChanged += (s, e) => { if (_pnlOtp.Visible) layoutOtp(); };

            _lblCountdown = new Label
            {
                AutoSize = false,
                Height = 22,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = TG.FontRegular(8.5f),
                ForeColor = TG.TextSecondary,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
            };
            _lblCountdown.Click += async (s, e) => await HandleResendOtpAsync();
            _timer = new System.Windows.Forms.Timer { Interval = 1000 }; // Timer tick mỗi 1000ms = 1 giây.

            // Mỗi tick: giảm đếm ngược 1, cập nhật label, dừng timer khi về 0.
            _timer.Tick += (s, e) => { _countdown--; UpdateCountdown(); if (_countdown <= 0) _timer.Stop(); };


            // ── Step 3: New Password ──────────────────
            var lblNew = new Label { Text = "Mật khẩu mới:", Font = TG.FontRegular(8.5f), ForeColor = TG.Blue, AutoSize = false, Height = 22, BackColor = Color.Transparent };
            _tbNewPass = new TelegramTextBox { Height = 44 };
            _tbNewPass.SetPlaceholder("Ít nhất 8 ký tự...");
            _tbNewPass.PasswordCharValue = '●';

            var lblConf = new Label { Text = "Xác nhận mật khẩu mới:", Font = TG.FontRegular(8.5f), ForeColor = TG.Blue, AutoSize = false, Height = 22, BackColor = Color.Transparent };
            _tbConfirmPass = new TelegramTextBox { Height = 44 };
            _tbConfirmPass.SetPlaceholder("Nhập lại mật khẩu...");
            _tbConfirmPass.PasswordCharValue = '●';

            // Error
            _lblError = new Label
            {
                AutoSize = false,
                Height = 20,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(0xE2, 0x4B, 0x4A),
                Font = TG.FontRegular(8.5f),
                BackColor = Color.Transparent,
                Visible = false,
            };

            // Buttons
            _btnNext = new TelegramButton { Text = "TIẾP THEO", Height = 46, Font = TG.FontSemiBold(10.5f), Radius = TG.RadiusSmall };
            _btnNext.Click += BtnNext_ClickAsync;

            _pnlContent.Controls.AddRange(new Control[] {
                // step1
                lblEmail, _tbEmail, _lblEmailHint,
                // step2
                _pnlOtp, _lblCountdown,
                // step3
                lblNew, _tbNewPass, lblConf, _tbConfirmPass,
            });

            // Panel bọc ngoài fill toàn form, padding 28px hai bên.Mỗi lần resize → tính lại layout.
            _pnlMain = new Panel { BackColor = TG.WindowBg, Padding = new Padding(28, 12, 28, 20) };
            _pnlMain.Controls.AddRange(new Control[] { _lblStepTitle, _lblStepDesc, pnlSteps, _pnlContent, _lblError, _btnNext });
            _pnlMain.Dock = DockStyle.Fill;
            _pnlMain.Resize += (s, e) => DoLayout(_pnlMain);

            // Thêm panel chính và header vào form. Header thêm sau nên nằm trên cùng (Z-order cao hơn).
            Controls.AddRange(new Control[] { _pnlMain, _header });
        }

        // Tính vị trí 3 chấm
        // Canh 3 chấm đối xứng quanh tâm panel. Khoảng cách giữa các chấm = 46px (28px chấm + 18px đường nối).
        private int[] GetDotXs(int panelWidth)
        {
            int center = panelWidth / 2;
            return new[] { center - 60, center - 14, center + 32 };
        }

        // Chuyển bước
        private void ShowStep(int step)
        {
            _step = step;
            HideError();

            // Duyệt qua tất cả các chấm tròn(dot) và ra lệnh cho chúng vẽ lại.
            // Khi bạn thay đổi biến _step(ví dụ từ bước 1 sang bước 2), các chấm tròn cần biết chúng phải đổi từ màu xám sang xanh, hoặc từ số "2" thành dấu "✓".
            // Gọi Invalidate() sẽ kích hoạt sự kiện.Paint của từng chấm mà bạn đã viết trước đó.
            foreach (var d in _stepDots) d.Invalidate();
            if (_stepDots[0].Parent != null) _stepDots[0].Parent.Invalidate();
            // Invalidate() báo cho Windows vẽ lại chấm tròn và đường nối (vì màu sắc thay đổi theo _step).
            // Việc kiểm tra != null giúp code không bị văng lỗi (Crash) nếu chẳng may các chấm tròn này chưa được add vào Panel nào đó tại thời điểm chạy.

            switch (step)
            {
                case 1:
                    _timer.Stop();
                    _resetToken = null;
                    _header.Title = "Đặt lại mật khẩu";
                    _lblStepTitle.Text = "Nhập email của bạn";
                    _lblStepDesc.Text = "Chúng tôi sẽ gửi mã OTP để đặt lại mật khẩu.";
                    SetStep1Visible(true); SetStep2Visible(false); SetStep3Visible(false);
                    _btnNext.Text = "GỬI MÃ OTP";
                    break;
                case 2:
                    _timer.Stop();
                    _header.Title = "Nhập mã xác nhận";
                    _lblStepTitle.Text = "Kiểm tra email của bạn";
                    _lblStepDesc.Text = $"Mã 6 chữ số đã được gửi đến\n{_tbEmail.Text}";
                    SetStep1Visible(false); SetStep2Visible(true); SetStep3Visible(false);
                    _btnNext.Text = "XÁC NHẬN";
                    _countdown = 60; _timer.Start(); UpdateCountdown();
                    _otpBoxes[0].Focus();
                    break;
                case 3:
                    _timer.Stop();
                    _header.Title = "Mật khẩu mới";
                    _lblStepTitle.Text = "Đặt mật khẩu mới";
                    _lblStepDesc.Text = "Mật khẩu phải có chữ hoa, chữ thường, số và ký tự đặc biệt.";
                    SetStep1Visible(false); SetStep2Visible(false); SetStep3Visible(true);
                    _btnNext.Text = "ĐẶT LẠI MẬT KHẨU";
                    _tbNewPass.Text = "";
                    _tbConfirmPass.Text = "";
                    break;
            }
            DoLayout(_pnlMain); // ← thêm dòng này vào cuối
        }

        private void SetStep1Visible(bool v)
        {
            _pnlContent.Controls[0].Visible = v; // lblEmail
            _pnlContent.Controls[1].Visible = v; // tbEmail
            _lblEmailHint.Visible = false;
        }
        private void SetStep2Visible(bool v)
        {
            _pnlContent.Controls[3].Visible = v; // pnlOtp
            _pnlContent.Controls[4].Visible = v; // countdown
        }
        private void SetStep3Visible(bool v)
        {
            _pnlContent.Controls[5].Visible = v;
            _pnlContent.Controls[6].Visible = v;
            _pnlContent.Controls[7].Visible = v;
            _pnlContent.Controls[8].Visible = v;
        }

        private void DoLayout(Panel pnlMain)
        {
            int pad = 28, w = pnlMain.Width - pad * 2, y = 12;
            var pnlSteps = pnlMain.Controls[2] as Panel;

            _lblStepTitle.SetBounds(0, y, pnlMain.Width, 26); y += 30;

            // _lblStepDesc.SetBounds(10, y, pnlMain.Width - 20, 36); y += 44;
            _lblStepDesc.SetBounds(10, y, pnlMain.Width - 20, 44); y += 52;

            pnlSteps?.SetBounds(0, y, pnlMain.Width, 48); y += 56;

            _pnlContent.SetBounds(pad, y, w, 160);
            int cy = 0;

            if (_step == 1)
            {
                _pnlContent.Controls[0].SetBounds(0, cy, w, 18); cy += 22;
                _pnlContent.Controls[1].SetBounds(0, cy, w, 44); cy += 52;
                if (_lblEmailHint.Visible) { _lblEmailHint.SetBounds(0, cy, w, 52); cy += 58; }
            }
            else if (_step == 2)
            {
                _pnlContent.Controls[3].SetBounds(0, cy, w, 62); cy += 70;
                _pnlContent.Controls[4].SetBounds(0, cy, w, 22); cy += 28;
                int total = 6 * 42 + 5 * 6, startX = (w - total) / 2;
                for (int i = 0; i < 6; i++) _otpBoxes[i].Location = new Point(startX + i * 48, 4);
            }
            else
            {
                _pnlContent.Controls[5].SetBounds(0, cy, w, 18); cy += 22;
                _pnlContent.Controls[6].SetBounds(0, cy, w, 44); cy += 52;
                _pnlContent.Controls[7].SetBounds(0, cy, w, 18); cy += 22;
                _pnlContent.Controls[8].SetBounds(0, cy, w, 44); cy += 48;
            }

            _pnlContent.Height = cy;
            y += _pnlContent.Height + 12;
            _lblError.SetBounds(0, y, pnlMain.Width, 20); y += 24;
            _btnNext.SetBounds(pad, y, w, 46);
        }

        private async void BtnNext_ClickAsync(object sender, EventArgs e)
        {
            HideError();
            try
            {
                SetBusy(true);
                switch (_step)
                {
                    case 1:
                        await HandleRequestOtpAsync();
                        break;
                    case 2:
                        await HandleVerifyOtpAsync();
                        break;
                    case 3:
                        await HandleResetPasswordAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[frmForgot] Unexpected UI error: {ex}");
                frmError.ShowError(this, "Đã xảy ra lỗi", ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task HandleRequestOtpAsync()
        {
            var email = _tbEmail.Text.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                ShowError("Vui lòng nhập email.");
                return;
            }

            if (!ValidationHelper.IsValidEmail(email))
            {
                ShowError("Email không đúng định dạng.");
                return;
            }

            var result = await _authService.RequestPasswordOtpAsync(email);
            if (!result.Success)
            {
                frmError.ShowApi(this, result.Message, "Không thể gửi OTP. Vui lòng thử lại.");
                return;
            }

            _lblEmailHint.Text = result.Message;
            _lblEmailHint.Visible = true;
            DoLayout(_pnlMain);

            frmError.ShowSuccess(this, "Đã gửi OTP",
                string.IsNullOrWhiteSpace(result.Message)
                    ? "Vui lòng kiểm tra email để lấy mã xác nhận."
                    : result.Message);
            ShowStep(2);
        }

        private async Task HandleVerifyOtpAsync()
        {
            var otp = string.Concat(Array.ConvertAll(_otpBoxes, b => b.Text)).Trim();
            if (otp.Length != 6)
            {
                ShowError("Vui lòng nhập đủ 6 chữ số.");
                return;
            }

            var result = await _authService.VerifyPasswordOtpAsync(_tbEmail.Text.Trim(), otp);
            if (!result.Success || result.Data is null)
            {
                frmError.ShowApi(this, result.Message, "Không thể xác thực OTP.");
                if (!string.IsNullOrWhiteSpace(result.ErrorCode) && result.ErrorCode.Contains("EXPIRED"))
                {
                    foreach (var otpBox in _otpBoxes)
                    {
                        otpBox.Text = string.Empty;
                    }

                    _countdown = 0;
                    UpdateCountdown();
                }

                return;
            }

            _resetToken = result.Data.ResetToken;
            ShowStep(3);
        }

        private async Task HandleResetPasswordAsync()
        {
            if (_tbNewPass.Text != _tbConfirmPass.Text)
            {
                ShowError("Mật khẩu xác nhận không khớp.");
                return;
            }

            if (!ValidationHelper.IsStrongPassword(_tbNewPass.Text, out var passwordError))
            {
                ShowError(passwordError);
                return;
            }

            var result = await _authService.ResetPasswordAsync(_resetToken ?? string.Empty, _tbNewPass.Text);
            if (!result.Success)
            {
                frmError.ShowApi(this, result.Message, "Không thể đặt lại mật khẩu.");
                if (result.ErrorCode is not null && result.ErrorCode.Contains("TOKEN"))
                {
                    ShowStep(1);
                }
                return;
            }

            frmError.ShowSuccess(this, "Đặt lại mật khẩu thành công",
                "Mật khẩu của bạn đã được cập nhật. Vui lòng đăng nhập lại.");
            Close();
        }

        private async Task HandleResendOtpAsync()
        {
            if (_step != 2 || _isBusy || _countdown > 0)
            {
                return;
            }

            HideError();
            var email = _tbEmail.Text.Trim();
            if (!ValidationHelper.IsValidEmail(email))
            {
                ShowError("Email không đúng định dạng.");
                return;
            }

            try
            {
                SetBusy(true);
                var result = await _authService.RequestPasswordOtpAsync(email);
                if (!result.Success)
                {
                    frmError.ShowApi(this, result.Message, "Không thể gửi lại OTP.");
                    return;
                }

                foreach (var otpBox in _otpBoxes)
                {
                    otpBox.Text = string.Empty;
                }

                _countdown = 60;
                _timer.Start();
                UpdateCountdown();
                _otpBoxes[0].Focus();
                frmError.ShowSuccess(this, "Đã gửi lại OTP", "Mã xác nhận mới đã được gửi đến email của bạn.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[frmForgot] Resend OTP failed: {ex}");
                frmError.ShowError(this, "Không thể gửi lại OTP", ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void UpdateCountdown()
        {
            _lblCountdown.Text = _countdown > 0
                ? $"Không nhận được mã? Gửi lại sau ({_countdown}s)"
                : "Không nhận được mã? Nhấn Gửi lại";

            _lblCountdown.ForeColor = _countdown > 0 ? TG.TextSecondary : TG.Blue;
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            _btnNext.Enabled = !busy;
            _btnNext.Text = busy ? "ĐANG XỬ LÝ..." : _step switch
            {
                1 => "GỬI MÃ OTP",
                2 => "XÁC NHẬN",
                3 => "ĐẶT LẠI MẬT KHẨU",
                _ => "TIẾP THEO"
            };
            _header.ShowBack = !busy;
        }

        private void ShowError(string msg) { _lblError.Text = msg; _lblError.Visible = true; }
        private void HideError() { _lblError.Visible = false; }
        protected override void OnFormClosed(FormClosedEventArgs e) { _timer?.Stop(); base.OnFormClosed(e); }
    }
}
