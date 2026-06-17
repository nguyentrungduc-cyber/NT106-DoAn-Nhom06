using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SecureChat.DTOs;
using SecureChat.Models;
using SecureChat.Repositories;

namespace SecureChat.Server.Hubs
{
    [Authorize]
    public sealed class ChatHub(ConversationRepository conversations, CallRepository calls, FriendRepository friends, ILogger<ChatHub> logger) : Hub
    {
        private string Me => Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        public override async Task OnConnectedAsync()
        {
            logger.LogInformation("SignalR connected: {ConnectionId} User={UserId}", Context.ConnectionId, Me);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception is null)
                logger.LogInformation("SignalR disconnected: {ConnectionId} User={UserId}", Context.ConnectionId, Me);
            else
                logger.LogWarning(exception, "SignalR disconnected with error: {ConnectionId} User={UserId}", Context.ConnectionId, Me);

            await base.OnDisconnectedAsync(exception);
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
                // DM: gửi từng người, bỏ qua nếu bị chặn (Telegram-style)
                // Clients.User() hoạt động nhờ UserIdProvider đã đăng ký
                var activeMembers = await conversations.GetActiveMembersAsync(conversationId);
                foreach (var m in activeMembers)
                {
                    if (m.UserID == Me)
                    {
                        // Gửi lại cho chính người gửi (multi-device)
                        await Clients.Caller.SendAsync("MessageReceived", message);
                        continue;
                    }
                    if (await friends.IsBlockedEitherWayAsync(Me, m.UserID))
                        continue; // im lặng, không deliver
                    await Clients.User(m.UserID).SendAsync("MessageReceived", message);
                }
            }
            else
            {
                // Group chat: broadcast bình thường, không check block
                await Clients.Group(conversationId).SendAsync("MessageReceived", message);
            }
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
        public Task LeaveCall(string callId)
        {
            if (string.IsNullOrWhiteSpace(callId))
                throw new HubException("CallId is required.");

            return Groups.RemoveFromGroupAsync(Context.ConnectionId, callId);
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

            await Clients.Group(callId).SendAsync("CallSignalReceived", callId, signal);
        }

        /// <summary>
        /// Relay a compressed video frame to other participants in a call.
        /// </summary>
        public async Task SendVideoFrame(string callId, byte[] frameData)
        {
            if (string.IsNullOrWhiteSpace(callId))
                throw new HubException("CallId is required.");

            await Clients.GroupExcept(callId, Context.ConnectionId).SendAsync("VideoFrameReceived", callId, frameData);
        }

        /// <summary>
        /// Notify all members in a conversation that an incoming call is happening.
        /// </summary>
        public async Task NotifyCallIncoming(string conversationId, string callId, string callerName, int callType)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new HubException("ConversationId is required.");

            var member = await conversations.GetMemberByConversationAndUserAsync(conversationId, Me);
            if (member is null || member.LeftAt is not null)
                throw new HubException("You are not a member of this conversation.");

            await Clients.GroupExcept(conversationId, Context.ConnectionId).SendAsync("CallIncoming", callId, callerName, callType, conversationId);
        }

        private bool IsCallParticipant(CallLog call)
        {
            return call.Participants.Any(p => p.Member?.UserID == Me);
        }
    }
}
