using System;
using System.Drawing;
using System.Windows.Forms;

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
                var emojiCopy = emoji;
                // Dùng Panel + custom Paint thay Label để có TextRenderingHint.AntiAlias
                var pnl = new Panel
                {
                    Size = new Size(42, 42),
                    Cursor = Cursors.Hand,
                    BackColor = Color.Transparent,
                };
                pnl.Paint += (s, pe) =>
                {
                    pe.Graphics.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    pe.Graphics.TextRenderingHint  = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    using var f = new Font("Segoe UI Emoji", 18f);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    pe.Graphics.DrawString(emojiCopy, f, Brushes.Black, pnl.ClientRectangle, sf);
                };
                pnl.MouseEnter += (_, __) => { pnl.BackColor = Color.FromArgb(30, 0, 120, 215); pnl.Invalidate(); };
                pnl.MouseLeave += (_, __) => { pnl.BackColor = Color.Transparent; pnl.Invalidate(); };
                pnl.Click += (s, e) =>
                {
                    SelectedReaction = emojiCopy;
                    DialogResult = DialogResult.OK;
                    Close();
                };
                flow.Controls.Add(pnl);
            }

            Controls.Add(flow);
        }
    }
}
