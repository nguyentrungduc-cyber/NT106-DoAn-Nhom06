using Microsoft.EntityFrameworkCore;

namespace SecureChat.Models
{
    public static class SeedData
    {
        public static void HasSecureChatSeedData(this ModelBuilder m)
        {
            var t0 = new DateTime(2025, 1, 10, 8, 0, 0, DateTimeKind.Utc);
            var t1 = t0.AddMinutes(2);
            var t2 = t0.AddMinutes(5);
            var t3 = t0.AddMinutes(8);
            var t4 = t0.AddMinutes(12);
            var t5 = t0.AddMinutes(15);
            var t6 = t0.AddMinutes(18);
            var t7 = t0.AddMinutes(21);
            var t8 = t0.AddMinutes(24);
            var t9 = t0.AddMinutes(28);

            m.Entity<User>().HasData(
                new User
                {
                    UserID = "U0000001",
                    Username = "hoanghieu",
                    DisplayName = "Hoang Hieu",
                    Email = "u1@securechat.local",
                    HashedPassword = "hash_demo_value",
                    HashedBKey = "hash_demo_value",
                    HashedRecoveryKey = "hash_demo_value",
                    KeySalt = "hash_demo_value",
                    PublicKey = "encrypted_demo_value",
                    ShowReadStatus = true,
                    ShowOnlineStatus = true,
                    LastSeenUtc = null,
                    CreatedAt = t0,
                    UpdatedAt = t0
                },
                new User
                {
                    UserID = "U0000002",
                    Username = "minhquan",
                    DisplayName = "Minh Quan",
                    Email = "u2@securechat.local",
                    HashedPassword = "hash_demo_value",
                    HashedBKey = "hash_demo_value",
                    HashedRecoveryKey = "hash_demo_value",
                    KeySalt = "hash_demo_value",
                    PublicKey = "encrypted_demo_value",
                    ShowReadStatus = true,
                    ShowOnlineStatus = true,
                    LastSeenUtc = null,
                    CreatedAt = t0,
                    UpdatedAt = t0
                },
                new User
                {
                    UserID = "U0000003",
                    Username = "linhnguyen",
                    DisplayName = "Linh Nguyen",
                    Email = "u3@securechat.local",
                    HashedPassword = "hash_demo_value",
                    HashedBKey = "hash_demo_value",
                    HashedRecoveryKey = "hash_demo_value",
                    KeySalt = "hash_demo_value",
                    PublicKey = "encrypted_demo_value",
                    ShowReadStatus = true,
                    ShowOnlineStatus = true,
                    LastSeenUtc = null,
                    CreatedAt = t0,
                    UpdatedAt = t0
                }
            );

            m.Entity<UserSession>().HasData(
                new UserSession
                {
                    SessionID = "S0000001",
                    UserID = "U0000001",
                    DeviceName = "Windows 11 Dev Machine",
                    RefreshToken = "refresh_token_demo_u1",
                    CreatedAt = t1,
                    ExpiresAt = t1.AddDays(30),
                    LastUsedAt = t1
                },
                new UserSession
                {
                    SessionID = "S0000002",
                    UserID = "U0000002",
                    DeviceName = "Windows 11 QA Laptop",
                    RefreshToken = "refresh_token_demo_u2",
                    CreatedAt = t1,
                    ExpiresAt = t1.AddDays(30),
                    LastUsedAt = t1
                }
            );

            m.Entity<Friend>().HasData(
                new Friend
                {
                    FriendshipID = "F0000001",
                    UserAID = "U0000001",
                    UserBID = "U0000002",
                    CreatedAt = t2
                }
            );

            m.Entity<FriendRequest>().HasData(
                new FriendRequest
                {
                    RequestID = "RQ000001",
                    SenderID = "U0000003",
                    RecipientID = "U0000001",
                    Status = FriendRequestStatus.Pending,
                    CreatedAt = t3,
                    RespondedAt = null
                }
            );

            m.Entity<BlockedUser>().HasData(
                new BlockedUser
                {
                    BlockID = "B0000001",
                    BlockerID = "U0000002",
                    BlockedID = "U0000003",
                    CreatedAt = t3
                }
            );

            m.Entity<Conversation>().HasData(
                new Conversation
                {
                    ConversationID = "C0000001",
                    Type = ConversationType.Direct,
                    Name = null,
                    AvatarURL = null,
                    CreatedBy = "U0000001",
                    LastMessageID = null,
                    LastActivityAt = t7,
                    CreatedAt = t4
                },
                new Conversation
                {
                    ConversationID = "C0000002",
                    Type = ConversationType.Group,
                    Name = "NT106 Team",
                    AvatarURL = null,
                    CreatedBy = "U0000001",
                    LastMessageID = null,
                    LastActivityAt = t9,
                    CreatedAt = t4
                }
            );

            m.Entity<ConversationMember>().HasData(
                new ConversationMember
                {
                    MemberID = "M0000001",
                    ConversationID = "C0000001",
                    UserID = "U0000001",
                    Role = MemberRole.Owner,
                    Nickname = "Hieu",
                    EncryptedKey = "encrypted_demo_value",
                    JoinedAt = t4,
                    LeftAt = null,
                    ShowNotifications = NotificationMode.All,
                    BannedUntil = null,
                    LastReadMsgID = null
                },
                new ConversationMember
                {
                    MemberID = "M0000002",
                    ConversationID = "C0000001",
                    UserID = "U0000002",
                    Role = MemberRole.Member,
                    Nickname = "Quan",
                    EncryptedKey = "encrypted_demo_value",
                    JoinedAt = t4,
                    LeftAt = null,
                    ShowNotifications = NotificationMode.All,
                    BannedUntil = null,
                    LastReadMsgID = null
                },
                new ConversationMember
                {
                    MemberID = "M0000003",
                    ConversationID = "C0000002",
                    UserID = "U0000001",
                    Role = MemberRole.Owner,
                    Nickname = "Admin Hieu",
                    EncryptedKey = "encrypted_demo_value",
                    JoinedAt = t4,
                    LeftAt = null,
                    ShowNotifications = NotificationMode.All,
                    BannedUntil = null,
                    LastReadMsgID = null
                },
                new ConversationMember
                {
                    MemberID = "M0000004",
                    ConversationID = "C0000002",
                    UserID = "U0000002",
                    Role = MemberRole.Moderator,
                    Nickname = "Mod Quan",
                    EncryptedKey = "encrypted_demo_value",
                    JoinedAt = t4,
                    LeftAt = null,
                    ShowNotifications = NotificationMode.All,
                    BannedUntil = null,
                    LastReadMsgID = null
                },
                new ConversationMember
                {
                    MemberID = "M0000005",
                    ConversationID = "C0000002",
                    UserID = "U0000003",
                    Role = MemberRole.Member,
                    Nickname = "Linh",
                    EncryptedKey = "encrypted_demo_value",
                    JoinedAt = t4,
                    LeftAt = null,
                    ShowNotifications = NotificationMode.All,
                    BannedUntil = null,
                    LastReadMsgID = null
                }
            );

            m.Entity<Message>().HasData(
                new Message
                {
                    MessageID = "MSG00001",
                    ConversationID = "C0000001",
                    OriginalSenderID = "U0000001",
                    SenderID = "M0000001",
                    ReplyToID = null,
                    Type = MessageType.Text,
                    Content = "hello bro",
                    ContentIV = "iv_demo_value",
                    SentAt = t5,
                    DeletedAt = null,
                    EditedAt = null
                },
                new Message
                {
                    MessageID = "MSG00002",
                    ConversationID = "C0000001",
                    OriginalSenderID = "U0000002",
                    SenderID = "M0000002",
                    ReplyToID = "MSG00001",
                    Type = MessageType.Text,
                    Content = "check API ch?a?",
                    ContentIV = "iv_demo_value",
                    SentAt = t6,
                    DeletedAt = null,
                    EditedAt = null
                },
                new Message
                {
                    MessageID = "MSG00003",
                    ConversationID = "C0000001",
                    OriginalSenderID = "U0000001",
                    SenderID = "M0000001",
                    ReplyToID = null,
                    Type = MessageType.Text,
                    Content = "encrypt ok ch?a?",
                    ContentIV = "iv_demo_value",
                    SentAt = t7,
                    DeletedAt = null,
                    EditedAt = null
                },
                new Message
                {
                    MessageID = "MSG00004",
                    ConversationID = "C0000002",
                    OriginalSenderID = "U0000001",
                    SenderID = "M0000003",
                    ReplyToID = null,
                    Type = MessageType.Text,
                    Content = "hello team, test group chat nh�",
                    ContentIV = "iv_demo_value",
                    SentAt = t5,
                    DeletedAt = null,
                    EditedAt = null
                },
                new Message
                {
                    MessageID = "MSG00005",
                    ConversationID = "C0000002",
                    OriginalSenderID = "U0000002",
                    SenderID = "M0000004",
                    ReplyToID = "MSG00004",
                    Type = MessageType.Text,
                    Content = "ok bro, SignalR realtime ?n",
                    ContentIV = "iv_demo_value",
                    SentAt = t6,
                    DeletedAt = null,
                    EditedAt = null
                },
                new Message
                {
                    MessageID = "MSG00006",
                    ConversationID = "C0000002",
                    OriginalSenderID = "U0000003",
                    SenderID = "M0000005",
                    ReplyToID = null,
                    Type = MessageType.Text,
                    Content = "done r?i, nh? test forgot password",
                    ContentIV = "iv_demo_value",
                    SentAt = t7,
                    DeletedAt = null,
                    EditedAt = null
                },
                new Message
                {
                    MessageID = "MSG00007",
                    ConversationID = "C0000002",
                    OriginalSenderID = "U0000001",
                    SenderID = "M0000003",
                    ReplyToID = "MSG00006",
                    Type = MessageType.File,
                    Content = "file sent: api_test_plan.pdf",
                    ContentIV = "iv_demo_value",
                    SentAt = t8,
                    DeletedAt = null,
                    EditedAt = null
                }
            );

            m.Entity<MessageAttachment>().HasData(
                new MessageAttachment
                {
                    AttachmentID = "A0000001",
                    MessageID = "MSG00007",
                    FileURL = "encrypted_demo_value",
                    FileName = "api_test_plan.pdf",
                    FileType = "application/pdf",
                    FileHash = "hash_demo_value",
                    FileSize = 102400,
                    Width = null,
                    Height = null,
                    ThumbnailURL = null,
                    DurationSecs = null,
                    FileIv = "iv_demo_value",
                    ThumbnailIv = null,
                    UploadedAt = t8
                }
            );

            m.Entity<MessageMention>().HasData(
                new MessageMention
                {
                    MessageID = "MSG00005",
                    MemberID = "M0000005"
                }
            );

            m.Entity<MessagePin>().HasData(
                new MessagePin
                {
                    MessageID = "MSG00006",
                    ConversationID = "C0000002",
                    PinnedBy = "M0000003",
                    PinnedAt = t9
                }
            );

            m.Entity<MessageReaction>().HasData(
                new MessageReaction
                {
                    ReactionID = "RE000001",
                    MessageID = "MSG00002",
                    MemberID = "M0000001",
                    Reaction = "??",
                    CreatedAt = t7
                }
            );

            m.Entity<MessageStatus>().HasData(
                new MessageStatus
                {
                    StatusID = "ST000001",
                    MessageID = "MSG00001",
                    MemberID = "M0000002",
                    DeliveredAt = t5,
                    ReadAt = t6
                },
                new MessageStatus
                {
                    StatusID = "ST000002",
                    MessageID = "MSG00002",
                    MemberID = "M0000001",
                    DeliveredAt = t6,
                    ReadAt = t7
                },
                new MessageStatus
                {
                    StatusID = "ST000003",
                    MessageID = "MSG00006",
                    MemberID = "M0000003",
                    DeliveredAt = t7,
                    ReadAt = t8
                }
            );

            m.Entity<CallLog>().HasData(
                new CallLog
                {
                    CallID = "CL000001",
                    ConversationID = "C0000002",
                    Type = CallType.Voice,
                    Status = CallStatus.Ended,
                    StartedBy = "M0000003",
                    StartedAt = t8,
                    EndedAt = t9
                }
            );

            m.Entity<CallParticipant>().HasData(
                new CallParticipant
                {
                    ParticipantID = "M0000003",
                    CallID = "CL000001",
                    Status = CallParticipantStatus.Joined,
                    JoinedAt = t8,
                    LeftAt = t9
                },
                new CallParticipant
                {
                    ParticipantID = "M0000004",
                    CallID = "CL000001",
                    Status = CallParticipantStatus.Joined,
                    JoinedAt = t8,
                    LeftAt = t9
                },
                new CallParticipant
                {
                    ParticipantID = "M0000005",
                    CallID = "CL000001",
                    Status = CallParticipantStatus.Missed,
                    JoinedAt = null,
                    LeftAt = null
                }
            );
        }
    }
}
