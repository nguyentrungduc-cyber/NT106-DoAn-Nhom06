using SecureChat.Client.Services;

namespace SecureChat.Client.Helpers;

public static class PresenceFormatter
{
    public static string GetPresenceText(string status, DateTime? lastSeenUtc)
    {
        if (status == "Online")
            return LocalizationService.Translate("Online");

        if (status == "Idle")
            return LocalizationService.Translate("Idle");

        if (lastSeenUtc is null)
            return LocalizationService.Translate("offline");

        var now = DateTime.UtcNow;

        if (lastSeenUtc.Value > now)
            return LocalizationService.Translate("last seen just now");

        var diff = now - lastSeenUtc.Value;

        if (diff.TotalSeconds < 60)
            return LocalizationService.Translate("last seen just now");

        if (diff.TotalMinutes < 60)
        {
            int mins = (int)diff.TotalMinutes;
            var fmt = LocalizationService.Translate("last seen {0}m ago");
            return string.Format(fmt, mins);
        }

        if (diff.TotalHours < 24 && lastSeenUtc.Value.Date == now.Date)
        {
            var fmt = LocalizationService.Translate("last seen today at {0}");
            return string.Format(fmt, lastSeenUtc.Value.ToLocalTime().ToString("HH:mm"));
        }

        if (diff.TotalHours < 48 && lastSeenUtc.Value.Date == now.Date.AddDays(-1))
        {
            var fmt = LocalizationService.Translate("last seen yesterday at {0}");
            return string.Format(fmt, lastSeenUtc.Value.ToLocalTime().ToString("HH:mm"));
        }

        var localDate = lastSeenUtc.Value.ToLocalTime();
        bool isVietnamese = LocalizationService.CurrentLanguage == LanguageType.Vietnamese;
        string formattedDate = isVietnamese
            ? $"{localDate.Day} {LocalizationService.Translate(localDate.ToString("MMMM", System.Globalization.CultureInfo.InvariantCulture))}"
            : localDate.ToString("MMM dd", System.Globalization.CultureInfo.InvariantCulture);
        var fmt2 = LocalizationService.Translate("last seen on {0} at {1}");
        return string.Format(fmt2,
            formattedDate,
            localDate.ToString("HH:mm"));
    }

    public static string GetPresenceText(bool isOnline, DateTime? lastSeenUtc)
        => GetPresenceText(isOnline ? "Online" : "Offline", lastSeenUtc);
}
