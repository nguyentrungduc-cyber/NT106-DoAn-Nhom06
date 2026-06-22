using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;
using System.Threading.Tasks;
using SecureChat.Client.Forms.Shared;
using SecureChat.Client.Services;

namespace SecureChat.Client
{

    /// Màn hình nhập mã 2FA (OTP 5 ký tự kiểu Telegram)

    public class frmTwoFA : Form
    {
        private readonly string _identifier;
        private TextBox[] _otpBoxes = new TextBox[6];
        private TelegramButton _btnConfirm;
        private Label _lblTitle, _lblDesc, _lblResend, _lblTimer;

        // khác System.Threading.Timer — cái này chạy trên UI thread, an toàn khi cập nhật giao diện
        private System.Windows.Forms.Timer _timer;

        private int _countdown = 60;
        private Label _lblError;

        public frmTwoFA(string identifier)
        {
            _identifier = identifier ?? string.Empty;
            InitializeComponent();

            ThemeRefreshHelper.Hook(this);
            // vì khi biết email thì gửi Opt đầu tiên, lúc này bộ đếm sẽ chạy lần đầu tiên
            StartCountdown();
        }

        // Parameterless constructor for designer
        public frmTwoFA()
        {
            _identifier = string.Empty;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Xác minh 2 bước";
            Size = new Size(520, 600);
            MinimumSize = new Size(500, 580);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = TG.WindowBg;
            Font = TG.FontRegular(9.5f);

            // ── Header xanh ──────────────────────────
            var header = new Panel { Height = 180, BackColor = TG.Blue, Dock = DockStyle.Top };

            var lblIcon = new Label
            {
                Text = "🔐",
                Font = new Font("Segoe UI Emoji", 42f),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(72, 72),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
            };
            var lblH = new Label
            {
                Text = "Xác minh 2 bước",
                Font = TG.FontSemiBold(15f),
                ForeColor = Color.White,
                AutoSize = false,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
            };
            var lblSub = new Label
            {
                Text = "Mã xác nhận đã được gửi đến email của bạn",
                Font = TG.FontRegular(8.5f),
                ForeColor = Color.FromArgb(200, 235, 255),
                AutoSize = false,
                Height = 20,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
            };
            header.Controls.AddRange(new Control[] { lblIcon, lblH, lblSub });
            header.Resize += (s, e) =>
            {
                lblIcon.Location = new Point((header.Width - 60) / 2, 14);
                lblH.SetBounds(0, 82, header.Width, 28);
                lblSub.SetBounds(0, 110, header.Width, 20);
            };

            // ── Body ──────────────────────────────────
            _lblDesc = new Label
            {
                Text = "Nhập 6 chữ số trong mã xác nhận:",
                Font = TG.FontRegular(9.5f),
                ForeColor = TG.TextSecondary,
                AutoSize = false,
                Height = 24,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
            };

            // OTP boxes for 6 Textboxes
            var pnlOtp = new Panel { Height = 70, BackColor = Color.Transparent };
            for (int i = 0; i < 6; i++)
            {
                int idx = i;
                var box = new TextBox
                {
                    MaxLength = 1,
                    Font = TG.FontTitle(24f),
                    ForeColor = TG.Blue,
                    TextAlign = HorizontalAlignment.Center, // Căn giữa ký tự cho một TextBox
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.None, // Ẩn viền mặc định (tự vẽ viền bo góc)
                    Size = new Size(58, 62),
                };

                // Panel bọc ngoài TextBox để vẽ viền bo góc tùy chỉnh.
                // Mỗi wrap bọc 1 Textbox
                var wrap = new Panel
                {
                    Size = new Size(62, 70),
                    BackColor = TG.WindowBg,
                };

                // Sự kiện Paint: mỗi khi giao diện cần hiển thị lại (khi mở form, thay đổi kích thước, hoặc khi bạn gọi Invalidate()), các lệnh này sẽ thực thi để "vẽ" lên màn hình.
                // Vẽ lại viền mỗi khi ô được tô vẽ. Viền thay đổi theo trạng thái (focus/filled/empty).
                wrap.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; //Kích hoạt chế độ khử răng cưa.Nó giúp các đường cong(của góc bo) trông mịn màng, không bị gai hoặc răng cưa.

                    bool focused = box.Focused; // Người dùng có đang đặt con trỏ chuột vào ô đó không?
                    bool filled = !string.IsNullOrEmpty(box.Text); // Ô đó đã có chữ chưa?

                    // Nếu đang được focus HOẶC đã có nội dung, viền sẽ hiện màu xanh(TG.Blue).Nếu trống trơn, viền sẽ mờ đi(TG.Divider).
                    Color border = focused ? TG.Blue : filled ? TG.Blue : TG.Divider;

                    // Khi bạn click vào ô(focus), viền sẽ dày lên 2px để làm nổi bật, bình thường chỉ 1px.
                    float bw = focused ? 2f : 1f;

                    //Tạo một khung chữ nhật khớp với kích thước của wrap.
                    var r = new Rectangle(0, 0, wrap.Width - 1, wrap.Height - 1);


                    using var path = RoundedPanel.GetRoundedPath(r, TG.RadiusSmall); // Bo góc

                    e.Graphics.FillPath(Brushes.White, path);  // Tô nền trắng

                    e.Graphics.DrawPath(new Pen(border, bw), path); // Vẽ viền
                };
                wrap.Controls.Add(box);
                box.Location = new Point(2, (70 - box.Height) / 2); // Căn giữa dọc trong wrap
                // X = 2 để có padding đều hai bên

                // Auto advance
                box.TextChanged += (s, e) =>
                {
                    wrap.Invalidate(); // Vẽ lại viền

                    // Nhập xong → tự nhảy sang ô tiếp theo
                    if (!string.IsNullOrEmpty(box.Text) && idx < 5)
                        _otpBoxes[idx + 1].Focus();

                    // Ô cuối → focus vào nút Xác nhận
                    if (!string.IsNullOrEmpty(box.Text) && idx == 5)
                        _btnConfirm.Focus();
                };

                // Nhấn Backspace khi ô đang trống → lùi về ô trước
                box.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Back && string.IsNullOrEmpty(box.Text) && idx > 0)
                        _otpBoxes[idx - 1].Focus();
                };

                box.GotFocus += (s, e) => wrap.Invalidate(); // Focus vào → vẽ lại viền xanh
                box.LostFocus += (s, e) => wrap.Invalidate(); // Mất focus → vẽ lại viền xám

                pnlOtp.Controls.Add(wrap); // thêm panel con -> panel cha
                _otpBoxes[i] = box;
            }

            // Layout OTP - Đảm bảo không bị khuất khi resize
            pnlOtp.Resize += (s, e) =>
            {
                int boxW = 62;
                int spacing = 14;
                int total = 6 * boxW + 5 * spacing; // 6 boxes + 5 spacing
                
                // Đảm bảo có đủ không gian, nếu không thì giảm spacing
                if (total > pnlOtp.Width)
                {
                    spacing = Math.Max(8, (pnlOtp.Width - 6 * boxW) / 5);
                    total = 6 * boxW + 5 * spacing;
                }
                
                int startX = Math.Max(0, (pnlOtp.Width - total) / 2);
                for (int i = 0; i < pnlOtp.Controls.Count; i++)
                    pnlOtp.Controls[i].Location = new Point(startX + i * (boxW + spacing), 0);
            };

            // Error
            _lblError = new Label
            {
                AutoSize = false,
                Height = 20,
                ForeColor = Color.FromArgb(0xE2, 0x4B, 0x4A),
                Font = TG.FontRegular(8.5f),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false,
            };

            // Button
            _btnConfirm = new TelegramButton
            {
                Text = "XÁC NHẬN",
                Height = 46,
                Font = TG.FontSemiBold(10.5f),
                Radius = TG.RadiusSmall,
            };
            _btnConfirm.Click += BtnConfirm_Click;

            // Resend
            _lblTimer = new Label
            {
                AutoSize = true,
                Font = TG.FontRegular(9f),
                ForeColor = TG.TextSecondary,
                BackColor = Color.Transparent,
            };
            _lblResend = new Label
            {
                Text = "Không nhận được mã?",
                Font = TG.FontRegular(9f),
                ForeColor = TG.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent,
            };
            var lnkResend = new LinkLabel
            {
                Text = "Gửi lại",
                LinkColor = TG.Blue,
                Font = TG.FontRegular(9f),
                AutoSize = true,
                BackColor = Color.Transparent,
                Enabled = false, // Vô hiệu ban đầu (phải chờ 60 giây)
            };

            lnkResend.LinkClicked += (s, e) =>
            {
                // Call server to resend OTP. Do not restart countdown unless server confirms.
                lnkResend.Enabled = false;
                Task.Run(async () =>
                {
                    try
                    {
                        var payload = new { Identifier = _identifier };
                        var (ok, _, err) = await ApiClient.Instance.PostAsync<object, System.Text.Json.JsonElement>("api/auth/resend-login-otp", payload);
                        if (!ok)
                        {
                            this.Invoke(() =>
                            {
                                lnkResend.Enabled = true;
                                frmError.ShowApi(this, err, "Không thể gửi lại OTP. Vui lòng thử lại.");
                            });
                            return;
                        }

                        // success: reset countdown and disable resend until timer expires
                        this.Invoke(() =>
                        {
                            _countdown = 60;
                            _lblTimer.Text = $"({_countdown}s)";
                            StartCountdown();
                            HideError();
                            frmError.ShowSuccess(this, "Đã gửi lại OTP", "Mã xác nhận mới đã được gửi đến email của bạn.");
                        });
                    }
                    catch (Exception ex)
                    {
                        this.Invoke(() =>
                        {
                            lnkResend.Enabled = true;
                            frmError.ShowError(this, "Gửi lại thất bại", ex.Message);
                        });
                    }
                });
            };

            // Panel chứa toàn bộ nội dung bên dưới header, padding tăng lên để thoáng hơn
            var pnlBody = new Panel { Dock = DockStyle.Fill, BackColor = TG.WindowBg, Padding = new Padding(40, 20, 40, 20) };
            pnlBody.Controls.AddRange(new Control[] { _lblDesc, pnlOtp, _lblError, _btnConfirm, _lblResend, lnkResend, _lblTimer });

            // Sắp xếp các control theo chiều dọc, tự tính lại khi form thay đổi kích thước. Biến y tích lũy vị trí từng control.
            pnlBody.Resize += (s, e) =>
            {
                int pad = 40, w = pnlBody.Width - pad * 2, y = 20;
                _lblDesc.SetBounds(0, y, pnlBody.Width, 28); y += 40;
                pnlOtp.SetBounds(pad, y, w, 70); y += 86;
                _lblError.SetBounds(0, y, pnlBody.Width, 20); y += 28;
                _btnConfirm.SetBounds(pad, y, w, 46); y += 60;
                _lblResend.Location = new Point(pad, y);
                lnkResend.Location = new Point(pad + _lblResend.Width + 4, y);
                _lblTimer.Location = new Point(pad + _lblResend.Width + lnkResend.Width + 8, y);
            };

            _timer = new System.Windows.Forms.Timer { Interval = 1000 }; // Mỗi 1000ms = 1 giây
            _timer.Tick += (s, e) =>
            {
                _countdown--;
                _lblTimer.Text = $"({_countdown}s)"; // Hiện thời gian giảm dần
                if (_countdown <= 0) { _timer.Stop(); lnkResend.Enabled = true; _lblTimer.Text = ""; } // Bật gửi lại, Ẩn đếm ngược
            };

            Controls.AddRange(new Control[] { pnlBody, header });
        }

        private void StartCountdown()
        {
            _lblTimer.Text = $"({_countdown}s)";
            _timer.Start();
        }

        private string GetOtpCode()
        {
            string code = "";
            foreach (var box in _otpBoxes) code += box.Text;
            return code;
        }

        private void BtnConfirm_Click(object sender, EventArgs e)
        {
            string code = GetOtpCode();
            if (code.Length < 6) { ShowError("Vui lòng nhập đủ 6 chữ số."); return; }
            HideError();

            // Call server verify-login-otp
            _btnConfirm.Enabled = false;
            Task.Run(async () =>
            {
                try
                {
                    var payload = new { Identifier = _identifier, Otp = code, DeviceName = Environment.MachineName };
                    var (ok, res, err) = await ApiClient.Instance.PostAsync<object, System.Text.Json.JsonElement>("api/auth/verify-login-otp", payload);
                    if (!ok)
                    {
                        this.Invoke(() =>
                        {
                            _btnConfirm.Enabled = true;
                            frmError.ShowApi(this, err, "Mã OTP không đúng hoặc đã hết hạn.");
                        });
                        return;
                    }

                    // success -> extract token
                    if (res.ValueKind == System.Text.Json.JsonValueKind.Object && res.TryGetProperty("token", out var tprop))
                    {
                        var token = tprop.GetString();
                        if (!string.IsNullOrWhiteSpace(token))
                        {
                            ApiClient.Instance.SetAccessToken(token);
                            this.Invoke(() => { DialogResult = DialogResult.OK; Close(); });
                            return;
                        }
                    }

                    this.Invoke(() =>
                    {
                        _btnConfirm.Enabled = true;
                        frmError.ShowError(this, "Xác thực thất bại", "Phản hồi từ máy chủ không hợp lệ.");
                    });
                }
                catch (Exception ex)
                {
                    this.Invoke(() =>
                    {
                        _btnConfirm.Enabled = true;
                        frmError.ShowError(this, "Lỗi kết nối", ex.Message);
                    });
                }
            });
        }

        private void ShowError(string msg) { _lblError.Text = msg; _lblError.Visible = true; }
        private void HideError() { _lblError.Visible = false; }

        protected override void OnFormClosed(FormClosedEventArgs e) { _timer?.Stop(); base.OnFormClosed(e); }
    }
}
