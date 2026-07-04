using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SecureChat.DTOs;
using SecureChat.Models;
using SecureChat.Repositories;
using SecureChat.Server.Services;

namespace SecureChat.Server.Hubs
{
    [Authorize]
    public sealed class ChatHub(
        ConversationRepository conversations,
        MessageRepository messages,
        CallRepository calls,
        UserRepository users,
        PrivacyRepository privacy,
        FriendRepository friends,
        UserPresenceService presence,
        ILogger<ChatHub> logger) : Hub
    {
        private string Me => Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        // Track connectionId → userId để dùng GroupExcept khi block
        internal static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> ConnectionUserMap = new();

        public override async Task OnConnectedAsync()
        {
            logger.LogInformation("SignalR connected: {ConnectionId} User={UserId}", Context.ConnectionId, Me);

            if (string.IsNullOrWhiteSpace(Me))
            {
                await base.OnConnectedAsync();
                return;
            }

            ConnectionUserMap[Context.ConnectionId] = Me;

            // Track presence via UserPresenceService (DB + broadcast)
            await presence.UserConnectedAsync(Me, Context.ConnectionId);

            // Always join conversation groups
            var myConvs = await conversations.GetConversationsByMemberAsync(Me);
            foreach (var conv in myConvs)
                await Groups.AddToGroupAsync(Context.ConnectionId, conv.ConversationID);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception is null)
                logger.LogInformation("SignalR disconnected: {ConnectionId} User={UserId}", Context.ConnectionId, Me);
            else
                logger.LogWarning(exception, "SignalR disconnected with error: {ConnectionId} User={UserId}", Context.ConnectionId, Me);

            if (string.IsNullOrWhiteSpace(Me))
            {
                await base.OnDisconnectedAsync(exception);
                return;
            }

            ConnectionUserMap.TryRemove(Context.ConnectionId, out _);

            // Track presence via UserPresenceService (DB + broadcast)
            await presence.UserDisconnectedAsync(Me, Context.ConnectionId);

            // Orphaned call cleanup: end any active calls for this user
            try
            {
                var myConvs = await conversations.GetConversationsByMemberAsync(Me);
                foreach (var conv in myConvs)
                {
                    var activeCall = await calls.GetActiveCallAsync(conv.ConversationID);
                    if (activeCall != null && activeCall.Status != CallStatus.Ended)
                    {
                        var ringing = activeCall.Status == CallStatus.Ringing;
                        await calls.EndCallAsync(activeCall.CallID);

                        if (ringing)
                        {
                            // Ringing: other participants are NOT in the SignalR call group.
                            // Route CALL_ENDED directly to each user connection.
                            var participants = await calls.GetParticipantsByCallAsync(activeCall.CallID);
                            foreach (var p in participants)
                            {
                                var uid = p.Member?.UserID;
                                if (uid != null && uid != Me)
                                {
                                    try { await Clients.User(uid).SendAsync("CallSignalReceived", activeCall.CallID, "CALL_ENDED"); } catch { }
                                }
                            }
                        }
                        else
                        {
                            // Ongoing: all participants are in the group
                            try { await Clients.Group(activeCall.CallID).SendAsync("CallSignalReceived", activeCall.CallID, "CALL_ENDED"); } catch { }
                        }
                    }
                }
            }
            catch { /* best-effort cleanup */ }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task QueryUserPresence(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return;
            var user = await users.GetByIdAsync(userId);
            if (user is null) return;

            bool online = presence.IsOnline(userId);

            // Respect ShowOnlineStatus privacy toggle
            if (user.ShowOnlineStatus == false)
            {
                await Clients.Caller.SendAsync("UserStatusChanged", userId, "Offline", (DateTime?)null);
                return;
            }

            DateTime? lastSeen = user.LastSeenUtc;
            if (!online)
            {
                var settings = await privacy.GetRawSettingsAsync(userId);
                if (settings is not null && settings.LastSeenPrivacy != PrivacyLevel.Everybody)
                    lastSeen = null;
            }

            await Clients.Caller.SendAsync("UserStatusChanged", userId, online ? "Online" : "Offline", lastSeen);
        }

        /// <summary>
        /// Join a conversation group to receive real-time messages.
        /// </summary>
        public async Task JoinConversation(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new HubException("ConversationId is required.");

            var member = await conversations.GetMemberByConversationAndUserAsync(conversationId, Me);
            if (member is null || member.LeftAt is not null)
                throw new HubException("You are not a member of this conversation.");

            await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);
        }

        /// <summary>
        /// Leave a conversation group.
        /// </summary>
        public Task LeaveConversation(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new HubException("ConversationId is required.");

            return Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);
        }

        /// <summary>
        /// Broadcast a message to a conversation group after it has been persisted.
        /// </summary>
        public async Task SendMessage(string conversationId, MessageResponse message)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new HubException("ConversationId is required.");
            ArgumentNullException.ThrowIfNull(message);

            var member = await conversations.GetMemberByConversationAndUserAsync(conversationId, Me);
            if (member is null || member.LeftAt is not null)
                throw new HubException("You are not a member of this conversation.");

            var conversation = await conversations.GetByIdAsync(conversationId);
            if (conversation?.Type == ConversationType.Direct)
            {
                // DM: tìm connectionId của người bị chặn rồi dùng GroupExcept
                var activeMembers = await conversations.GetActiveMembersAsync(conversationId);
                var excludedConnIds = new List<string>();

                foreach (var m in activeMembers.Where(m => m.UserID != Me))
                {
                    bool isBlocked = await friends.IsBlockedEitherWayAsync(Me, m.UserID);
                    logger.LogInformation("Block check: sender={Sender} receiver={Receiver} isBlocked={IsBlocked}", Me, m.UserID, isBlocked);
                    if (isBlocked)
                    {
                        // Lấy tất cả connectionId của user bị chặn
                        var blockedConns = ConnectionUserMap
                            .Where(kv => kv.Value == m.UserID)
                            .Select(kv => kv.Key)
                            .ToList();
                        excludedConnIds.AddRange(blockedConns);
                    }
                }

                // Broadcast trong group nhưng exclude connection của người bị chặn
                await Clients.GroupExcept(conversationId, excludedConnIds)
                             .SendAsync("MessageReceived", message);
            }
            else
            {
                // Group chat: broadcast bình thường, không check block
                await Clients.Group(conversationId).SendAsync("MessageReceived", message);
            }
        }

		/// <summary>
		/// Broadcast a recalled message to a conversation group.
		/// </summary>
		public async Task RecallMessage(string conversationId, string messageId)
		{
			if (string.IsNullOrWhiteSpace(conversationId))
				throw new HubException("ConversationId is required.");
			if (string.IsNullOrWhiteSpace(messageId))
				throw new HubException("MessageId is required.");

			var member = await conversations.GetMemberByConversationAndUserAsync(conversationId, Me);
			if (member is null || member.LeftAt is not null)
				throw new HubException("You are not a member of this conversation.");

			var msg = await messages.GetByIdAsync(messageId);
			if (msg is null || msg.ConversationID != conversationId)
				throw new HubException("Message not found.");
			if (msg.RecalledAt is null)
				throw new HubException("Message has not been recalled.");

			await Clients.Group(conversationId).SendAsync("MessageRecalled", MessageResponse.From(msg));
		}

        /// <summary>
        /// Broadcast a pin event to a conversation group.
        /// Pinner identity is resolved server-side from the JWT claim.
        /// </summary>
        public async Task PinMessage(string conversationId, string messageId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new HubException("ConversationId is required.");
            if (string.IsNullOrWhiteSpace(messageId))
                throw new HubException("MessageId is required.");

            var member = await conversations.GetMemberByConversationAndUserAsync(conversationId, Me);
            if (member is null || member.LeftAt is not null)
                throw new HubException("You are not a member of this conversation.");

            var pinnedByName = member.Nickname ?? member.User?.DisplayName ?? member.User?.Username ?? "Unknown";
            await Clients.Group(conversationId).SendAsync("MessagePinned", conversationId, messageId, member.UserID, pinnedByName);
        }

        /// <summary>
        /// Broadcast an unpin event to a conversation group.
        /// </summary>
        public async Task UnpinMessage(string conversationId, string messageId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new HubException("ConversationId is required.");
            if (string.IsNullOrWhiteSpace(messageId))
                throw new HubException("MessageId is required.");

            var member = await conversations.GetMemberByConversationAndUserAsync(conversationId, Me);
            if (member is null || member.LeftAt is not null)
                throw new HubException("You are not a member of this conversation.");

            await Clients.Group(conversationId).SendAsync("MessageUnpinned", conversationId, messageId);
        }

        /// <summary>
        /// Notify group that the current user is typing.
        /// </summary>
        public async Task UserTyping(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new HubException("ConversationId is required.");

            var member = await conversations.GetMemberByConversationAndUserAsync(conversationId, Me);
            if (member is null || member.LeftAt is not null)
                throw new HubException("You are not a member of this conversation.");

            await Clients.GroupExcept(conversationId, Context.ConnectionId)
                .SendAsync("UserTyping", conversationId, member.User?.DisplayName ?? member.User?.Username ?? "Unknown");
        }

        /// <summary>
        /// Notify group that the current user stopped typing.
        /// </summary>
        public async Task UserStoppedTyping(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new HubException("ConversationId is required.");

            var member = await conversations.GetMemberByConversationAndUserAsync(conversationId, Me);
            if (member is null || member.LeftAt is not null)
                throw new HubException("You are not a member of this conversation.");

            await Clients.GroupExcept(conversationId, Context.ConnectionId)
                .SendAsync("UserStoppedTyping", conversationId, member.User?.DisplayName ?? member.User?.Username ?? "Unknown");
        }

        /// <summary>
        /// Join a call group for signaling events.
        /// </summary>
        public async Task JoinCall(string callId)
        {
            if (string.IsNullOrWhiteSpace(callId))
                throw new HubException("CallId is required.");

            var call = await calls.GetByIdAsync(callId);
            if (call is null)
                throw new HubException("Call not found.");
            if (!IsCallParticipant(call))
                throw new HubException("You are not a participant of this call.");

            await Groups.AddToGroupAsync(Context.ConnectionId, callId);
        }

        /// <summary>
        /// Leave a call group.
        /// </summary>
        public async Task LeaveCall(string callId)
        {
            if (string.IsNullOrWhiteSpace(callId))
                throw new HubException("CallId is required.");

            var call = await calls.GetByIdAsync(callId);
            if (call is null)
                throw new HubException("Call not found.");
            if (!IsCallParticipant(call))
                throw new HubException("You are not a participant of this call.");

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, callId);
        }

        /// <summary>
        /// Broadcast call signaling data to other participants.
        /// </summary>
        public async Task SendCallSignal(string callId, string signal)
        {
            if (string.IsNullOrWhiteSpace(callId))
                throw new HubException("CallId is required.");
            if (string.IsNullOrWhiteSpace(signal))
                throw new HubException("Signal is required.");

            var call = await calls.GetByIdAsync(callId);
            if (call is null)
                throw new HubException("Call not found.");
            if (!IsCallParticipant(call))
                throw new HubException("You are not a participant of this call.");

            // Route signals based on call state.
            // Ringing: participants may NOT be in the SignalR call group yet.
            // Ongoing: all participants are in the group.
            if (call.Status == CallStatus.Ringing)
            {
                // Ringing state — route directly to each other participant's user connection.
                foreach (var p in call.Participants ?? Enumerable.Empty<CallParticipant>())
                {
                    var uid = p.Member?.UserID;
                    if (uid != null && uid != Me)
                    {
                        try { await Clients.User(uid).SendAsync("CallSignalReceived", callId, signal); } catch { }
                    }
                }
            }
            else
            {
                // Ongoing state — existing group routing with sender prefix
                await Clients.GroupExcept(callId, Context.ConnectionId).SendAsync("CallSignalReceived", callId, $"{Me}|{signal}");
            }

            // When a participant declines, update their server-side status so EndCall
            // history logic can detect the decline and show the correct message.
            if (signal == "CALL_REJECTED")
            {
                var rejectedParticipant = call.Participants?
                    .FirstOrDefault(p => p.Member?.UserID == Me && p.Status == CallParticipantStatus.Ringing);
                if (rejectedParticipant != null)
                {
                    await calls.UpdateParticipantStatusAsync(
                        rejectedParticipant.ParticipantID, callId, CallParticipantStatus.Declined);
                }
            }
        }

        /// <summary>
        /// Relay a compressed video frame to other participants in a call.
        /// </summary>
        public async Task SendVideoFrame(string callId, byte[] frameData)
        {
            if (string.IsNullOrWhiteSpace(callId))
                throw new HubException("CallId is required.");

            var call = await calls.GetByIdAsync(callId);
            if (call is null)
                throw new HubException("Call not found.");
            if (!IsCallParticipant(call))
                throw new HubException("You are not a participant of this call.");

            string senderId = Me;
            await Clients.GroupExcept(callId, Context.ConnectionId).SendAsync("VideoFrameReceived", callId, senderId, frameData);
        }

        /// <summary>
        /// Relay audio data to other participants in a call.
        /// </summary>
        public async Task SendAudioData(string callId, byte[] audioData)
        {
            if (string.IsNullOrWhiteSpace(callId))
                throw new HubException("CallId is required.");

            var call = await calls.GetByIdAsync(callId);
            if (call is null)
                throw new HubException("Call not found.");
            if (!IsCallParticipant(call))
                throw new HubException("You are not a participant of this call.");

            string senderId = Me;
            await Clients.GroupExcept(callId, Context.ConnectionId).SendAsync("AudioDataReceived", callId, senderId, audioData);
        }

        /// <summary>
        /// Notify members in a conversation about an incoming call — respects CallsPrivacy.
        /// </summary>
        public async Task NotifyCallIncoming(string conversationId, string callId, string callerName, CallType callType)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new HubException("ConversationId is required.");

            var member = await conversations.GetMemberByConversationAndUserAsync(conversationId, Me);
            if (member is null || member.LeftAt is not null)
                throw new HubException("You are not a member of this conversation.");

            var activeMembers = await conversations.GetActiveMembersAsync(conversationId);
            foreach (var m in activeMembers.Where(m => m.UserID != Me))
            {
                if (await privacy.CanStartCallAsync(Me, m.UserID))
                    await Clients.User(m.UserID).SendAsync("CallIncoming", callId, callerName, callType, conversationId);
            }
        }

        private bool IsCallParticipant(CallLog call)
        {
            return call.Participants.Any(p => p.Member?.UserID == Me);
        }
    }
}
