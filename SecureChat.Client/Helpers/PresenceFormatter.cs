namespace SecureChat.Client.Helpers;

public static class PresenceFormatter
{
    public static string GetPresenceText(string status, DateTime? lastSeenUtc)
    {
        if (status == "Online")
            return "Online";

        if (status == "Idle")
            return "Idle";

        if (lastSeenUtc is null)
            return "offline";

        var now = DateTime.UtcNow;

        if (lastSeenUtc.Value > now)
            return "last seen just now";

        var diff = now - lastSeenUtc.Value;

        if (diff.TotalSeconds < 60)
            return "last seen just now";

        if (diff.TotalMinutes < 60)
        {
            int mins = (int)diff.TotalMinutes;
            return $"last seen {mins}m ago";
        }

        if (diff.TotalHours < 24 && lastSeenUtc.Value.Date == now.Date)
            return $"last seen today at {lastSeenUtc.Value.ToLocalTime():HH:mm}";

        if (diff.TotalHours < 48 && lastSeenUtc.Value.Date == now.Date.AddDays(-1))
            return $"last seen yesterday at {lastSeenUtc.Value.ToLocalTime():HH:mm}";

        return $"last seen on {lastSeenUtc.Value.ToLocalTime():MMM dd} at {lastSeenUtc.Value.ToLocalTime():HH:mm}";
    }

    public static string GetPresenceText(bool isOnline, DateTime? lastSeenUtc)
        => GetPresenceText(isOnline ? "Online" : "Offline", lastSeenUtc);
}
