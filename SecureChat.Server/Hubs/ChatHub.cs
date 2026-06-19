using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SecureChat.DTOs;
using SecureChat.Models;
using SecureChat.Repositories;

namespace SecureChat.Server.Hubs
{
    [Authorize]
    public sealed class ChatHub(ConversationRepository conversations, MessageRepository messages, CallRepository calls, ILogger<ChatHub> logger) : Hub
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

            await Clients.Group(conversationId).SendAsync("MessageReceived", message);
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

            await Clients.GroupExcept(callId, Context.ConnectionId).SendAsync("CallSignalReceived", callId, $"{Me}|{signal}");
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
        /// Notify all members in a conversation that an incoming call is happening.
        /// </summary>
        public async Task NotifyCallIncoming(string conversationId, string callId, string callerName, CallType callType)
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
