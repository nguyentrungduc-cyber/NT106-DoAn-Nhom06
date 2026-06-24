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
        private static ToolTip? _appToolTip;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            LocalizationService.LanguageChanged += OnLanguageChanged;
        }

        public static void SetAppToolTip(ToolTip toolTip)
        {
            _appToolTip = toolTip;
        }

        /// <summary>
        /// Sets a control's text AND registers the English base text for live re-translation.
        /// Use this instead of direct Translate() for any control text set at runtime.
        /// </summary>
        public static void SetTranslatedText(Control c, string englishText)
        {
            if (c == null) return;
            if (!BaseTexts.TryGetValue(c, out var state))
            {
                state = new TextState { BaseText = englishText };
                BaseTexts.Add(c, state);
            }
            else
            {
                state.BaseText = englishText;
            }
            string translated = LocalizationService.Translate(englishText);
            if (translated != c.Text)
                c.Text = translated;
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

            // Re-translate app-wide ToolTip
            if (_appToolTip != null)
            {
                foreach (Form form in Application.OpenForms)
                {
                    TranslateToolTipsForForm(form);
                }
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

            // Translate PlaceholderText if available (TextBox only)
            if (c is TextBox tbox && !string.IsNullOrEmpty(tbox.PlaceholderText))
            {
                string key = tbox.PlaceholderText;
                string translated = LocalizationService.Translate(key);
                if (translated != key)
                    tbox.PlaceholderText = translated;
            }

            // Translate tooltips for this control
            if (_appToolTip != null)
            {
                string tipText = _appToolTip.GetToolTip(c);
                if (!string.IsNullOrEmpty(tipText))
                {
                    string translated = LocalizationService.Translate(tipText);
                    if (translated != tipText)
                        _appToolTip.SetToolTip(c, translated);
                }
            }

            // Translate ContextMenuStrip
            if (c.ContextMenuStrip != null)
                TranslateContextMenu(c.ContextMenuStrip);

            foreach (Control child in c.Controls)
            {
                ApplyRecursive(child);
            }
        }

        private static void TranslateContextMenu(ContextMenuStrip menu)
        {
            if (menu == null) return;
            foreach (ToolStripItem item in menu.Items)
            {
                if (item is ToolStripMenuItem menuItem)
                {
                    if (!string.IsNullOrEmpty(menuItem.Text))
                    {
                        string translated = LocalizationService.Translate(menuItem.Text);
                        if (translated != menuItem.Text)
                            menuItem.Text = translated;
                    }

                    // Handle dropdown items
                    if (menuItem.DropDownItems.Count > 0)
                        TranslateToolStripDropDown(menuItem.DropDownItems);
                }
                else if (!string.IsNullOrEmpty(item.Text))
                {
                    string translated = LocalizationService.Translate(item.Text);
                    if (translated != item.Text)
                        item.Text = translated;
                }
            }
        }

        private static void TranslateToolStripDropDown(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                if (item is ToolStripMenuItem menuItem)
                {
                    if (!string.IsNullOrEmpty(menuItem.Text))
                    {
                        string translated = LocalizationService.Translate(menuItem.Text);
                        if (translated != menuItem.Text)
                            menuItem.Text = translated;
                    }
                    if (menuItem.DropDownItems.Count > 0)
                        TranslateToolStripDropDown(menuItem.DropDownItems);
                }
                else if (!string.IsNullOrEmpty(item.Text))
                {
                    string translated = LocalizationService.Translate(item.Text);
                    if (translated != item.Text)
                        item.Text = translated;
                }
            }
        }

        private static void TranslateToolTipsForForm(Form form)
        {
            if (_appToolTip == null || form == null) return;
            TranslateToolTipsRecursive(form);
        }

        private static void TranslateToolTipsRecursive(Control c)
        {
            string tipText = _appToolTip.GetToolTip(c);
            if (!string.IsNullOrEmpty(tipText))
            {
                string translated = LocalizationService.Translate(tipText);
                if (translated != tipText)
                    _appToolTip.SetToolTip(c, translated);
            }

            foreach (Control child in c.Controls)
            {
                TranslateToolTipsRecursive(child);
            }

            if (c.ContextMenuStrip != null)
                TranslateContextMenu(c.ContextMenuStrip);
        }
    }
}
