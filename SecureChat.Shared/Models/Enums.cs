namespace SecureChat.Models
{
	public enum CallType : byte
	{
		Voice = 0,
		Video = 1
	}

	public enum CallStatus : byte
	{
	Ringing = 0,
	Ongoing = 1,
	Ended = 2,
	Failed = 3,
	Missed = 4
	}

	public enum CallParticipantStatus : byte
	{
		Ringing = 0,
		Joined = 1,
		Declined = 2,
		Missed = 3,
		LeftEarly = 4
	}

	public enum ConversationType : byte
	{
		Direct = 0,
		Group = 1,
		SavedMessages = 2
	}

	public enum FriendRequestStatus : byte
	{
		Pending = 0,
		Accepted = 1,
		Declined = 2,
		Cancelled = 3   /* người gửi hủy yêu cầu */
	}

	public enum GroupVisibility : byte { Private = 0, Public = 1 }
	public enum HistoryMode : byte { Visible = 0, Hidden = 1 }

	public enum MemberRole : byte
	{
		Member = 0,
		Moderator = 1,
		Owner = 2
	}

	public enum MessageType : byte
	{
		Text = 0,
		Image = 1,
		Video = 2,
		Audio = 3,
		File = 4,
		Sticker = 5,
		Call = 6,
		SystemNotification = 7
	}

	public enum NotificationMode : byte
	{
		Off = 0,
		MentionsOnly = 1,
		All = 2
	}

	public enum PrivacyLevel : byte
	{
		Everybody = 0,
		Contacts = 1,
		Nobody = 2
	}

	public enum AutoDeleteMode : byte
	{
		Off = 0,
		TwentyFourHours = 1,
		SevenDays = 2,
		ThirtyDays = 3
	}

	public enum UserStatus : byte
	{
		Offline = 0,
		Online = 1,
		Idle = 2
	}
}
