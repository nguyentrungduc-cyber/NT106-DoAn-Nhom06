using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using SecureChat.DTOs;

namespace SecureChat.Client.Services.RealTime
{
    public sealed class SignalRClient : IAsyncDisposable
    {
        private const string DefaultBaseUrl = "http://localhost:5097";
        private readonly HubConnection _connection;

		public event Func<MessageResponse, Task>? MessageReceived;
		public event Func<MessageResponse, Task>? MessageRecalled;
		public event Func<string, string, Task>? CallSignalReceived;
        public event Func<string, string, int, string, Task>? CallIncoming;
        public event Func<string, byte[], Task>? VideoFrameReceived;
        public event Func<string, string, Task>? UserTyping;
        public event Func<string, string, Task>? UserStoppedTyping;
        public event Func<Exception?, Task>? Closed;
        public event Func<Exception?, Task>? Reconnecting;
        public event Func<string?, Task>? Reconnected;
        public event Func<string, Task>? ConversationCreated;
        public event Func<string, string?, string?, string?, Task>? ProfileUpdated;
        public event Func<string, Task>? ConversationDeleted;
        public event Func<string, Task>? ConversationUpdated;
        public event Func<string, string, string, string, Task>? MessagePinned;
        public event Func<string, string, Task>? MessageUnpinned;
        public event Func<string, string, Task>? MemberAdded;
        public event Func<string, string, Task>? MemberRemoved;

        public bool IsConnected => _connection.State == HubConnectionState.Connected;

        public SignalRClient(Func<Task<string?>> accessTokenProvider, string? baseUrl = null)
        {
            ArgumentNullException.ThrowIfNull(accessTokenProvider);

            var resolvedBaseUrl = ResolveBaseUrl(baseUrl);
            _connection = new HubConnectionBuilder()
                .WithUrl($"{resolvedBaseUrl}/hubs/chat", options =>
                {
                    options.AccessTokenProvider = accessTokenProvider;
                })
                .WithAutomaticReconnect()
                .Build();

            _connection.Closed += async ex =>
            {
                if (Closed is not null)
                    await Closed.Invoke(ex);
            };

            _connection.Reconnecting += async ex =>
            {
                if (Reconnecting is not null)
                    await Reconnecting.Invoke(ex);
            };

            _connection.Reconnected += async connectionId =>
            {
                if (Reconnected is not null)
                    await Reconnected.Invoke(connectionId);
            };

            _connection.On<MessageResponse>("MessageReceived", async message =>
            {
                if (MessageReceived is not null)
                    await MessageReceived.Invoke(message);
            });

            _connection.On<MessageResponse>("MessageRecalled", async message =>
            {
                if (MessageRecalled is not null)
                    await MessageRecalled.Invoke(message);
            });

            _connection.On<string, string>("CallSignalReceived", async (callId, signal) =>
            {
                if (CallSignalReceived is not null)
                    await CallSignalReceived.Invoke(callId, signal);
            });

            _connection.On<string, string, int, string>("CallIncoming", async (callId, callerName, callType, conversationId) =>
            {
                if (CallIncoming is not null)
                    await CallIncoming.Invoke(callId, callerName, callType, conversationId);
            });

            _connection.On<string, byte[]>("VideoFrameReceived", async (callId, frameData) =>
            {
                if (VideoFrameReceived is not null)
                    await VideoFrameReceived.Invoke(callId, frameData);
            });

            _connection.On<string, string>("UserTyping", async (conversationId, username) =>
            {
                if (UserTyping is not null)
                    await UserTyping.Invoke(conversationId, username);
            });

            _connection.On<string, string>("UserStoppedTyping", async (conversationId, username) =>
            {
                if (UserStoppedTyping is not null)
                    await UserStoppedTyping.Invoke(conversationId, username);
            });

            _connection.On<string>("ConversationCreated", async conversationId =>
            {
                if (ConversationCreated is not null)
                    await ConversationCreated.Invoke(conversationId);
            });

            _connection.On<string, string, string, string>("ProfileUpdated", async (userId, displayName, username, avatarUrl) =>
            {
                if (ProfileUpdated is not null)
                    await ProfileUpdated.Invoke(userId, displayName, username, avatarUrl);
            });

            _connection.On<string>("ConversationDeleted", async conversationId =>
            {
                if (ConversationDeleted is not null)
                    await ConversationDeleted.Invoke(conversationId);
            });

            _connection.On<string>("ConversationUpdated", async conversationId =>
            {
                if (ConversationUpdated is not null)
                    await ConversationUpdated.Invoke(conversationId);
            });

            _connection.On<string, string, string, string>("MessagePinned", async (conversationId, messageId, pinnedByUserId, pinnedByName) =>
            {
                if (MessagePinned is not null)
                    await MessagePinned.Invoke(conversationId, messageId, pinnedByUserId, pinnedByName);
            });

            _connection.On<string, string>("MessageUnpinned", async (conversationId, messageId) =>
            {
                if (MessageUnpinned is not null)
                    await MessageUnpinned.Invoke(conversationId, messageId);
            });

            _connection.On<string, string>("MemberAdded", async (conversationId, userId) =>
            {
                if (MemberAdded is not null)
                    await MemberAdded.Invoke(conversationId, userId);
            });

            _connection.On<string, string>("MemberRemoved", async (conversationId, userId) =>
            {
                if (MemberRemoved is not null)
                    await MemberRemoved.Invoke(conversationId, userId);
            });
        }

        public Task StartAsync() => _connection.StartAsync();

        public Task StopAsync() => _connection.StopAsync();

        public Task JoinConversationAsync(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("ConversationId is required.", nameof(conversationId));

            return _connection.InvokeAsync("JoinConversation", conversationId);
        }

        public Task LeaveConversationAsync(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("ConversationId is required.", nameof(conversationId));

            return _connection.InvokeAsync("LeaveConversation", conversationId);
        }

        public Task NotifyTypingAsync(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("ConversationId is required.", nameof(conversationId));

            return _connection.InvokeAsync("UserTyping", conversationId);
        }

        public Task NotifyStoppedTypingAsync(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("ConversationId is required.", nameof(conversationId));

            return _connection.InvokeAsync("UserStoppedTyping", conversationId);
        }

        public Task SendMessageAsync(string conversationId, MessageResponse message)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("ConversationId is required.", nameof(conversationId));
            ArgumentNullException.ThrowIfNull(message);

            return _connection.InvokeAsync("SendMessage", conversationId, message);
        }

        public Task RecallMessageAsync(string conversationId, string messageId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("ConversationId is required.", nameof(conversationId));
            if (string.IsNullOrWhiteSpace(messageId))
                throw new ArgumentException("MessageId is required.", nameof(messageId));

            return _connection.InvokeAsync("RecallMessage", conversationId, messageId);
        }

        public Task JoinCallAsync(string callId)
        {
            if (string.IsNullOrWhiteSpace(callId))
                throw new ArgumentException("CallId is required.", nameof(callId));

            return _connection.InvokeAsync("JoinCall", callId);
        }

        public Task LeaveCallAsync(string callId)
        {
            if (string.IsNullOrWhiteSpace(callId))
                throw new ArgumentException("CallId is required.", nameof(callId));

            return _connection.InvokeAsync("LeaveCall", callId);
        }

        public Task SendCallSignalAsync(string callId, string signal)
        {
            if (string.IsNullOrWhiteSpace(callId))
                throw new ArgumentException("CallId is required.", nameof(callId));
            if (string.IsNullOrWhiteSpace(signal))
                throw new ArgumentException("Signal is required.", nameof(signal));

            return _connection.InvokeAsync("SendCallSignal", callId, signal);
        }

        public Task SendVideoFrameAsync(string callId, byte[] frameData)
        {
            if (string.IsNullOrWhiteSpace(callId))
                throw new ArgumentException("CallId is required.", nameof(callId));
            ArgumentNullException.ThrowIfNull(frameData);

            return _connection.InvokeAsync("SendVideoFrame", callId, frameData);
        }

        public Task PinMessageAsync(string conversationId, string messageId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("ConversationId is required.", nameof(conversationId));
            if (string.IsNullOrWhiteSpace(messageId))
                throw new ArgumentException("MessageId is required.", nameof(messageId));

            return _connection.InvokeAsync("PinMessage", conversationId, messageId);
        }

        public Task UnpinMessageAsync(string conversationId, string messageId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("ConversationId is required.", nameof(conversationId));
            if (string.IsNullOrWhiteSpace(messageId))
                throw new ArgumentException("MessageId is required.", nameof(messageId));

            return _connection.InvokeAsync("UnpinMessage", conversationId, messageId);
        }

        public Task NotifyCallIncomingAsync(string conversationId, string callId, string callerName, int callType)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("ConversationId is required.", nameof(conversationId));

            return _connection.InvokeAsync("NotifyCallIncoming", conversationId, callId, callerName, callType);
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }

        private static string ResolveBaseUrl(string? overrideBaseUrl = null)
        {
            var resolved = overrideBaseUrl
                ?? Environment.GetEnvironmentVariable("SECURECHAT_API_BASE_URL")
                ?? DefaultBaseUrl;

            return resolved.TrimEnd('/');
        }
    }
}
