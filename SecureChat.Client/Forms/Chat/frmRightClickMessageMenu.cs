using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SecureChat.Client.Forms.Chat
{
    public class MessageActions
    {
        public Action<string>? Reply { get; init; }
        public Action<string>? Forward { get; init; }
        public Action<string>? Edit { get; init; }
        public Action<string>? Delete { get; init; }
        public Action<string>? Copy { get; init; }
        public Action<string>? Pin { get; init; }
    }

    public class frmRightClickMessageMenu
    {
        /// <summary>
        /// Create a ContextMenuStrip for a message. Optionally provide an icon resolver (label -> Image).
        /// If the resolver returns at least one non-null Image the menu will show the image margin.
        /// </summary>
        public static ContextMenuStrip Create(string messageId, MessageActions actions, Func<string, Image?>? iconFor = null, bool isPinned = false)
        {
            var pinLabel = isPinned ? "Unpin" : "Pin";

            // Known menu labels in the same order as added below
            var labels = new[]
            {
                "Reply",
                "Forward",
                "Copy",
                "Edit",
                pinLabel,
                "Delete"
            };

            // Pre-resolve icons so we can toggle ShowImageMargin only when needed
            var icons = labels.ToDictionary(l => l, l => iconFor?.Invoke(l));

            var menu = new ContextMenuStrip
            {
                ShowImageMargin = icons.Values.Any(i => i != null)
            };

            AddItem(menu, "Reply", actions.Reply, messageId, icons["Reply"]);
            AddItem(menu, "Forward", actions.Forward, messageId, icons["Forward"]);
            AddItem(menu, "Copy", actions.Copy, messageId, icons["Copy"]);
            AddItem(menu, "Edit", actions.Edit, messageId, icons["Edit"]);
            AddItem(menu, pinLabel, actions.Pin, messageId, icons[pinLabel]);

            menu.Items.Add(new ToolStripSeparator());

            var deleteItem = new ToolStripMenuItem("Delete")
            {
                Tag = messageId
            };

            if (icons["Delete"] != null)
            {
                deleteItem.Image = icons["Delete"];
                deleteItem.ImageScaling = ToolStripItemImageScaling.SizeToFit;
            }

            if (actions.Delete != null)
            {
                deleteItem.Click += (_, __) => actions.Delete(messageId);
                deleteItem.ForeColor = Color.Red;
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
                Tag = messageId,
                Enabled = handler != null
            };

            if (icon != null)
            {
                item.Image = icon;
                item.ImageScaling = ToolStripItemImageScaling.SizeToFit;
            }

            if (handler != null)
            {
                item.Click += (_, __) => handler(messageId);
            }

            menu.Items.Add(item);
        }
    }
}
