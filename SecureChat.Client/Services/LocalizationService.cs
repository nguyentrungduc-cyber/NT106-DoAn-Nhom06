using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace SecureChat.Client.Services
{
    public enum LanguageType
    {
        English,
        Vietnamese
    }

    public static class LocalizationService
    {
        private const string ConfigFileName = "language.config";
        private static Dictionary<string, string> _translations = new(StringComparer.Ordinal);
        private static LanguageType _currentLanguage = LanguageType.English;

        public static event Action? LanguageChanged;

        public static LanguageType CurrentLanguage
        {
            get => _currentLanguage;
            private set => _currentLanguage = value;
        }

        private static string ConfigFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SecureChat", ConfigFileName);

        public static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return _translations.TryGetValue(text, out var translated) ? translated : text;
        }

        public static void SetLanguage(LanguageType language)
        {
            if (_currentLanguage == language) return;
            _currentLanguage = language;
            LoadDictionary();
            SaveConfig();
            LanguageChanged?.Invoke();
        }

        public static void Initialize()
        {
            LoadConfig();
            LoadDictionary();
        }

        public static void ApplyToForm(Control root)
        {
            if (root == null) return;
            ApplyRecursive(root);
        }

        public static void ApplyToOpenForms()
        {
            foreach (Form form in Application.OpenForms)
            {
                ApplyToForm(form);
                form.Refresh();
            }
        }

        private static void ApplyRecursive(Control c)
        {
            if (!string.IsNullOrEmpty(c.Text))
            {
                string translated = Translate(c.Text);
                if (translated != c.Text)
                    c.Text = translated;
            }

            foreach (Control child in c.Controls)
            {
                ApplyRecursive(child);
            }
        }

        private static void LoadDictionary()
        {
            _translations.Clear();

            try
            {
                string fileName = CurrentLanguage == LanguageType.Vietnamese ? "vi.json" : "en.json";
                string langDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lang");
                string filePath = Path.Combine(langDir, fileName);
                if (!File.Exists(filePath))
                {
                    langDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Lang");
                    filePath = Path.Combine(langDir, fileName);
                }
                if (!File.Exists(filePath))
                {
                    langDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resources", "Lang"));
                    filePath = Path.Combine(langDir, fileName);
                }
                if (!File.Exists(filePath)) return;

                var json = File.ReadAllText(filePath, Encoding.UTF8);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict != null)
                {
                    foreach (var kvp in dict)
                        _translations[kvp.Key] = kvp.Value;
                }
            }
            catch { }
        }

        private static void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigFilePath)) return;
                var json = File.ReadAllText(ConfigFilePath, Encoding.UTF8);
                var data = JsonSerializer.Deserialize<LanguageConfigData>(json);
                if (data != null)
                    _currentLanguage = data.Language == "vi" ? LanguageType.Vietnamese : LanguageType.English;
            }
            catch { }
        }

        private static void SaveConfig()
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigFilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var data = new LanguageConfigData
                {
                    Language = CurrentLanguage == LanguageType.Vietnamese ? "vi" : "en"
                };
                File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(data), Encoding.UTF8);
            }
            catch { }
        }

        private sealed class LanguageConfigData
        {
            public string Language { get; set; } = "en";
        }
    }
}
