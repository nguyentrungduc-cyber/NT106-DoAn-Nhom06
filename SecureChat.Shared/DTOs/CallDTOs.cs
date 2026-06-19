using System.ComponentModel.DataAnnotations;
using SecureChat.Models;

namespace SecureChat.DTOs
{
	public record StartCallRequest(
		[Required] CallType Type
	);

	public record UpdateCallStatusRequest(
		[Required] CallStatus Status
	);

	public record UpdateParticipantStatusRequest(
		[Required] CallParticipantStatus Status
	);

	public record LeaveCallRequest(
		CallParticipantStatus Status = CallParticipantStatus.LeftEarly
	);

	public record CallResponse(
		string CallID,
		string ConversationID,
		string? CallerName,
		CallType Type,
		CallStatus Status,
		string StartedBy,
		DateTime StartedAt,
		DateTime? EndedAt,
		List<ParticipantResponse>? Participants
	)
	{
		public static CallResponse From(CallLog c) => new(
			c.CallID, c.ConversationID, c.StartedByMember?.User.Username,
			c.Type, c.Status, c.StartedBy, c.StartedAt, c.EndedAt,
			c.Participants.Select(ParticipantResponse.From).ToList());
	}

	public record ParticipantResponse(
		string ParticipantID,
		string CallID,
		string? Username,
		CallParticipantStatus Status,
		DateTime? JoinedAt,
		DateTime? LeftAt
	)
	{
		public static ParticipantResponse From(CallParticipant p) => new(
			p.ParticipantID, p.CallID,
			p.Member?.User.Username,
			p.Status, p.JoinedAt, p.LeftAt);
	}
}
