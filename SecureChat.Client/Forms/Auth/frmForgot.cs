using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

using SecureChat.Client.Forms.Shared;
using SecureChat.Client.Forms.Settings;
using SecureChat.Client.Helpers;
using SecureChat.Client.Services;
using SecureChat.Client.Services.Api;

namespace SecureChat.Client
{

    /// Forgot password screen - 3 steps: Email → OTP → New password

    public class frmForgot : Form
    {
        private readonly IAuthService _authService;
        private int _step = 1; // 1=email, 2=otp, 3=newpass
        private string? _resetToken;

        private Panel _pnlMain;

        // Array of 3 circular step indicators (Step 1 → 2 → 3). Each dot is a self-drawing Panel.
        private Panel[] _stepDots = new Panel[3];

        // Large title and small description that change with each step.
        private Label _lblStepTitle, _lblStepDesc;

        // Step 1
        private TelegramTextBox _tbEmail; // _tbEmail: email input field
        private Label _lblEmailHint; // _lblEmailHint: green hint message "Link valid for 15 minutes…" (hidden initially)

        // Step 2
        private Panel _pnlOtp; // panel containing 6 OTP input boxes
        private TextBox[] _otpBoxes = new TextBox[6]; // array of 6 TextBoxes, each for 1 digit
        private System.Windows.Forms.Timer _timer; // 60-second countdown timer
        private int _countdown = 60; // current countdown value
        private Label _lblCountdown; // label showing "Resend in (Xs)"

        // Step 3: Two input fields for new password and confirm password.
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
            UiLocalization.ApplyToForm(this);
            ShowStep(1);
        }

        private void InitializeComponent()
        {
            Text = "Forgot Password";
            Size = new Size(400, 520);
            MinimumSize = new Size(380, 490);
            // StartPosition = FormStartPosition.CenterParent;
            //  FormBorderStyle = FormBorderStyle.FixedSingle;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            BackColor = TG.WindowBg;
            Font = TG.FontRegular(9.5f);

            // Header
            _header = new TelegramHeader { Title = "Forgot Password" };
            _header.ShowBack = true; // Show Back button (arrow to go back)
            _header.BackClicked += (s, e) =>
            {
                if (_step > 1) ShowStep(_step - 1); // if at step 2 or 3, go back to previous step
                else Close(); // if at step 1, close form
            };
            Controls.Add(_header);

            // Step indicator
            // Horizontal panel containing 3 circular step dots, height 48px
            var pnlSteps = new Panel { Height = 48, BackColor = TG.WindowBg };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                // Each dot is a 28x28 Panel, transparent (self-draws a circle inside).
                var dot = new Panel
                {
                    Size = new Size(28, 28),
                    BackColor = Color.Transparent
                };
                // Instead of using the default interface, we intercept the drawing process of the dot.
                dot.Paint += (s, e) =>
                {
                    // e.Graphics: the main drawing tool for rendering on the Control surface.
                    // SmoothingMode.AntiAlias: Enables anti-aliasing so circles look smooth without jagged edges.
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    bool active = _step == idx + 1; // (Current step): If the current step matches this dot's position.
                    bool done = _step > idx + 1; // (Completed): If the current step has passed this dot's position.

                    // If completed or active: Fill with blue (TG.Blue).
                    // If not yet reached: Fill with gray divider (TG.Divider).
                    Color bg = done ? TG.Blue : active ? TG.Blue : TG.Divider;

                    // Draw a solid circle with bg color.
                    // Draw at coordinates (0,0) with size 27x27 pixels.
                    e.Graphics.FillEllipse(new SolidBrush(bg), 0, 0, 27, 27);

                    // If completed: Show checkmark "✓".
                    // If not completed: Show step number (idx + 1): (1, 2, 3).
                    string txt = done ? "✓" : (idx + 1).ToString();

                    // Draw white text, centered both horizontally and vertically in the 28x28 box.
                    using var sf = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center };
                    e.Graphics.DrawString(txt, TG.FontSemiBold(9f), System.Drawing.Brushes.White, new Rectangle(0, 0, 28, 28), sf);
                };
                // Store in array and add to panel.
                _stepDots[i] = dot;
                pnlSteps.Controls.Add(dot);
            }

            // Draw connecting lines between dots with changing colors
            pnlSteps.Paint += (s, e) =>
            {
                int lineY = 24 + pnlSteps.Padding.Top; // y-coordinate for horizontal line between dots
                int[] xs = GetDotXs(pnlSteps.Width); // returns an array of X coordinates for the 3 dots

                // If _step > 1 (step 1 done), line is blue (TG.Blue), otherwise gray (TG.Divider)
                // Starts from right edge of dot 1 (xs[0] + 28) and goes to left edge of dot 2 (xs[1])
                e.Graphics.DrawLine(new System.Drawing.Pen(_step > 1 ? TG.Blue : TG.Divider, 2), xs[0] + 28, lineY, xs[1], lineY);

                e.Graphics.DrawLine(new System.Drawing.Pen(_step > 2 ? TG.Blue : TG.Divider, 2), xs[1] + 28, lineY, xs[2], lineY);
            };

            // When panel resizes, recalculate dot positions and redraw.
            pnlSteps.Resize += (s, e) =>
            {
                int[] xs = GetDotXs(pnlSteps.Width);
                for (int i = 0; i < 3; i++) _stepDots[i].Location = new Point(xs[i], 10);
                pnlSteps.Invalidate(); // Invalidate() triggers the Paint event to redraw
            };

            // Step labels
            _lblStepTitle = new Label // stores the large step title
            {
                AutoSize = false,
                Height = 26,
                Font = TG.FontSemiBold(12f),
                ForeColor = TG.TextPrimary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            _lblStepDesc = new Label // stores the small description below the title
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
            var lblEmail = new Label { Text = "Email", Font = TG.FontRegular(8.5f), ForeColor = TG.Blue, AutoSize = false, Height = 22, BackColor = Color.Transparent };

            // Email input box 44px high, with placeholder hint.
            _tbEmail = new TelegramTextBox { Height = 44 };
            _tbEmail.SetPlaceholder("user@example.com");

            _lblEmailHint = new Label
            {
                Text = "📧  The reset link is valid for 15 minutes. Also check your spam folder.",
                Font = TG.FontRegular(8.5f),
                ForeColor = Color.FromArgb(0x2E, 0x7D, 0x32), // dark green text
                BackColor = Color.FromArgb(0xE8, 0xF5, 0xE9), // light green background
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
                    MaxLength = 1, // each box accepts exactly 1 character
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

                // When a character is typed in the current box and it's not the last (idx < 5), auto-focus next box.
                box.TextChanged += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(box.Text) && idx < 5) _otpBoxes[idx + 1].Focus();
                };

                // When Backspace is pressed on an empty box and it's not the first, go back to previous box (natural backspace).
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
                // total = total width: 6 boxes x 42px + 5 gaps x 6px = 282px
                // startX = starting point to center within the panel
                // Each box is spaced 48px apart (42px box + 6px gap), offset 4px from top
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
            _timer = new System.Windows.Forms.Timer { Interval = 1000 }; // Timer ticks every 1000ms = 1 second.

            // Each tick: decrement countdown by 1, update label, stop timer when it reaches 0.
            _timer.Tick += (s, e) => { _countdown--; UpdateCountdown(); if (_countdown <= 0) _timer.Stop(); };


            // ── Step 3: New Password ──────────────────
            var lblNew = new Label { Text = "New password", Font = TG.FontRegular(8.5f), ForeColor = TG.Blue, AutoSize = false, Height = 22, BackColor = Color.Transparent };
            _tbNewPass = new TelegramTextBox { Height = 44 };
            _tbNewPass.SetPlaceholder("At least 8 characters...");
            _tbNewPass.PasswordCharValue = '●';

            var lblConf = new Label { Text = "Confirm Password", Font = TG.FontRegular(8.5f), ForeColor = TG.Blue, AutoSize = false, Height = 22, BackColor = Color.Transparent };
            _tbConfirmPass = new TelegramTextBox { Height = 44 };
            _tbConfirmPass.SetPlaceholder("Re-enter password...");
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
            _btnNext = new TelegramButton { Text = "NEXT", Height = 46, Font = TG.FontSemiBold(10.5f), Radius = TG.RadiusSmall };
            _btnNext.Click += BtnNext_ClickAsync;

            _pnlContent.Controls.AddRange(new Control[] {
                // step1
                lblEmail, _tbEmail, _lblEmailHint,
                // step2
                _pnlOtp, _lblCountdown,
                // step3
                lblNew, _tbNewPass, lblConf, _tbConfirmPass,
            });

            // Outer panel fills the entire form, with 28px padding on each side. Layout recalculated on each resize.
            _pnlMain = new Panel { BackColor = TG.WindowBg, Padding = new Padding(28, 12, 28, 20) };
            _pnlMain.Controls.AddRange(new Control[] { _lblStepTitle, _lblStepDesc, pnlSteps, _pnlContent, _lblError, _btnNext });
            _pnlMain.Dock = DockStyle.Fill;
            _pnlMain.Resize += (s, e) => DoLayout(_pnlMain);

            // Add main panel and header to form. Header added last so it stays on top (higher Z-order).
            Controls.AddRange(new Control[] { _pnlMain, _header });
        }

        // Calculate positions for the 3 step dots
        // Center the 3 dots symmetrically around the panel center. Distance between dots = 46px (28px dot + 18px connecting line).
        private int[] GetDotXs(int panelWidth)
        {
            int center = panelWidth / 2;
            return new[] { center - 60, center - 14, center + 32 };
        }

        // Switch steps
        private void ShowStep(int step)
        {
            _step = step;
            HideError();

            // Iterate through all dots and force them to redraw.
            // When _step changes (e.g. from step 1 to step 2), dots need to know whether to change from gray to blue, or from "2" to "✓".
            // Calling Invalidate() triggers the Paint event on each dot as defined earlier.
            foreach (var d in _stepDots) d.Invalidate();
            if (_stepDots[0].Parent != null) _stepDots[0].Parent.Invalidate();
            // Invalidate() tells Windows to redraw the dots and connecting lines (since colors change based on _step).
            // The null check prevents crashing if the dots haven't been added to a Panel yet at runtime.

            switch (step)
            {
                case 1:
                    _timer.Stop();
                    _resetToken = null;
                    _header.Title = "Forgot Password";
                    _lblStepTitle.Text = "Step 1: Enter your email";
                    _lblStepDesc.Text = "We will send a code to reset your password.";
                    SetStep1Visible(true); SetStep2Visible(false); SetStep3Visible(false);
                    _btnNext.Text = "SEND CODE";
                    break;
                case 2:
                    _timer.Stop();
                    _header.Title = "Forgot Password";
                    _lblStepTitle.Text = "Step 2: Enter verification code";
                    _lblStepDesc.Text = $"A 6-digit code has been sent to\n{_tbEmail.Text}";
                    SetStep1Visible(false); SetStep2Visible(true); SetStep3Visible(false);
                    _btnNext.Text = "CONFIRM";
                    _countdown = 60; _timer.Start(); UpdateCountdown();
                    _otpBoxes[0].Focus();
                    break;
                case 3:
                    _timer.Stop();
                    _header.Title = "Forgot Password";
                    _lblStepTitle.Text = "Step 3: Set new password";
                    _lblStepDesc.Text = "Password must have uppercase, lowercase, numbers and special characters.";
                    SetStep1Visible(false); SetStep2Visible(false); SetStep3Visible(true);
                    _btnNext.Text = "RESET";
                    _tbNewPass.Text = "";
                    _tbConfirmPass.Text = "";
                    break;
            }
            DoLayout(_pnlMain);
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
                frmError.ShowError(this, "Error", ex.Message);
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
                ShowError("Please enter your email.");
                return;
            }

            if (!ValidationHelper.IsValidEmail(email))
            {
                ShowError("Invalid email address.");
                return;
            }

            var result = await _authService.RequestPasswordOtpAsync(email);
            if (!result.Success)
            {
                frmError.ShowApi(this, result.Message, "Failed to send code.");
                return;
            }

            _lblEmailHint.Text = result.Message;
            _lblEmailHint.Visible = true;
            DoLayout(_pnlMain);

            frmError.ShowSuccess(this, "Code sent",
                string.IsNullOrWhiteSpace(result.Message)
                    ? "Please check your email for the verification code."
                    : result.Message);
            ShowStep(2);
        }

        private async Task HandleVerifyOtpAsync()
        {
            var otp = string.Concat(Array.ConvertAll(_otpBoxes, b => b.Text)).Trim();
            if (otp.Length != 6)
            {
                ShowError("Please enter the verification code.");
                return;
            }

            var result = await _authService.VerifyPasswordOtpAsync(_tbEmail.Text.Trim(), otp);
            if (!result.Success || result.Data is null)
            {
                frmError.ShowApi(this, result.Message, "Verification code is incorrect.");
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
                ShowError("New passwords do not match.");
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
                frmError.ShowApi(this, result.Message, "Password reset failed.");
                if (result.ErrorCode is not null && result.ErrorCode.Contains("TOKEN"))
                {
                    ShowStep(1);
                }
                return;
            }

            frmError.ShowSuccess(this, "Password has been reset successfully!",
                "Your password has been updated. Please log in again.");
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
                ShowError("Invalid email address.");
                return;
            }

            try
            {
                SetBusy(true);
                var result = await _authService.RequestPasswordOtpAsync(email);
                if (!result.Success)
                {
                    frmError.ShowApi(this, result.Message, "Failed to send code.");
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
                frmError.ShowSuccess(this, "Code resent", "A new verification code has been sent to your email.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[frmForgot] Resend OTP failed: {ex}");
                frmError.ShowError(this, "Failed to send code", ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void UpdateCountdown()
        {
            _lblCountdown.Text = _countdown > 0
                ? $"Didn't receive the code? Resend in ({_countdown}s)"
                : "Didn't receive the code? Click Resend";

            _lblCountdown.ForeColor = _countdown > 0 ? TG.TextSecondary : TG.Blue;
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            _btnNext.Enabled = !busy;
            _btnNext.Text = busy ? "PROCESSING..." : _step switch
            {
                1 => "SEND CODE",
                2 => "CONFIRM",
                3 => "RESET",
                _ => "NEXT"
            };
            _header.ShowBack = !busy;
        }

        private void ShowError(string msg) { _lblError.Text = msg; _lblError.Visible = true; }
        private void HideError() { _lblError.Visible = false; }
        protected override void OnFormClosed(FormClosedEventArgs e) { _timer?.Stop(); base.OnFormClosed(e); }
    }
}
