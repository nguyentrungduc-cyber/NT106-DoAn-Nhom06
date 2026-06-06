using Microsoft.EntityFrameworkCore;

namespace SecureChat.Models
{
	public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
	{
		public DbSet<User>		Users			=> Set<User>();
		public DbSet<UserSession>	UserSessions		=> Set<UserSession>();
		public DbSet<Friend>		Friends			=> Set<Friend>();
		public DbSet<FriendRequest>	FriendRequests		=> Set<FriendRequest>();
		public DbSet<BlockedUser>       BlockedUsers		=> Set<BlockedUser>();
		public DbSet<Conversation>      Conversations		=> Set<Conversation>();
		public DbSet<ConversationMember>ConversationMembers	=> Set<ConversationMember>();
		public DbSet<Message>		Messages		=> Set<Message>();
		public DbSet<MessageAttachment>	MessageAttachments	=> Set<MessageAttachment>();
		public DbSet<MessagePin>	MessagePins		=> Set<MessagePin>();
		public DbSet<MessageReaction>   MessageReactions	=> Set<MessageReaction>();
		public DbSet<MessageStatus>     MessageStatuses		=> Set<MessageStatus>();
		public DbSet<MessageMention>    MessageMentions		=> Set<MessageMention>();
		public DbSet<CallLog>           CallLogs		=> Set<CallLog>();
		public DbSet<CallParticipant>   CallParticipants	=> Set<CallParticipant>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			ConfigureRelationships(modelBuilder);
			ConfigureUniqueIndexes(modelBuilder);
			ConfigureNonUniqueIndexes(modelBuilder);
			ConfigureDefaultValues(modelBuilder);
		}

		private static void ConfigureRelationships(ModelBuilder m)
		{
			m.Entity<Friend>()
				.HasOne(f => f.UserA)
				.WithMany(u => u.FriendshipsA)
				.HasForeignKey(f => f.UserAID)
				.OnDelete(DeleteBehavior.Cascade);

			m.Entity<Friend>()
				.HasOne(f => f.UserB)
				.WithMany(u => u.FriendshipsB)
				.HasForeignKey(f => f.UserBID)
				.OnDelete(DeleteBehavior.Cascade);


			m.Entity<Friend>()
        			.ToTable(t => t.HasCheckConstraint(
							"chk_friends_order",
							"user_a_id < user_b_id"));

			m.Entity<FriendRequest>()
				.HasOne(r => r.Sender)
				.WithMany(u => u.FriendRequestsSent)
				.HasForeignKey(r => r.SenderID)
				.OnDelete(DeleteBehavior.Cascade);

			m.Entity<FriendRequest>()
				.HasOne(r => r.Recipient)
				.WithMany(u => u.FriendRequestsReceived)
				.HasForeignKey(r => r.RecipientID)
				.OnDelete(DeleteBehavior.Cascade);

			m.Entity<BlockedUser>()
				.HasOne(b => b.Blocker)
				.WithMany(u => u.BlockedUsers)
				.HasForeignKey(r => r.BlockerID)
				.OnDelete(DeleteBehavior.Cascade);

			m.Entity<BlockedUser>()
				.HasOne(b => b.Blocked)
				.WithMany()
				.HasForeignKey(r => r.BlockedID)
				.OnDelete(DeleteBehavior.Cascade);

				m.Entity<Conversation>()
				.HasOne(c => c.Creator)
				.WithMany()
				.HasForeignKey(c => c.CreatedBy)
				.OnDelete(DeleteBehavior.SetNull)
				.IsRequired(false);

			m.Entity<Conversation>()
				.HasOne(c => c.LastMessage)
				.WithMany()
				.HasForeignKey(c => c.LastMessageID)
				.OnDelete(DeleteBehavior.SetNull)
				.IsRequired(false);

			m.Entity<ConversationMember>()
				.HasOne(cm => cm.Conversation)
				.WithMany(c => c.Members)
				.HasForeignKey(cm => cm.ConversationID)
				.OnDelete(DeleteBehavior.Cascade);

			m.Entity<ConversationMember>()
				.HasOne(cm => cm.User)
				.WithMany(u => u.ConversationMemberships)
				.HasForeignKey(cm => cm.UserID)
				.OnDelete(DeleteBehavior.Cascade);

			m.Entity<ConversationMember>()
				.HasOne(cm => cm.LastReadMessage)
				.WithMany()
				.HasForeignKey(cm => cm.LastReadMsgID)
				.OnDelete(DeleteBehavior.SetNull)
				.IsRequired(false);

			m.Entity<Message>()
				.HasOne(msg => msg.Conversation)
				.WithMany(c => c.Messages)
				.HasForeignKey(msg => msg.ConversationID)
				.OnDelete(DeleteBehavior.Cascade);

			m.Entity<Message>()
				.HasOne(msg => msg.OriginalSender)
				.WithMany()
				.HasForeignKey(msg => msg.OriginalSenderID)
				.OnDelete(DeleteBehavior.SetNull);

			m.Entity<Message>()
				.HasOne(msg => msg.ReplyTo)
				.WithMany()
				.HasForeignKey(msg => msg.ReplyToID)
				.OnDelete(DeleteBehavior.SetNull);

			m.Entity<Message>()
				.HasOne(msg => msg.Sender)
				.WithMany(cm => cm.SentMessages)
				.HasForeignKey(msg => msg.SenderID)
				.OnDelete(DeleteBehavior.Cascade);

			m.Entity<MessageAttachment>()
				.HasOne(a => a.Message)
				.WithMany(msg => msg.Attachments)
				.HasForeignKey(a => a.MessageID)
				.OnDelete(DeleteBehavior.Cascade);

			m.Entity<MessagePin>()
				.HasOne(p => p.Message)
				.WithMany()
				.HasForeignKey(p => p.MessageID)
				.OnDelete(DeleteBehavior.Cascade);

			m.Entity<MessagePin>()
				.HasOne(p => p.Conversation)
				.WithMany(c => c.PinnedMessages)
				.HasForeignKey(p => p.ConversationID)
				.OnDelete(DeleteBehavior.Cascade);

			m.Entity<MessagePin>()
				.HasOne(p => p.Member)
				.WithMany()
				.HasForeignKey(p => p.PinnedBy)
				.OnDelete(DeleteBehavior.SetNull)
				.IsRequired(false);

			m.Entity<MessageReaction>()
				.HasOne(r => r.Message)
				.WithMany(msg => msg.Reactions)
				.HasForeignKey(r => r.MessageID)
				.OnDelete(DeleteBehavior.Cascade);
 
			m.Entity<MessageReaction>()
				.HasOne(r => r.Member)
				.WithMany()
				.HasForeignKey(r => r.MemberID)
				.OnDelete(DeleteBehavior.Cascade);

			m.Entity<MessageStatus>()
				.HasOne(s => s.Message)
				.WithMany()
				.HasForeignKey(s => s.MessageID)
				.OnDelete(DeleteBehavior.Cascade);
 
			m.Entity<MessageStatus>()
				.HasOne(s => s.Member)
				.WithMany()
				.HasForeignKey(s => s.MemberID)
				.OnDelete(DeleteBehavior.Cascade);

			m.Entity<MessageMention>()
				.HasOne(mm => mm.Message)
				.WithMany(msg => msg.Mentions)
				.HasForeignKey(mm => mm.MessageID)
				.OnDelete(DeleteBehavior.Cascade);
 
			m.Entity<MessageMention>()
				.HasOne(mm => mm.Member)
				.WithMany()
				.HasForeignKey(mm => mm.MemberID)
				.OnDelete(DeleteBehavior.Cascade);

			m.Entity<CallLog>()
				.HasOne(c => c.Conversation)
				.WithMany()
				.HasForeignKey(c => c.ConversationID)
				.OnDelete(DeleteBehavior.Cascade);
 
			m.Entity<CallLog>()
				.HasOne(c => c.StartedByMember)
				.WithMany()
				.HasForeignKey(c => c.StartedBy)
				.OnDelete(DeleteBehavior.Cascade);

			m.Entity<CallParticipant>()
				.HasOne(cp => cp.Call)
				.WithMany(c => c.Participants)
				.HasForeignKey(cp => cp.CallID)
				.OnDelete(DeleteBehavior.Cascade);

			m.Entity<CallParticipant>()
				.HasOne(cp => cp.Member)
				.WithMany(cm => cm.CallsJoined)
				.HasForeignKey(cp => cp.ParticipantID)
				.OnDelete(DeleteBehavior.Cascade);
		}

		private static void ConfigureUniqueIndexes(ModelBuilder m)
		{
			m.Entity<User>()
				.HasIndex(u => u.Username)
				.IsUnique()
				.HasDatabaseName("uq_users_username");
 
			m.Entity<User>()
				.HasIndex(u => u.Email)
				.IsUnique()
				.HasDatabaseName("uq_users_email");
 
			m.Entity<UserSession>()
				.HasIndex(s => new { s.RefreshToken })
				.IsUnique()
				.HasDatabaseName("uq_sessions_refresh_token");
 
			m.Entity<Friend>()
				.HasIndex(f => new { f.UserAID, f.UserBID })
				.IsUnique()
				.HasDatabaseName("uq_friends_pair");
 
			m.Entity<FriendRequest>()
				.HasIndex(r => new { r.SenderID, r.RecipientID })
				.IsUnique()
				.HasDatabaseName("uq_friendreq_pair");
 
			m.Entity<BlockedUser>()
				.HasIndex(b => new { b.BlockerID, b.BlockedID })
				.IsUnique()
				.HasDatabaseName("uq_block_pair");
 
			m.Entity<ConversationMember>()
				.HasIndex(cm => new { cm.ConversationID, cm.UserID })
				.IsUnique()
				.HasDatabaseName("uq_convmems_user");
 
			m.Entity<MessageStatus>()
				.HasIndex(s => new { s.MessageID, s.MemberID })
				.IsUnique()
				.HasDatabaseName("uq_status_msg_user");
 
			m.Entity<MessageReaction>()
				.HasIndex(r => new { r.MessageID, r.MemberID, r.Reaction })
				.IsUnique()
				.HasDatabaseName("uq_reaction_msg_users");
		}

		private static void ConfigureNonUniqueIndexes(ModelBuilder m)
		{
			m.Entity<MessageAttachment>()
				.HasIndex(a => a.FileHash)
				.HasDatabaseName("idx_attachments_hash");
 
			// Sắp xếp giảm dần theo last_activity_at (DESC)
			m.Entity<Conversation>()
				.HasIndex(c => c.LastActivityAt)
				.IsDescending(true)
				.HasDatabaseName("idx_conv_activity");
 
			m.Entity<ConversationMember>()
				.HasIndex(cm => cm.UserID)
				.HasDatabaseName("idx_conv_members_user");
 
			m.Entity<FriendRequest>()
				.HasIndex(r => new { r.RecipientID, r.Status })
				.HasDatabaseName("idx_friend_req_receiver");
 
			m.Entity<BlockedUser>()
				.HasIndex(b => b.BlockerID)
				.HasDatabaseName("idx_is_blocked");
 
			m.Entity<MessageMention>()
				.HasIndex(mm => mm.MemberID)
				.HasDatabaseName("idx_mentions_user");
 
			// (conversation_id ASC, sent_at DESC)
			m.Entity<Message>()
				.HasIndex(msg => new { msg.ConversationID, msg.SentAt })
				.IsDescending(false, true)
				.HasDatabaseName("idx_messages_conversation");
 
			m.Entity<Message>()
				.HasIndex(msg => msg.SenderID)
				.HasDatabaseName("idx_messages_sender");
 
			m.Entity<MessageStatus>()
				.HasIndex(s => new { s.MemberID, s.ReadAt })
				.HasDatabaseName("idx_msg_status_user");
 
			m.Entity<User>()
				.HasIndex(u => u.Email)
				.HasDatabaseName("idx_users_email");
 
			m.Entity<User>()
				.HasIndex(u => u.Username)
				.HasDatabaseName("idx_users_username");
 
			m.Entity<CallLog>()
				.HasIndex(c => c.ConversationID)
				.HasDatabaseName("idx_call_logs_conversation");
 
			m.Entity<CallParticipant>()
				.HasIndex(cp => cp.ParticipantID)
				.HasDatabaseName("idx_call_participants_user");
 
			m.Entity<MessagePin>()
				.HasIndex(p => p.ConversationID)
				.HasDatabaseName("idx_message_pins_conv");
		}

		private static void ConfigureDefaultValues(ModelBuilder m)
		{
			m.Entity<User>()
				.Property(u => u.CreatedAt)
				.HasDefaultValueSql("current_timestamp");

			m.Entity<User>()
				.Property(u => u.UpdatedAt)
				.HasDefaultValueSql("current_timestamp");

			m.Entity<UserSession>()
				.Property(u => u.CreatedAt)
				.HasDefaultValueSql("current_timestamp");

			m.Entity<UserSession>()
				.Property(u => u.LastUsedAt)
				.HasDefaultValueSql("current_timestamp");

			m.Entity<Friend>()
				.Property(u => u.CreatedAt)
				.HasDefaultValueSql("current_timestamp");

			m.Entity<FriendRequest>()
				.Property(u => u.CreatedAt)
				.HasDefaultValueSql("current_timestamp");

			m.Entity<BlockedUser>()
				.Property(u => u.CreatedAt)
				.HasDefaultValueSql("current_timestamp");

			m.Entity<Conversation>()
				.Property(u => u.CreatedAt)
				.HasDefaultValueSql("current_timestamp");

			m.Entity<ConversationMember>()
				.Property(u => u.JoinedAt)
				.HasDefaultValueSql("current_timestamp");

			m.Entity<Message>()
				.Property(u => u.SentAt)
				.HasDefaultValueSql("current_timestamp");

			m.Entity<MessageAttachment>()
				.Property(u => u.UploadedAt)
				.HasDefaultValueSql("current_timestamp");

			m.Entity<MessagePin>()
				.Property(u => u.PinnedAt)
				.HasDefaultValueSql("current_timestamp");

			m.Entity<MessageReaction>()
				.Property(u => u.CreatedAt)
				.HasDefaultValueSql("current_timestamp");
			
			m.Entity<CallLog>()
				.Property(u => u.StartedAt)
				.HasDefaultValueSql("current_timestamp");
		}

	}
}
