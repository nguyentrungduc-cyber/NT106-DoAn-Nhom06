using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SecureChat.Models
{
	[Table("CallLogs")]
	public class CallLog
	{
		[Key, MaxLength(8), Column("call_id")]
		public string CallID { get; set; } = "";

		[Required, MaxLength(8), Column("conversation_id")]
		public string ConversationID { get; set; } = "";

		[Column("call_type")]
		public CallType Type { get; set; } = CallType.Voice;

		[Column("status")]
		public CallStatus Status { get; set; } = CallStatus.Ringing;

		[Required, MaxLength(8), Column("started_by")]
		public string StartedBy { get; set; } = "";

		[Column("started_at")]
		public DateTime StartedAt { get; set; }

		[Column("ended_at")]
		public DateTime? EndedAt { get; set; }

		[Column("has_history_message")]
		public bool HasHistoryMessage { get; set; }

		[ForeignKey(nameof(ConversationID))]
		public Conversation Conversation { get; set; } = null!;

		[ForeignKey(nameof(StartedBy))]
		public ConversationMember StartedByMember { get; set; } = null!;

		[InverseProperty(nameof(CallParticipant.Call))]
		public ICollection<CallParticipant> Participants { get; set; } = [];
	}

	[Table("CallParticipants")]
	[PrimaryKey(nameof(ParticipantID), nameof(CallID))]
	public class CallParticipant
	{
		[Key, Column("participant_id"), MaxLength(8)]
		public string ParticipantID { get; set; } = "";

		[Required, Column("call_id"), MaxLength(8)]
		public string CallID { get; set; } = "";

		[Column("status")]
		public CallParticipantStatus Status { get; set; } = CallParticipantStatus.Ringing;

		[Column("joined_at")]
		public DateTime? JoinedAt { get; set; }

		[Column("left_at")]
		public DateTime? LeftAt { get; set; }

		[InverseProperty(nameof(CallLog.Participants))]
		public CallLog Call { get; set; } = null!;

		[InverseProperty(nameof(ConversationMember.CallsJoined))]
		public ConversationMember Member { get; set; } = null!;
	}

}
