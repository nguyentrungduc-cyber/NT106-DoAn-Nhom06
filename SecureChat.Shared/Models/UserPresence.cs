using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureChat.Models
{
	[Table("UserPresence")]
	public class UserPresence
	{
		[Key, Column("user_id"), MaxLength(8)]
		public string UserID { get; set; } = "";

		[Required, Column("status")]
		public UserStatus Status { get; set; } = UserStatus.Offline;

		[Column("last_seen_utc")]
		public DateTime? LastSeenUtc { get; set; }

		[Required, Column("active_session_count")]
		public int ActiveSessionCount { get; set; } = 0;

		[ForeignKey(nameof(UserID))]
		public User User { get; set; } = null!;
	}
}
