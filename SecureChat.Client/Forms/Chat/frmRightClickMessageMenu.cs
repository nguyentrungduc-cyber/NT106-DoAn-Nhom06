using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SecureChat.Client.Services;

namespace SecureChat.Client.Forms.Chat
{
    public class MessageActions
    {
        public Action<string>? Reply { get; init; }
        public Action<string>? Forward { get; init; }
        public Action<string>? Edit { get; init; }
        public Action<string>? Recall { get; init; }
        public Action<string>? Delete { get; init; }
        public Action<string>? Copy { get; init; }
        public Action<string>? Pin { get; init; }
    }

    public class frmRightClickMessageMenu
    {
        public static ContextMenuStrip Create(string messageId, MessageActions actions, Func<string, Image?>? iconFor = null, bool isPinned = false)
        {
            var pinLabel = isPinned ? "Unpin" : "Pin";
            var pinLabelLoc = LocalizationService.Translate(pinLabel);

            var labels = new[]
            {
                LocalizationService.Translate("Reply"),
                LocalizationService.Translate("Forward"),
                LocalizationService.Translate("Copy"),
                LocalizationService.Translate("Edit"),
                LocalizationService.Translate("Recall"),
                pinLabelLoc,
                LocalizationService.Translate("Delete")
            };

            var labelsKeys = new[] { "Reply", "Forward", "Copy", "Edit", "Recall", pinLabel, "Delete" };
            var icons = labelsKeys.ToDictionary(l => l, l => iconFor?.Invoke(l));

            var menu = new ContextMenuStrip
            {
                ShowImageMargin = icons.Values.Any(i => i != null),
                BackColor       = TG.SidebarBg,
                ForeColor       = TG.TextPrimary,
                Font            = new Font("Segoe UI", 9.5f),
                Renderer        = new ToolStripProfessionalRenderer(new MessageMenuColorTable()),
            };

            AddItem(menu, labels[0], actions.Reply,   messageId, icons["Reply"]);
            AddItem(menu, labels[1], actions.Forward, messageId, icons["Forward"]);
            AddItem(menu, labels[2], actions.Copy,    messageId, icons["Copy"]);
            AddItem(menu, labels[3], actions.Edit,    messageId, icons["Edit"]);
            AddRecallItem(menu, labels[4], actions.Recall, messageId, icons["Recall"]);
            AddItem(menu, pinLabelLoc,  actions.Pin,     messageId, icons[pinLabel]);

            menu.Items.Add(new ToolStripSeparator());

            var deleteItem = new ToolStripMenuItem(LocalizationService.Translate("Delete")) { Tag = messageId };
            if (icons["Delete"] != null)
            {
                deleteItem.Image        = icons["Delete"];
                deleteItem.ImageScaling = ToolStripItemImageScaling.SizeToFit;
            }
            if (actions.Delete != null)
            {
                deleteItem.Click    += (_, __) => actions.Delete(messageId);
                deleteItem.ForeColor = Color.FromArgb(0xE2, 0x4B, 0x4A);
            }
            else
            {
                deleteItem.Enabled = false;
            }
            menu.Items.Add(deleteItem);

            return menu;
        }

        private static void AddItem(ContextMenuStrip menu, string text, Action<string>? handler, string messageId, Image? icon)
        {
            var item = new ToolStripMenuItem(text)
            {
                Tag      = messageId,
                Enabled  = handler != null,
                ForeColor = TG.TextPrimary,
            };
            if (icon != null) { item.Image = icon; item.ImageScaling = ToolStripItemImageScaling.SizeToFit; }
            if (handler != null) item.Click += (_, __) => handler(messageId);
            menu.Items.Add(item);
        }

        private static void AddRecallItem(ContextMenuStrip menu, string text, Action<string>? handler, string messageId, Image? icon)
        {
            var item = new ToolStripMenuItem(text)
            {
                Tag       = messageId,
                Enabled   = handler != null,
                ForeColor = Color.FromArgb(0xE2, 0x4B, 0x4A),
            };
            if (icon != null) { item.Image = icon; item.ImageScaling = ToolStripItemImageScaling.SizeToFit; }
            if (handler != null) item.Click += (_, __) => handler(messageId);
            menu.Items.Add(item);
        }

        // ColorTable tự động dùng TG tokens — dark/light đều đúng
        private sealed class MessageMenuColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected            => TG.SidebarHover;
            public override Color MenuItemBorder              => TG.Divider;
            public override Color ToolStripDropDownBackground  => TG.SidebarBg;
            public override Color SeparatorDark               => TG.Divider;
            public override Color SeparatorLight              => TG.Divider;
            public override Color ImageMarginGradientBegin    => TG.SidebarBg;
            public override Color ImageMarginGradientMiddle   => TG.SidebarBg;
            public override Color ImageMarginGradientEnd      => TG.SidebarBg;
        }
    }
}
