using System;
using System.Drawing;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using SecureChat.Client.Services.Api;

namespace SecureChat.Client.Services
{
    public static class AvatarService
    {
        public static string CurrentUserId { get; private set; } = string.Empty;
        public static string CurrentDisplayName { get; private set; } = string.Empty;
        public static string CurrentUsername { get; private set; } = string.Empty;
        public static string CurrentEmail { get; private set; } = string.Empty;
        public static string CurrentAvatarUrl { get; private set; } = string.Empty;

        public static event Action? CurrentUserChanged;

        internal static void SetCurrentUser(string userId, string displayName, string username, string email, string avatarUrl)
        {
            CurrentUserId = userId;
            CurrentDisplayName = displayName;
            CurrentUsername = username;
            CurrentEmail = email;
            CurrentAvatarUrl = avatarUrl ?? string.Empty;
        }

        public static async Task InitializeAsync()
        {
            try
            {
                var http = ApiClient.Instance.GetHttpClient();
                var res = await http.GetAsync("api/users/me");
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var me = JsonSerializer.Deserialize<DTOs.UserResponse>(json, opts);
                    if (me != null)
                    {
                        CurrentUserId = me.UserID;
                        CurrentDisplayName = me.DisplayName;
                        CurrentUsername = me.Username;
                        CurrentEmail = me.Email;
                        CurrentAvatarUrl = me.AvatarURL ?? string.Empty;
                    }
                }
            }
            catch { }
        }

        public static Image? GetAvatarImage()
        {
            return AvatarCacheService.LoadImage(CurrentAvatarUrl);
        }

        public static void UpdateAvatar(string url)
        {
            if (CurrentAvatarUrl == url) return;
            AvatarCacheService.Invalidate(CurrentAvatarUrl);
            CurrentAvatarUrl = url ?? string.Empty;
            // Pre-cache the new avatar so subscribers get the image immediately
            if (!string.IsNullOrWhiteSpace(url))
                _ = AvatarCacheService.DownloadAsync(url);
            CurrentUserChanged?.Invoke();
        }

        public static void UpdateProfile(string displayName, string username, string email)
        {
            CurrentDisplayName = displayName;
            CurrentUsername = username;
            CurrentEmail = email;
            CurrentUserChanged?.Invoke();
        }
    }
}
