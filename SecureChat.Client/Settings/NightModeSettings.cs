using System;
using System.IO;
using System.Text;

namespace SecureChat.Client.Settings
{
    internal static class NightModeSettings
    {
        private const string FileName = "nightmode.config";

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SecureChat", FileName);

        public static bool IsEnabled { get; set; }

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                var text = File.ReadAllText(FilePath, Encoding.UTF8);
                IsEnabled = text.Trim() == "1";
            }
            catch
            {
                IsEnabled = false;
            }
        }

        public static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (dir != null) Directory.CreateDirectory(dir);
                File.WriteAllText(FilePath, IsEnabled ? "1" : "0", Encoding.UTF8);
            }
            catch { }
        }
    }
}
