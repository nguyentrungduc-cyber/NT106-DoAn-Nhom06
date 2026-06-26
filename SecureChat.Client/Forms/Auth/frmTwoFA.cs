using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;
using System.Threading.Tasks;
using SecureChat.Client.Forms.Shared;
using SecureChat.Client.Forms.Settings;
using SecureChat.Client.Services;

namespace SecureChat.Client
{

    /// Two-Factor Authentication screen (6-digit OTP)

    public class frmTwoFA : Form
    {
        private readonly string _identifier;
        private TextBox[] _otpBoxes = new TextBox[6];
        private TelegramButton _btnConfirm;
        private Label _lblTitle, _lblDesc, _lblResend, _lblTimer;
        private System.Windows.Forms.Timer _timer;
        private int _countdown = 60;
        private Label _lblError;

        public frmTwoFA(string identifier)
        {
            _identifier = identifier ?? string.Empty;
            InitializeComponent();

            ThemeRefreshHelper.Hook(this);
            UiLocalization.ApplyToForm(this);
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
            Text = "Two-Factor Authentication";
            Size = new Size(520, 600);
            MinimumSize = new Size(500, 580);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = TG.WindowBg;
            Font = TG.FontRegular(9.5f);

            // Header panel
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
                Text = "Two-Factor Authentication",
                Font = TG.FontSemiBold(15f),
                ForeColor = Color.White,
                AutoSize = false,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
            };
            var lblSub = new Label
            {
                Text = "Enter the verification code sent to your email.",
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

            // Body
            _lblDesc = new Label
            {
                Text = "Enter the 6-digit verification code:",
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
                    TextAlign = HorizontalAlignment.Center,
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.None,
                    Size = new Size(58, 62),
                };

                // Wrapper panel to draw custom rounded border.
                // Each wrap holds one TextBox
                var wrap = new Panel
                {
                    Size = new Size(62, 70),
                    BackColor = TG.WindowBg,
                };

                // Paint event: redraws border on form resize, invalidation, etc.
                // Redraw border based on state (focus/filled/empty).
                wrap.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                    bool focused = box.Focused;
                    bool filled = !string.IsNullOrEmpty(box.Text);

                    // Blue border if focused or filled, otherwise dimmed divider color.
                    Color border = focused ? TG.Blue : filled ? TG.Blue : TG.Divider;

                    // Thicker (2px) border on focus, normal (1px) otherwise.
                    float bw = focused ? 2f : 1f;

                    // Create a rectangle matching the wrap size.
                    var r = new Rectangle(0, 0, wrap.Width - 1, wrap.Height - 1);

                    using var path = RoundedPanel.GetRoundedPath(r, TG.RadiusSmall);

                    e.Graphics.FillPath(Brushes.White, path);

                    e.Graphics.DrawPath(new Pen(border, bw), path);
                };
                wrap.Controls.Add(box);
                box.Location = new Point(2, (70 - box.Height) / 2);
                // X = 2 for even padding on both sides

                // Auto advance
                box.TextChanged += (s, e) =>
                {
                    wrap.Invalidate();

                    // Auto-advance to next box on input
                    if (!string.IsNullOrEmpty(box.Text) && idx < 5)
                        _otpBoxes[idx + 1].Focus();

                    // Last box -> focus Confirm button
                    if (!string.IsNullOrEmpty(box.Text) && idx == 5)
                        _btnConfirm.Focus();
                };

                // Backspace on empty box -> go back
                box.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Back && string.IsNullOrEmpty(box.Text) && idx > 0)
                        _otpBoxes[idx - 1].Focus();
                };

                box.GotFocus += (s, e) => wrap.Invalidate();
                box.LostFocus += (s, e) => wrap.Invalidate();

                pnlOtp.Controls.Add(wrap);
                _otpBoxes[i] = box;
            }

            // Layout OTP - Ensure visibility on resize
            pnlOtp.Resize += (s, e) =>
            {
                int boxW = 62;
                int spacing = 14;
                int total = 6 * boxW + 5 * spacing;

                // Ensure enough space; reduce spacing if needed
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
                Text = "CONFIRM",
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
                Text = "Didn't get the code?",
                Font = TG.FontRegular(9f),
                ForeColor = TG.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent,
            };
            var lnkResend = new LinkLabel
            {
                Text = "Resend",
                LinkColor = TG.Blue,
                Font = TG.FontRegular(9f),
                AutoSize = true,
                BackColor = Color.Transparent,
                Enabled = false, // Initially disabled (wait 60s)
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
                                frmError.ShowApi(this, err, "Could not resend OTP. Please try again.");
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
                            frmError.ShowSuccess(this, "OTP Resent", "A new verification code has been sent to your email.");
                        });
                    }
                    catch (Exception ex)
                    {
                        this.Invoke(() =>
                        {
                            lnkResend.Enabled = true;
                            frmError.ShowError(this, "Resend Failed", ex.Message);
                        });
                    }
                });
            };

            // Panel containing all body content below header, with increased padding
            var pnlBody = new Panel { Dock = DockStyle.Fill, BackColor = TG.WindowBg, Padding = new Padding(40, 20, 40, 20) };
            pnlBody.Controls.AddRange(new Control[] { _lblDesc, pnlOtp, _lblError, _btnConfirm, _lblResend, lnkResend, _lblTimer });

            // Arrange controls vertically, recalculate on resize.
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

            _timer = new System.Windows.Forms.Timer { Interval = 1000 };
            _timer.Tick += (s, e) =>
            {
                _countdown--;
                _lblTimer.Text = $"({_countdown}s)";
                if (_countdown <= 0) { _timer.Stop(); lnkResend.Enabled = true; _lblTimer.Text = ""; }
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
            if (code.Length < 6) { ShowError("Please enter all 6 digits."); return; }
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
                            frmError.ShowApi(this, err, "Invalid verification code.");
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
                        frmError.ShowError(this, "Verification Failed", "Invalid server response.");
                    });
                }
                catch (Exception ex)
                {
                    this.Invoke(() =>
                    {
                        _btnConfirm.Enabled = true;
                        frmError.ShowError(this, "Connection Error", ex.Message);
                    });
                }
            });
        }

        private void ShowError(string msg) { _lblError.Text = msg; _lblError.Visible = true; }
        private void HideError() { _lblError.Visible = false; }

        protected override void OnFormClosed(FormClosedEventArgs e) { _timer?.Stop(); base.OnFormClosed(e); }
    }
}
