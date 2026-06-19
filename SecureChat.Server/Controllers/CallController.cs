using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SecureChat.DTOs;
using SecureChat.Models;
using SecureChat.Repositories;
using SecureChat.Server.Hubs;

namespace SecureChat.Controllers
{
	[Authorize]
	[ApiController]
	[Route("api/conversations/{conversationID}/calls")]
	public class CallController(
		CallRepository calls,
		ConversationRepository conversations,
		MessageRepository messages,
		IHubContext<ChatHub> hubContext) : BaseController
	{
		string Me => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

		async Task<ConversationMember?> GetActiveMember(string conversationID)
			=> (await conversations.GetMemberByConversationAndUserAsync(conversationID, Me)) is { LeftAt: null } m ? m : null;

		[HttpGet]
		public async Task<IActionResult> GetCallHistory(string conversationID, [FromQuery] int limit = 20)
		{
			if (await GetActiveMember(conversationID) is null)
				return Forbid();
			var list = await calls.GetByConversationAsync(conversationID, limit);
			return Ok(list.Select(CallResponse.From));
		}

		[HttpGet("active")]
		public async Task<IActionResult> GetActiveCall(string conversationID)
		{
			if (await GetActiveMember(conversationID) is null)
				return Forbid();
			var call = await calls.GetActiveCallAsync(conversationID);
			if (call is null)
				return NotFound(new { error = "Không có cuộc gọi đang diễn ra." });
			return Ok(CallResponse.From(call));
		}

		[HttpGet("{callID}")]
		public async Task<IActionResult> GetCall(string conversationID, string callID)
		{
			if (await GetActiveMember(conversationID) is null)
				return Forbid();
			var call = await calls.GetByIdAsync(callID);
			if (call is null || call.ConversationID != conversationID)
				return NotFound();
			return Ok(CallResponse.From(call));
		}

		[HttpPost]
		public async Task<IActionResult> StartCall(string conversationID, [FromBody] StartCallRequest req)
		{
			var member = await GetActiveMember(conversationID);
			if (member is null)
				return Forbid();

			var existing = await calls.GetActiveCallAsync(conversationID);
			if (existing is not null)
				return Conflict(new { error = "Đã có cuộc gọi đang diễn ra trong cuộc hội thoại này." });

			var existingUserCall = await calls.GetActiveCallByMemberAsync(member.MemberID);
			if (existingUserCall is not null)
				return Conflict(new { error = "Bạn đang có cuộc gọi trong cuộc hội thoại khác." });

			var call = await calls.CreateCallAsync(new CallLog {
				CallID = NewID(),
				ConversationID = conversationID,
				StartedBy = member.MemberID,
				Type = req.Type,
				Status = CallStatus.Ringing
			});

			await calls.AddParticipantAsync(new CallParticipant {
				ParticipantID = member.MemberID,
				CallID = call.CallID,
				Status = CallParticipantStatus.Joined,
				JoinedAt = DateTime.UtcNow
			});

			var activeMembers = await conversations.GetActiveMembersAsync(conversationID);

			foreach (var m in activeMembers.Where(m => m.MemberID != member.MemberID))
				await calls.AddParticipantAsync(new CallParticipant {
					ParticipantID = m.MemberID,
					CallID        = call.CallID,
					Status        = CallParticipantStatus.Ringing
				});

			var loaded = await calls.GetByIdAsync(call.CallID);
			return CreatedAtAction(nameof(GetCall), new { conversationID, callID = call.CallID }, CallResponse.From(loaded!));
		}

		[HttpPut("{callID}/status")]
		public async Task<IActionResult> UpdateCallStatus(string conversationID, string callID, [FromBody] UpdateCallStatusRequest req)
		{
			var member = await GetActiveMember(conversationID);
			if (member is null)
				return Forbid();

			var call = await calls.GetByIdAsync(callID);
			if (call is null || call.ConversationID != conversationID)
				return NotFound();
			if (call.StartedBy != member.MemberID)
				return Forbid();

			if (req.Status == CallStatus.Ended) {
				var ended = await calls.EndCallAsync(callID);
				var endedLoaded = await calls.GetByIdAsync(callID);
				return Ok(CallResponse.From(endedLoaded!));
			}

			var updated = await calls.UpdateStatusAsync(callID, req.Status);
			var loaded  = await calls.GetByIdAsync(callID);
			return Ok(CallResponse.From(loaded!));
		}

		[HttpPost("{callID}/join")]
		public async Task<IActionResult> JoinCall(string conversationID, string callID)
		{
			var member = await GetActiveMember(conversationID);
			if (member is null)
				return Forbid();

			var existingUserCall = await calls.GetActiveCallByMemberAsync(member.MemberID);
			if (existingUserCall is not null && existingUserCall.CallID != callID)
				return Conflict(new { error = "Bạn đang có cuộc gọi trong cuộc hội thoại khác." });

			var call = await calls.GetByIdAsync(callID);
			if (call is null || call.ConversationID != conversationID)
				return NotFound();
			if (call.Status != CallStatus.Ringing && call.Status != CallStatus.Ongoing)
				return BadRequest(new { error = "Cuộc gọi không còn hoạt động." });

			try {
				var participant = await calls.JoinCallAsync(member.MemberID, callID);
				if (call.Status == CallStatus.Ringing)
					await calls.UpdateStatusAsync(callID, CallStatus.Ongoing);

				return Ok(new ParticipantResponse(participant.ParticipantID, participant.CallID,
							null, participant.Status, participant.JoinedAt, participant.LeftAt));
			} catch (KeyNotFoundException) {
				return NotFound(new { error = "Bạn không trong danh sách cuộc gọi này." });
			}
		}

		[HttpPost("{callID}/end")]
		public async Task<IActionResult> EndCall(string conversationID, string callID)
		{
			var member = await GetActiveMember(conversationID);
			if (member is null)
				return Forbid();

			var call = await calls.GetByIdAsync(callID);
			if (call is null || call.ConversationID != conversationID)
				return NotFound();

			var preEndStatus = call.Status;
			var hasDeclined = call.Participants?.Any(p => p.Status == CallParticipantStatus.Declined) ?? false;

			await calls.EndCallAsync(callID);

			if (preEndStatus == CallStatus.Ongoing)
			{
				var ended = await calls.GetByIdAsync(callID) ?? call;
				await CreateCallSystemMessageAsync(ended, missed: false);
			}
			else if (preEndStatus == CallStatus.Ringing && hasDeclined)
			{
				var ended = await calls.GetByIdAsync(callID) ?? call;
				await CreateCallSystemMessageAsync(ended, missed: true);
			}
			// Ringing without decline → caller cancelled before answer → no message

			return NoContent();
		}

		[HttpPost("{callID}/leave")]
		public async Task<IActionResult> LeaveCall(string conversationID, string callID, [FromBody] LeaveCallRequest? req = null)
		{
			var member = await GetActiveMember(conversationID);
			if (member is null)
				return Forbid();

			var call = await calls.GetByIdAsync(callID);
			if (call is null || call.ConversationID != conversationID)
				return NotFound();

			var status = req?.Status ?? CallParticipantStatus.LeftEarly;

			try {
				await calls.LeaveCallAsync(member.MemberID, callID, status);
			} catch (KeyNotFoundException) {
				return NotFound();
			}

			// Private call: leaving ends the entire call
			if (call.Conversation?.Type == ConversationType.Direct)
			{
				var preEndStatus = call.Status;
				var hasDeclined = call.Participants?.Any(p => p.Status == CallParticipantStatus.Declined) ?? false;

				await calls.EndCallAsync(callID);

				if (preEndStatus == CallStatus.Ongoing)
				{
					var ended = await calls.GetByIdAsync(callID) ?? call;
					await CreateCallSystemMessageAsync(ended, missed: false);
				}
				else if (preEndStatus == CallStatus.Ringing && hasDeclined)
				{
					var ended = await calls.GetByIdAsync(callID) ?? call;
					await CreateCallSystemMessageAsync(ended, missed: true);
				}
				// Ringing without decline → caller left without answer → no message
			}
			else
			{
				// Group call: auto-end when no active participants remain
				var activeCount = await calls.GetActiveParticipantCountAsync(callID);
				if (activeCount == 0)
				{
					var preEndStatus = call.Status;

					await calls.EndCallAsync(callID);

					if (preEndStatus == CallStatus.Ongoing)
					{
						var ended = await calls.GetByIdAsync(callID) ?? call;
						await CreateCallSystemMessageAsync(ended, missed: false, isGroup: true);
					}
					// Ringing group call with no active participants → no message
				}
			}

			return NoContent();
		}

		private async Task CreateCallSystemMessageAsync(CallLog call, bool missed, bool isGroup = false)
		{
			var callTypeName = call.Type == CallType.Video ? "video" : "voice";
			string content;

			if (isGroup)
			{
				content = "Group call ended";
			}
			else if (missed)
			{
				content = $"Missed {callTypeName} call";
			}
			else
			{
				var duration = call.EndedAt.HasValue && call.StartedAt != default
					? (int)(call.EndedAt.Value - call.StartedAt).TotalSeconds
					: 0;
				content = duration > 0
					? $"{callTypeName} call — {duration}s"
					: $"{callTypeName} call";
			}

			var sysMsg = new Message
			{
				MessageID = NewID(),
				ConversationID = call.ConversationID,
				SenderID = null,
				Type = MessageType.Call,
				Content = content
			};

			// Atomic: UPDATE flag + INSERT message in single transaction
			if (!await calls.TryCreateCallHistoryMessageAsync(call.CallID, sysMsg))
				return;

			var response = MessageResponse.From(sysMsg);

			await hubContext.Clients
				.Group(call.ConversationID)
				.SendAsync("MessageReceived", response);
		}

		[HttpPut("{callID}/participants/{participantID}")]
		public async Task<IActionResult> UpdateParticipant(string conversationID, string callID, string participantID, [FromBody] UpdateParticipantStatusRequest req)
		{
			var member = await GetActiveMember(conversationID);
			if (member is null)
				return Forbid();
			var call = await calls.GetByIdAsync(callID);
			if (call is null || call.ConversationID != conversationID)
				return NotFound();
			if (call.StartedBy != member.MemberID && member.MemberID != participantID)
				return Forbid();

			var participant = await calls.UpdateParticipantStatusAsync(participantID, callID, req.Status);

			return Ok(new ParticipantResponse(participant.ParticipantID, participant.CallID,
						null, participant.Status, participant.JoinedAt, participant.LeftAt));
		}
	}
}
