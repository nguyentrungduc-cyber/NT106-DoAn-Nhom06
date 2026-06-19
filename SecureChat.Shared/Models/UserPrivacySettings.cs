using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureChat.Models
{
	[Table("UserPrivacySettings")]
	public class UserPrivacySettings
	{
		[Key, Column("user_id"), MaxLength(8)]
		public string UserID { get; set; } = "";

		[Required, Column("last_seen_privacy")]
		public PrivacyLevel LastSeenPrivacy { get; set; } = PrivacyLevel.Everybody;

		[Required, Column("profile_photo_privacy")]
		public PrivacyLevel ProfilePhotoPrivacy { get; set; } = PrivacyLevel.Everybody;

		[Required, Column("forwarded_messages_privacy")]
		public PrivacyLevel ForwardedMessagesPrivacy { get; set; } = PrivacyLevel.Everybody;

		[Required, Column("calls_privacy")]
		public PrivacyLevel CallsPrivacy { get; set; } = PrivacyLevel.Everybody;

		[Required, Column("voice_messages_privacy")]
		public PrivacyLevel VoiceMessagesPrivacy { get; set; } = PrivacyLevel.Everybody;

		[Required, Column("messages_privacy")]
		public PrivacyLevel MessagesPrivacy { get; set; } = PrivacyLevel.Everybody;

		[Required, Column("birthday_privacy")]
		public PrivacyLevel BirthdayPrivacy { get; set; } = PrivacyLevel.Everybody;

		[Required, Column("bio_privacy")]
		public PrivacyLevel BioPrivacy { get; set; } = PrivacyLevel.Everybody;

		[Required, Column("auto_delete_mode")]
		public AutoDeleteMode AutoDeleteMode { get; set; } = AutoDeleteMode.Off;

		[Required, Column("updated_at")]
		public DateTime UpdatedAt { get; set; }

		[ForeignKey(nameof(UserID))]
		public User User { get; set; } = null!;
	}
}
