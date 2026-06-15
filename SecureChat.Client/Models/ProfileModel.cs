using System;

namespace SecureChat.Client.Models
{
    public sealed class ProfileModel
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty; // t.me/username (without prefix)
        public string AvatarPath { get; set; } = string.Empty;
        public DateTime? Birthday { get; set; }
        public string StatusText { get; set; } = "online";
    }
}
