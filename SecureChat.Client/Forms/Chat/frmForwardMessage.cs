using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SecureChat.Client.Services;

namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmForwardMessage : Form
    {
        public string SelectedConversationId { get; private set; }

        public frmForwardMessage(List<(string Id, string Name, string Preview, string Time, int Unread, bool IsGroup)> convs, string? excludeConversationId = null, string? savedMessagesConvId = null)
        {
            ThemeRefreshHelper.Hook(this);
            Text = "Forward to...";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            BackColor = TG.WindowBg;
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(320, 450);

            var pnlList = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = TG.WindowBg
            };

            var filtered = excludeConversationId is not null
                ? convs.Where(c => c.Id != excludeConversationId).ToList()
                : convs.ToList();

            int y = 0;

            // Add Saved Messages as first row (always visible, even during current conv)
            if (!string.IsNullOrWhiteSpace(savedMessagesConvId))
            {
                var row = new Panel { Height = 56, Cursor = Cursors.Hand, Width = ClientSize.Width };

                var lblIcon = new Label
                {
                    Text = "📁",
                    Font = new Font("Segoe UI", 18f),
                    Size = new Size(40, 40),
                    Location = new Point(16, 8),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };

                var lblName = new Label
                {
                    Text = "Saved Messages",
                    Font = TG.FontSemiBold(10f),
                    Location = new Point(68, 18),
                    AutoSize = true,
                    ForeColor = TG.TextPrimary,
                    BackColor = Color.Transparent
                };

                row.Controls.AddRange(new Control[] { lblIcon, lblName });

                row.MouseEnter += (s, e) => row.BackColor = TG.SidebarHover;
                row.MouseLeave += (s, e) => row.BackColor = TG.WindowBg;
                lblName.MouseEnter += (s, e) => row.BackColor = TG.SidebarHover;
                lblIcon.MouseEnter += (s, e) => row.BackColor = TG.SidebarHover;

                Action onClick = () =>
                {
                    SelectedConversationId = savedMessagesConvId;
                    DialogResult = DialogResult.OK;
                };

                row.Click += (s, e) => onClick();
                lblIcon.Click += (s, e) => onClick();
                lblName.Click += (s, e) => onClick();

                row.Location = new Point(0, y);
                pnlList.Controls.Add(row);
                y += 56;
            }

            foreach (var c in filtered)
            {
                // Skip saved messages conv from the list (already added above)
                if (!string.IsNullOrWhiteSpace(savedMessagesConvId) && c.Id == savedMessagesConvId)
                    continue;

                var row = new Panel { Height = 56, Cursor = Cursors.Hand, Width = ClientSize.Width };

                var avatar = new AvatarControl { Size = new Size(40, 40), Location = new Point(16, 8) };
                avatar.SetName(c.Name);

                var lblName = new Label
                {
                    Text = c.Name,
                    Font = TG.FontSemiBold(10f),
                    Location = new Point(68, 18),
                    AutoSize = true,
                    ForeColor = TG.TextPrimary,
                    BackColor = Color.Transparent
                };

                row.Controls.AddRange(new Control[] { avatar, lblName });

                row.MouseEnter += (s, e) => row.BackColor = TG.SidebarHover;
                row.MouseLeave += (s, e) => row.BackColor = TG.WindowBg;
                lblName.MouseEnter += (s, e) => row.BackColor = TG.SidebarHover;
                avatar.MouseEnter += (s, e) => row.BackColor = TG.SidebarHover;

                Action onClick = () =>
                {
                    SelectedConversationId = c.Id;
                    DialogResult = DialogResult.OK;
                };

                row.Click += (s, e) => onClick();
                avatar.Click += (s, e) => onClick();
                lblName.Click += (s, e) => onClick();

                row.Location = new Point(0, y);
                pnlList.Controls.Add(row);
                y += 56;
            }

            Controls.Add(pnlList);
        }
    }
}