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

        var diff = DateTime.UtcNow - lastSeenUtc.Value;

        if (diff.TotalSeconds < 60)
            return LocalizationService.Translate("last seen just now");

        if (diff.TotalMinutes < 60)
        {
            int mins = (int)diff.TotalMinutes;
            var fmt = LocalizationService.Translate("last seen {0}m ago");
            return string.Format(fmt, mins);
        }

        // Use local dates for "today" / "yesterday" boundary to avoid UTC date mismatch
        var localNow = DateTime.Now;
        var localLastSeen = lastSeenUtc.Value.ToLocalTime();
        if (localLastSeen.Date == localNow.Date)
        {
            var fmt = LocalizationService.Translate("last seen today at {0}");
            return string.Format(fmt, localLastSeen.ToString("HH:mm"));
        }

        if (localLastSeen.Date == localNow.Date.AddDays(-1))
        {
            var fmt = LocalizationService.Translate("last seen yesterday at {0}");
            return string.Format(fmt, localLastSeen.ToString("HH:mm"));
        }

        bool isVietnamese = LocalizationService.CurrentLanguage == LanguageType.Vietnamese;
        string formattedDate = isVietnamese
            ? $"{localLastSeen.Day} {LocalizationService.Translate(localLastSeen.ToString("MMMM", System.Globalization.CultureInfo.InvariantCulture))}"
            : localLastSeen.ToString("MMM dd", System.Globalization.CultureInfo.InvariantCulture);
        var fmt2 = LocalizationService.Translate("last seen on {0} at {1}");
        return string.Format(fmt2,
            formattedDate,
            localLastSeen.ToString("HH:mm"));
    }

    public static string GetPresenceText(bool isOnline, DateTime? lastSeenUtc)
        => GetPresenceText(isOnline ? "Online" : "Offline", lastSeenUtc);
}
