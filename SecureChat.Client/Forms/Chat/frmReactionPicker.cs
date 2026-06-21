using System;
using System.Drawing;
using System.Windows.Forms;
using SecureChat.Client.Services;

namespace SecureChat.Client.Forms.Chat
{
    public class frmReactionPicker : Form
    {
        public string? SelectedReaction { get; private set; }

        private static readonly string[] Emojis = {
            "\u2764\ufe0f",  // ❤️
            "\U0001f60d",    // 😍
            "\U0001f604",    // 😄
            "\U0001f622",    // 😢
            "\U0001f620",    // 😠
            "\U0001f44d",    // 👍
            "\U0001f44e",    // 👎
            "\U0001f4af",    // 💯
            "\U0001f525",    // 🔥
            "\U0001f389",    // 🎉
            "\U0001f60e",    // 😎
            "\U0001f914",    // 🤔
            "\U0001f602",    // 😂
            "\U0001f44c",    // 👌
            "\U0001f64f",    // 🙏
            "\U0001f48b",    // 💋
            "\U0001f4aa",    // 💪
            "\U0001f451",    // 👑
            "\U0001f31f",    // 🌟
            "\u2615",        // ☕
        };

        public frmReactionPicker()
        {
            NightModeService.ThemeChanged += OnThemeChanged;
            FormClosed += (_, __) => NightModeService.ThemeChanged -= OnThemeChanged;
            Text = "Pick an emoji";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            Size = new Size(440, 200);

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(6),
                AutoSize = false,
                AutoScroll = true,
                WrapContents = true,
            };

            foreach (var emoji in Emojis)
            {
                // Label renders emoji correctly via Graphics.DrawString (Button uses TextRenderer which shows squares)
                var lbl = new Label
                {
                    Text = emoji,
                    Font = new Font("Segoe UI Emoji", 18f, FontStyle.Regular),
                    Size = new Size(42, 42),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Cursor = Cursors.Hand,
                };
                lbl.Click += (s, e) =>
                {
                    SelectedReaction = emoji;
                    DialogResult = DialogResult.OK;
                    Close();
                };
                flow.Controls.Add(lbl);
            }

            Controls.Add(flow);
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
