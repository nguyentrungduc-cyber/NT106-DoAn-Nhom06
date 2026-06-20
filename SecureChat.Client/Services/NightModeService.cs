using System;
using System.Linq;
using System.Windows.Forms;
using SecureChat.Client.Settings;

namespace SecureChat.Client.Services
{
    internal static class NightModeService
    {
        public static bool IsEnabled { get; private set; }

        /// <summary>Fired on UI thread after theme switch completes.</summary>
        public static event Action? ThemeChanged;

        public static void Initialize()
        {
            NightModeSettings.Load();
            if (NightModeSettings.IsEnabled)
            {
                TG.ApplyDark();
                IsEnabled = true;
            }
        }

        public static void Toggle()
        {
            if (IsEnabled)
            {
                TG.ApplyLight();
                IsEnabled = false;
            }
            else
            {
                TG.ApplyDark();
                IsEnabled = true;
            }

            NightModeSettings.IsEnabled = IsEnabled;
            NightModeSettings.Save();
            RefreshOpenForms();
            ThemeChanged?.Invoke();
        }

        private static void RefreshOpenForms()
        {
            var forms = Application.OpenForms.Cast<Form>().ToArray();
            foreach (var form in forms)
            {
                if (form is frmMainChat main)
                    main.OnNightModeChanged();
                form.Invalidate(true);
            }
        }
    }
}
