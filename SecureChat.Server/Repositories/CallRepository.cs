using Microsoft.EntityFrameworkCore;
using SecureChat.Models;

namespace SecureChat.Repositories
{
	public class CallRepository(AppDbContext db)
	{
		/*
		 * CALLLOGS
		 */

		public async Task<CallLog> CreateCallAsync(CallLog call)
		{
			call.StartedAt = DateTime.UtcNow;
			db.CallLogs.Add(call);
			await db.SaveChangesAsync();
			return call;
		}

		public async Task<CallLog?> GetByIdAsync(string callID)
			=> await db.CallLogs
				.Include(c => c.StartedByMember)
				.ThenInclude(m => m.User)
				.Include(c => c.Conversation)
				.Include(c => c.Participants)
				.ThenInclude(p => p.Member)
				.ThenInclude(m => m.User)
				.FirstOrDefaultAsync(c => c.CallID == callID);

		public async Task<List<CallLog>> GetByConversationAsync(string conversationID, int limit = 20)
			=> await db.CallLogs
				.Include(c => c.StartedByMember)
				.ThenInclude(m => m.User)
				.Include(c => c.Participants)
				.Where(c => c.ConversationID == conversationID)
				.OrderByDescending(c => c.StartedAt)
				.Take(limit)
				.ToListAsync();

		public async Task<CallLog?> GetActiveCallAsync(string conversationID)
		{
			var now = DateTime.UtcNow;
			return await db.CallLogs
				.Include(c => c.Participants)
				.FirstOrDefaultAsync(c => c.ConversationID == conversationID
						&& ((c.Status == CallStatus.Ringing && c.StartedAt >= now.AddMinutes(-2))
						 || (c.Status == CallStatus.Ongoing  && c.StartedAt >= now.AddHours(-4))));
		}

		public async Task<CallLog?> GetActiveCallByMemberAsync(string memberID)
		{
			var now = DateTime.UtcNow;
			return await db.CallLogs
				.Include(c => c.Participants)
				.Where(c => c.Status == CallStatus.Ringing || c.Status == CallStatus.Ongoing)
				.Where(c => c.Participants.Any(p =>
					p.ParticipantID == memberID &&
					p.Status != CallParticipantStatus.LeftEarly &&
					p.Status != CallParticipantStatus.Declined &&
					p.Status != CallParticipantStatus.Missed))
				.Where(c => c.Status == CallStatus.Ringing
					? c.StartedAt >= now.AddMinutes(-2)
					: c.StartedAt >= now.AddHours(-4))
				.FirstOrDefaultAsync();
		}

		public async Task<CallLog> UpdateStatusAsync(string callID, CallStatus status)
		{
			var call = await db.CallLogs.FindAsync(callID)
				?? throw new KeyNotFoundException($"Không tìm thấy call_log {callID}.");

			call.Status = status;
			await db.SaveChangesAsync();
			return call;
		}

		public async Task<CallLog> EndCallAsync(string callID)
		{
			var call = await db.CallLogs.FindAsync(callID)
				?? throw new KeyNotFoundException($"Không tìm thấy call_log {callID}.");

			call.Status = CallStatus.Ended;
			call.EndedAt = DateTime.UtcNow;

			// Mark all non-left participants as LeftEarly so they don't block future calls
			var activeParticipants = await db.CallParticipants
				.Where(p => p.CallID == callID
					&& p.Status != CallParticipantStatus.LeftEarly
					&& p.Status != CallParticipantStatus.Declined
					&& p.Status != CallParticipantStatus.Missed)
				.ToListAsync();

			foreach (var p in activeParticipants)
			{
				p.Status = CallParticipantStatus.LeftEarly;
				p.LeftAt ??= DateTime.UtcNow;
			}

			await db.SaveChangesAsync();
			return call;
		}

		public async Task<CallLog> MarkCallAsMissedAsync(string callID)
		{
			var call = await db.CallLogs.FindAsync(callID)
				?? throw new KeyNotFoundException($"Không tìm thấy call_log {callID}.");

			call.Status = CallStatus.Missed;
			call.EndedAt = DateTime.UtcNow;

			var ringingParticipants = await db.CallParticipants
				.Where(p => p.CallID == callID && p.Status == CallParticipantStatus.Ringing)
				.ToListAsync();

			foreach (var p in ringingParticipants)
			{
				p.Status = CallParticipantStatus.Missed;
				p.LeftAt ??= DateTime.UtcNow;
			}

			// Mark the caller as LeftEarly if they never joined (shouldn't happen, but safety)
			var caller = await db.CallParticipants
				.FirstOrDefaultAsync(p => p.CallID == callID && p.Status == CallParticipantStatus.Joined);
			if (caller != null)
			{
				caller.Status = CallParticipantStatus.LeftEarly;
				caller.LeftAt ??= DateTime.UtcNow;
			}

			await db.SaveChangesAsync();
			return call;
		}

		public async Task<int> GetActiveParticipantCountAsync(string callID)
			=> await db.CallParticipants
				.CountAsync(p => p.CallID == callID
					&& p.Status != CallParticipantStatus.LeftEarly
					&& p.Status != CallParticipantStatus.Declined
					&& p.Status != CallParticipantStatus.Missed);

		public async Task DeleteCallAsync(string callID)
		{
			var call = await db.CallLogs.FindAsync(callID);
			if (call is null)
				return;

			db.CallLogs.Remove(call);
			await db.SaveChangesAsync();
		}

		/*
		 * PARTICIPANTS
		 */

		public async Task<CallParticipant> AddParticipantAsync(CallParticipant participant)
		{
			db.CallParticipants.Add(participant);
			await db.SaveChangesAsync();
			return participant;
		}

		public async Task<CallParticipant?> GetParticipantAsync(string participantID, string callID)
			=> await db.CallParticipants
				.Include(p => p.Member)
				.ThenInclude(m => m.User)
				.FirstOrDefaultAsync(p => p.ParticipantID == participantID &&
						p.CallID == callID);

		public async Task<List<CallParticipant>> GetParticipantsByCallAsync(string callID)
			=> await db.CallParticipants
				.Include(p => p.Member)
				.ThenInclude(m => m.User)
				.Where(p => p.CallID == callID)
				.OrderBy(p => p.JoinedAt)
				.ToListAsync();

		public async Task<List<CallParticipant>> GetCallsByParticipantAsync(string participantID)
			=> await db.CallParticipants
				.Include(p => p.Call)
				.ThenInclude(c => c.Conversation)
				.Where(p => p.ParticipantID == participantID)
				.OrderByDescending(p => p.Call.StartedAt)
				.ToListAsync();

		public async Task<CallParticipant> UpdateParticipantStatusAsync(string participantID, string callID, CallParticipantStatus status)
		{
			var participant = await db.CallParticipants.FindAsync(participantID, callID)
				?? throw new KeyNotFoundException($"Không tìm thấy người tham gia {participantID}/{callID}.");

			participant.Status = status;
			await db.SaveChangesAsync();
			return participant;
		}

		public async Task<CallParticipant> JoinCallAsync(string participantID, string callID)
		{
			var participant = await db.CallParticipants.FindAsync(participantID, callID)
				?? throw new KeyNotFoundException($"Không tìm thấy người tham gia {participantID}/{callID}.");

			participant.Status = CallParticipantStatus.Joined;
			participant.JoinedAt = DateTime.UtcNow;
			await db.SaveChangesAsync();
			return participant;
		}

		public async Task<CallParticipant> LeaveCallAsync( string participantID, string callID, CallParticipantStatus status = CallParticipantStatus.LeftEarly)
		{
			var participant = await db.CallParticipants.FindAsync(participantID, callID)
				?? throw new KeyNotFoundException( $"Không tìm thấy người tham gia {participantID}/{callID}.");

			participant.Status = status;
			participant.LeftAt = DateTime.UtcNow;
			await db.SaveChangesAsync();
			return participant;
		}
	}
}
