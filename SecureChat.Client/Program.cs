using System.Windows.Forms;
using System.IO;
using SecureChat.Client.Forms.Settings;
using SecureChat.Client.Models;
using SecureChat.Client.Services;

namespace SecureChat.Client
{
    // SecureChat.Client/Program.cs
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Debug flag (can be controlled via environment variable SECURECHAT_CLIENT_DEBUG=1)
            bool enableDebugLogging = false;
            try
            {
                var env = Environment.GetEnvironmentVariable("SECURECHAT_CLIENT_DEBUG");
                if (!string.IsNullOrWhiteSpace(env) && (env == "1" || env.Equals("true", StringComparison.OrdinalIgnoreCase)))
                    enableDebugLogging = true;
            }
            catch { /* ignore */ }

            void Log(string msg)
            {
                if (!enableDebugLogging) return;
                try { System.Diagnostics.Debug.WriteLine(msg); } catch { }
                try { Console.WriteLine(msg); } catch { }
            }

            Log("[CLIENT] Application starting");

            // Initialize localization service
            LocalizationService.Initialize();
            UiLocalization.Initialize();

            // 1. UI thread exceptions
            Application.ThreadException += new ThreadExceptionEventHandler(UIThreadException);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            // 2. Non-UI threads
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            // 3. Task scheduler exceptions
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                LogException(e.Exception);
                e.SetObserved();
            };

            ApplicationConfiguration.Initialize();

            // Monitor open forms for debug logging (manual-only, no automation)
            var seenForms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var formsTimer = new System.Windows.Forms.Timer { Interval = 500 };
            formsTimer.Tick += (_, __) =>
            {
                if (!enableDebugLogging) return;
                try
                {
                    var names = Application.OpenForms.Cast<Form>().Select(f => f.Name).ToList();
                    // New forms
                    foreach (var n in names)
                    {
                        if (!seenForms.Contains(n))
                        {
                            seenForms.Add(n);
                            Log($"[CLIENT] Form opened: {n}");
                        }
                    }
                    // Closed forms
                    var removed = seenForms.Except(names).ToList();
                    foreach (var r in removed)
                    {
                        seenForms.Remove(r);
                        Log($"[CLIENT] Form closed: {r}");
                    }
                }
                catch { }
            };
            formsTimer.Start();

            Application.ApplicationExit += (_, __) =>
            {
                SecureChat.Shared.Security.KeyManager.Clear();
                Log("[CLIENT] Application exiting");
            };

            // Launch login form (manual flow). Do not automate any actions.
            Log("[CLIENT] Opening login form");
            var loginForm = new frmLoginRegister();
            loginForm.Shown += (_, __) => Log("[CLIENT] Login form shown");
            loginForm.FormClosed += (_, __) => Log("[CLIENT] Login form closed");
            Application.Run(loginForm);
        }

        private static void UIThreadException(object sender, ThreadExceptionEventArgs e)
        {
            LogException(e.Exception);
            MessageBox.Show("An error occurred in the UI system. Please try again.", "System Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException((Exception)e.ExceptionObject);
            MessageBox.Show("The application encountered a critical error and needs to restart.", "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
        }

        private static void LogException(Exception ex)
        {
            // In đầy đủ message + stack trace ra Debug Output và Console để dễ truy lỗi
            string fullLog = $"[ERROR] {DateTime.Now}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
            if (ex.InnerException is not null)
                fullLog += $"\n--- Inner Exception ---\n{ex.InnerException.GetType().Name}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";

            Console.WriteLine(fullLog);
            try { System.Diagnostics.Debug.WriteLine(fullLog); } catch { }

            // Ghi ra file để xem được khi chạy .exe trực tiếp (không có console/Debug Output)
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "client_error.log");
                File.AppendAllText(logPath, fullLog + "\n\n========================================\n\n");
            }
            catch { /* không để lỗi log làm crash app */ }
        }
    }
}
