using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SecureChat.Client.Services;

namespace SecureChat.Client.Forms.Settings
{
    public class frmLanguage : Form
    {
        private LanguageType _selectedLanguage;

        public frmLanguage()
        {
            _selectedLanguage = LocalizationService.CurrentLanguage;
            BuildUI();
            ThemeRefreshHelper.Hook(this);
        }

        private void BuildUI()
        {
            Text = "Language";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            HelpButton = false;
            ControlBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(360, 320);
            BackColor = TG.WindowBg;
            DoubleBuffered = true;

            var title = new Label
            {
                Text = "Language",
                Font = new Font("Segoe UI Semibold", 15f),
                ForeColor = TG.TextPrimary,
                AutoSize = true,
                Location = new Point(28, 24)
            };

            var subtitle = new Label
            {
                Text = "Choose your app language",
                Font = new Font("Segoe UI", 10f),
                ForeColor = TG.TextSecondary,
                AutoSize = true,
                Location = new Point(28, 54)
            };

            var pnlEnglish = BuildLanguageOption(
                "English",
                "English",
                LanguageType.English,
                110);

            var pnlVietnamese = BuildLanguageOption(
                "Tiếng Việt",
                "Vietnamese",
                LanguageType.Vietnamese,
                180);

            var btnDone = new Button
            {
                Text = "Done",
                FlatStyle = FlatStyle.Flat,
                BackColor = TG.Blue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 11f),
                Size = new Size(100, 36),
                Cursor = Cursors.Hand,
                FlatAppearance = { BorderSize = 0 }
            };
            btnDone.Location = new Point(ClientSize.Width - btnDone.Width - 28, ClientSize.Height - 64);
            btnDone.Click += (_, __) =>
            {
                if (_selectedLanguage != LocalizationService.CurrentLanguage)
                {
                    LocalizationService.SetLanguage(_selectedLanguage);
                }
                DialogResult = DialogResult.OK;
                Close();
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TG.TextPrimary,
                Font = new Font("Segoe UI", 11f),
                Size = new Size(80, 36),
                Cursor = Cursors.Hand,
                FlatAppearance = { BorderSize = 0 }
            };
            btnCancel.Location = new Point(ClientSize.Width - btnDone.Width - btnCancel.Width - 36, ClientSize.Height - 64);
            btnCancel.Click += (_, __) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(pnlEnglish);
            Controls.Add(pnlVietnamese);
            Controls.Add(btnCancel);
            Controls.Add(btnDone);

            UiLocalization.ApplyToForm(this);
        }

        private Panel BuildLanguageOption(string nativeName, string englishName, LanguageType langType, int y)
        {
            var pnl = new Panel
            {
                Size = new Size(ClientSize.Width - 56, 56),
                Location = new Point(28, y),
                BackColor = TG.WindowBg,
                Cursor = Cursors.Hand
            };

            var circle = new Panel
            {
                Size = new Size(24, 24),
                Location = new Point(0, 16),
                BackColor = Color.Transparent
            };
            circle.Paint += (s, e) =>
            {
                bool isActive = _selectedLanguage == langType;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, 23, 23);
                using var p = new Pen(isActive ? TG.Blue : TG.TextSecondary, 2f);
                e.Graphics.DrawEllipse(p, r);
                if (isActive)
                {
                    using var b = new SolidBrush(TG.Blue);
                    e.Graphics.FillEllipse(b, r.X + 5, r.Y + 5, 13, 13);
                }
            };

            var lblNative = new Label
            {
                Text = nativeName,
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = TG.TextPrimary,
                AutoSize = true,
                Location = new Point(36, 4),
                BackColor = Color.Transparent
            };

            var lblEnglish = new Label
            {
                Text = englishName,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = TG.TextSecondary,
                AutoSize = true,
                Location = new Point(36, 28),
                BackColor = Color.Transparent
            };

            void SelectThis()
            {
                if (_selectedLanguage == langType) return;
                _selectedLanguage = langType;

                foreach (Control c in Controls)
                {
                    if (c is Panel optionPnl && optionPnl != pnl)
                    {
                        foreach (Control child in optionPnl.Controls)
                        {
                            if (child is Panel circlePnl)
                            {
                                circlePnl.Invalidate();
                            }
                        }
                    }
                }
                circle.Invalidate();
            }

            pnl.Click += (_, __) => SelectThis();
            circle.Click += (_, __) => SelectThis();
            lblNative.Click += (_, __) => SelectThis();
            lblEnglish.Click += (_, __) => SelectThis();

            pnl.Controls.Add(circle);
            pnl.Controls.Add(lblNative);
            pnl.Controls.Add(lblEnglish);

            pnl.Paint += (s, e) =>
            {
                using var pen = new Pen(TG.Divider);
                e.Graphics.DrawLine(pen, 0, pnl.Height - 1, pnl.Width, pnl.Height - 1);
            };

            return pnl;
        }
    }
}
