using System;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using SecureChat.Client.Services;

namespace SecureChat.Client.Forms.Settings
{
    internal static class UiLocalization
    {
        private sealed class TextState
        {
            public string BaseText { get; set; } = string.Empty;
        }

        private static readonly ConditionalWeakTable<Control, TextState> BaseTexts = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            LocalizationService.LanguageChanged += OnLanguageChanged;
        }

        private static void OnLanguageChanged()
        {
            ApplyToOpenForms();
        }

        public static void ApplyToForm(Control root)
        {
            if (root == null) return;
            EnsureInitialized();
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

        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                _initialized = true;
                LocalizationService.LanguageChanged += OnLanguageChanged;
            }
        }

        private static void ApplyRecursive(Control c)
        {
            if (c == null) return;

            bool isTranslatable = !(c is Form) || ((Form)c).ControlBox;

            if (isTranslatable && !string.IsNullOrEmpty(c.Text))
            {
                if (!BaseTexts.TryGetValue(c, out var state))
                {
                    state = new TextState { BaseText = c.Text };
                    BaseTexts.Add(c, state);
                }

                if (!string.IsNullOrWhiteSpace(state.BaseText))
                {
                    string translated = LocalizationService.Translate(state.BaseText);
                    if (translated != c.Text)
                        c.Text = translated;
                }
            }

            foreach (Control child in c.Controls)
            {
                ApplyRecursive(child);
            }
        }
    }
}
