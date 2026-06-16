using Microsoft.EntityFrameworkCore;
using SecureChat.Models;

namespace SecureChat.Repositories
{
	public class ConversationRepository(AppDbContext db)
	{
		/*
		 * CONVERSATION
		 */
		public async Task<Conversation> CreateAsync(Conversation conversation)
		{
			conversation.CreatedAt = DateTime.UtcNow;
			db.Conversations.Add(conversation);
			await db.SaveChangesAsync();
			return conversation;
		}

		public async Task<Conversation?> GetByIdAsync(string conversationID)
			=> await db.Conversations
				.Include(c => c.Creator)
				.Include(c => c.LastMessage)
				.FirstOrDefaultAsync(c => c.ConversationID == conversationID);

		public async Task<Conversation?> GetByIdWithMembersAsync(string conversationID)
			=> await db.Conversations
				.Include(c => c.Creator)
				.Include(c => c.LastMessage)
				.Include(c => c.Members)
					.ThenInclude(m => m.User)
				.FirstOrDefaultAsync(c => c.ConversationID == conversationID);

		public async Task<List<Conversation>> GetByUserAsync(string userID)
			=> await db.Conversations
				.Include(c => c.LastMessage)
				.Include(c => c.Members)
					.ThenInclude(m => m.User)
				.Where(c => c.Members.Any(m => m.UserID == userID && m.LeftAt == null))
				.OrderByDescending(c => c.LastActivityAt)
				.ToListAsync();

		public async Task<Conversation?> GetDirectConversationAsync(string userAID, string userBID)
			=> await db.Conversations
				.Where(c => c.Type == ConversationType.Direct &&
					c.Members.Any(m => m.UserID == userAID && m.LeftAt == null) &&
					c.Members.Any(m => m.UserID == userBID && m.LeftAt == null))
				.FirstOrDefaultAsync();

		public async Task UpdateAsync(Conversation conversation)
		{
			db.Conversations.Update(conversation);
			await db.SaveChangesAsync();
		}

		public async Task SetLastMessageAsync(string conversationID, string messageID, DateTime activityAt)
		{
			var conv = await db.Conversations.FindAsync(conversationID)
				?? throw new KeyNotFoundException($"Không tìm thấy cuộc trò chuyện {conversationID}.");

			conv.LastMessageID   = messageID;
			conv.LastActivityAt  = activityAt;
			await db.SaveChangesAsync();
		}

		public async Task DeleteAsync(string conversationID)
		{
			var conv = await db.Conversations
				.Include(c => c.Members)
				.Include(c => c.PinnedMessages)
				.FirstOrDefaultAsync(c => c.ConversationID == conversationID);
			if (conv is null)
				return;

			// Break SetNull FK references to prevent circular dependency.
			// When both the principal (Message, ConversationMember) and the dependent
			// (Conversation, ConversationMember, MessagePin) are being deleted, EF cannot
			// UPDATE a row (SetNull) and DELETE it in the same SaveChangesAsync().
			//
			// Conflicts fixed:
			//   Conversation.LastMessageID     (SetNull → Message)
			//   ConversationMember.LastReadMsgID (SetNull → Message)
			//   MessagePin.PinnedBy            (SetNull → ConversationMember)
			conv.LastMessageID = null;
			foreach (var m in conv.Members)
				m.LastReadMsgID = null;
			foreach (var p in conv.PinnedMessages)
				p.PinnedBy = null;

			await db.SaveChangesAsync();

			db.Conversations.Remove(conv);
			await db.SaveChangesAsync();
		}

		/*
		 * MEMBERS
		 */

		public async Task<ConversationMember> AddMemberAsync(ConversationMember member)
		{
			member.JoinedAt = DateTime.UtcNow;
			db.ConversationMembers.Add(member);
			await db.SaveChangesAsync();
			return member;
		}

		public async Task<ConversationMember?> GetMemberByIdAsync(string memberID)
			=> await db.ConversationMembers
				.Include(m => m.User)
				.Include(m => m.Conversation)
				.FirstOrDefaultAsync(m => m.MemberID == memberID);

		public async Task<ConversationMember?> GetMemberByConversationAndUserAsync(
			string conversationID, string userID)
			=> await db.ConversationMembers
				.Include(m => m.User)
				.FirstOrDefaultAsync(m => m.ConversationID == conversationID &&
				                          m.UserID == userID);

		public async Task<List<ConversationMember>> GetActiveMembersAsync(string conversationID)
			=> await db.ConversationMembers
				.Include(m => m.User)
				.Where(m => m.ConversationID == conversationID && m.LeftAt == null)
				.OrderBy(m => m.JoinedAt)
				.ToListAsync();

		public async Task<List<ConversationMember>> GetAllMembersAsync(string conversationID)
			=> await db.ConversationMembers
				.Include(m => m.User)
				.Where(m => m.ConversationID == conversationID)
				.OrderBy(m => m.JoinedAt)
				.ToListAsync();

		public async Task<ConversationMember> UpdateRoleAsync(string memberID, MemberRole role)
		{
			var member = await db.ConversationMembers.FindAsync(memberID)
				?? throw new KeyNotFoundException($"Không tìm thấy thành viên {memberID}.");

			member.Role = role;
			await db.SaveChangesAsync();
			return member;
		}

		public async Task<ConversationMember> UpdateNicknameAsync(string memberID, string? nickname)
		{
			var member = await db.ConversationMembers.FindAsync(memberID)
				?? throw new KeyNotFoundException($"Không tìm thấy thành viên {memberID}.");

			member.Nickname = nickname;
			await db.SaveChangesAsync();
			return member;
		}

		public async Task<ConversationMember> UpdateNotificationModeAsync(
			string memberID, NotificationMode mode)
		{
			var member = await db.ConversationMembers.FindAsync(memberID)
				?? throw new KeyNotFoundException($"Không tìm thấy thành viên {memberID}.");

			member.ShowNotifications = mode;
			await db.SaveChangesAsync();
			return member;
		}

		public async Task<ConversationMember> SetLastReadMessageAsync(
			string memberID, string messageID)
		{
			var member = await db.ConversationMembers.FindAsync(memberID)
				?? throw new KeyNotFoundException($"Không tìm thấy thành viên {memberID}.");

			member.LastReadMsgID = messageID;
			await db.SaveChangesAsync();
			return member;
		}

		public async Task<ConversationMember> SetBanAsync(string memberID, DateTime? bannedUntil)
		{
			var member = await db.ConversationMembers.FindAsync(memberID)
				?? throw new KeyNotFoundException($"Không tìm thấy thành viên {memberID}.");

			member.BannedUntil = bannedUntil;
			await db.SaveChangesAsync();
			return member;
		}

		public async Task LeaveMemberAsync(string memberID)
		{
			var member = await db.ConversationMembers.FindAsync(memberID)
				?? throw new KeyNotFoundException($"ConversationMember {memberID} not found.");

			member.LeftAt = DateTime.UtcNow;
			await db.SaveChangesAsync();
		}

		public async Task RemoveMemberAsync(string memberID)
		{
			var member = await db.ConversationMembers.FindAsync(memberID);
			if (member is null)
				return;

			db.ConversationMembers.Remove(member);
			await db.SaveChangesAsync();
		}

		public async Task<ConversationMember> UpdateEncryptedKeyAsync(string memberID, string encryptedKey)
		{
			var member = await db.ConversationMembers.FindAsync(memberID)
				?? throw new KeyNotFoundException($"Không tìm thấy thành viên {memberID}.");

			member.EncryptedKey = encryptedKey;
			await db.SaveChangesAsync();
			return member;
		}
	}
}
