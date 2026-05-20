using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SecureChat.DTOs;
using SecureChat.Models;
using SecureChat.Repositories;

namespace SecureChat.Server.Hubs
{
    [Authorize]
    public sealed class ChatHub(ConversationRepository conversations, CallRepository calls, ILogger<ChatHub> logger) : Hub
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

        private bool IsCallParticipant(CallLog call)
        {
            return call.Participants.Any(p => p.Member?.UserID == Me);
        }
    }
}
