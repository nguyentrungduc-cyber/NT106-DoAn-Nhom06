using System;
using System.IO;
using System.Text;

namespace SecureChat.Client.Settings
{
    public class AdvancedSettings
    {
        private const string FileName = "advancedsettings.config";

        public static AdvancedSettings Default { get; private set; } = Load();

        public int DownloadPathMode { get; set; } = 0;
        public string CustomDownloadPath { get; set; } = string.Empty;
        public bool AskDownloadPathEachFile { get; set; } = true;
        public bool ShowChatName { get; set; } = true;
        public bool TotalUnreadCount { get; set; } = true;
        public bool UseSystemWindowFrame { get; set; } = false;
        public bool ShowTaskbarIcon { get; set; } = true;
        public bool UseMonochromeIcon { get; set; } = true;

        public string ResolveDownloadPath()
        {
            return DownloadPathMode switch
            {
                1 => Path.GetTempPath(),
                2 => !string.IsNullOrWhiteSpace(CustomDownloadPath) ? CustomDownloadPath : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SecureChat Downloads"),
                _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SecureChat Downloads")
            };
        }

        public void Save()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, FileName);
                var data = string.Join("\u001F",
                    DownloadPathMode, CustomDownloadPath, AskDownloadPathEachFile,
                    ShowChatName, TotalUnreadCount, UseSystemWindowFrame,
                    ShowTaskbarIcon, UseMonochromeIcon);
                File.WriteAllText(path, data, Encoding.UTF8);
            }
            catch { }
        }

        private static AdvancedSettings Load()
        {
            var s = new AdvancedSettings();
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, FileName);
                if (!File.Exists(path)) return s;

                var text = File.ReadAllText(path, Encoding.UTF8);
                var parts = text.Split('\u001F');

                if (parts.Length >= 8)
                {
                    if (int.TryParse(parts[0], out var mode)) s.DownloadPathMode = mode;
                    s.CustomDownloadPath = parts[1];
                    if (bool.TryParse(parts[2], out var p1)) s.AskDownloadPathEachFile = p1;
                    if (bool.TryParse(parts[3], out var p2)) s.ShowChatName = p2;
                    if (bool.TryParse(parts[4], out var p3)) s.TotalUnreadCount = p3;
                    if (bool.TryParse(parts[5], out var p4)) s.UseSystemWindowFrame = p4;
                    if (bool.TryParse(parts[6], out var p5)) s.ShowTaskbarIcon = p5;
                    if (bool.TryParse(parts[7], out var p6)) s.UseMonochromeIcon = p6;
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
