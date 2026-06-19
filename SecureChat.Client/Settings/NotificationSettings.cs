using System;
using System.IO;
using System.Text;

namespace SecureChat.Client.Settings
{
    public class NotificationSettings
    {
        private const string FileName = "notificationsettings.config";

        public static NotificationSettings Default { get; private set; } = Load();

        public bool DesktopNotifications { get; set; } = true;
        public bool FlashTaskbar { get; set; } = true;
        public bool AllowSound { get; set; } = true;
        public int Volume { get; set; } = 100;
        public bool PrivateChatNotifications { get; set; } = true;
        public bool GroupNotifications { get; set; } = true;
        public bool ChannelNotifications { get; set; } = true;
        public bool ContactJoinedNotifications { get; set; } = true;
        public bool PinnedMessageNotifications { get; set; } = true;

        public void Save()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, FileName);
                var data = string.Join("\u001F",
                    DesktopNotifications, FlashTaskbar, AllowSound, Volume,
                    PrivateChatNotifications, GroupNotifications, ChannelNotifications,
                    ContactJoinedNotifications, PinnedMessageNotifications);
                File.WriteAllText(path, data, Encoding.UTF8);
            }
            catch { }
        }

        private static NotificationSettings Load()
        {
            var s = new NotificationSettings();
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, FileName);
                if (!File.Exists(path)) return s;

                var text = File.ReadAllText(path, Encoding.UTF8);
                var parts = text.Split('\u001F');

                if (parts.Length >= 9)
                {
                    if (bool.TryParse(parts[0], out var v1)) s.DesktopNotifications = v1;
                    if (bool.TryParse(parts[1], out var v2)) s.FlashTaskbar = v2;
                    if (bool.TryParse(parts[2], out var v3)) s.AllowSound = v3;
                    if (int.TryParse(parts[3], out var v4)) s.Volume = Math.Clamp(v4, 0, 100);
                    if (bool.TryParse(parts[4], out var v5)) s.PrivateChatNotifications = v5;
                    if (bool.TryParse(parts[5], out var v6)) s.GroupNotifications = v6;
                    if (bool.TryParse(parts[6], out var v7)) s.ChannelNotifications = v7;
                    if (bool.TryParse(parts[7], out var v8)) s.ContactJoinedNotifications = v8;
                    if (bool.TryParse(parts[8], out var v9)) s.PinnedMessageNotifications = v9;
                }
            }
            catch { }

            return s;
        }

        public static void Reload()
        {
            Default = Load();
        }
    }
}
