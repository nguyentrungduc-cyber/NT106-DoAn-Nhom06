using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SecureChat.DTOs;
using SecureChat.Models;
using SecureChat.Repositories;
using SecureChat.Server.Hubs;

namespace SecureChat.Server.Services
{
    public sealed class CallTimeoutService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ILogger<CallTimeoutService> _logger;
        private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);

        public CallTimeoutService(
            IServiceScopeFactory scopeFactory,
            IHubContext<ChatHub> hubContext,
            ILogger<CallTimeoutService> logger)
        {
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CallTimeoutService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckForTimedOutCallsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking for timed-out calls.");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        private async Task CheckForTimedOutCallsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var calls = scope.ServiceProvider.GetRequiredService<CallRepository>();

            var now = DateTime.UtcNow;
            var threshold = now.AddSeconds(-CallTimeout.TotalSeconds);

            var timedOutCalls = await db.CallLogs
                .Where(c => c.Status == CallStatus.Ringing && c.StartedAt <= threshold)
                .Include(c => c.Conversation)
                .ToListAsync(ct);

            foreach (var call in timedOutCalls)
            {
                _logger.LogInformation("Call {CallId} has timed out (ringing since {StartedAt}). Marking as missed.", call.CallID, call.StartedAt);

                var missed = await calls.MarkCallAsMissedAsync(call.CallID);
                var loaded = await calls.GetByIdAsync(call.CallID);

                var callerName = loaded?.StartedByMember?.User?.DisplayName
                    ?? loaded?.StartedByMember?.User?.Username
                    ?? "Someone";

                var callTypeName = call.Type == CallType.Video ? "video" : "voice";

                // Notify everyone in the conversation about the missed call
                await _hubContext.Clients
                    .Group(call.ConversationID)
                    .SendAsync("CallMissed", call.CallID, call.ConversationID, callerName, call.Type, ct);

                // Create system message for missed call
                await CreateSystemCallMessageAsync(db, call.CallID, call.ConversationID, call.Type, call.StartedBy, missed: true, ct);
            }
        }

        private async Task CreateSystemCallMessageAsync(
            AppDbContext db, string callId, string conversationId,
            CallType callType, string startedByMemberId,
            bool missed, CancellationToken ct)
        {
            var callTypeName = callType == CallType.Video ? "video" : "voice";
            var content = missed
                ? $"Missed {callTypeName} call"
                : $"{callTypeName} call";

            var sysMsg = new Message
            {
                MessageID = NewID(),
                ConversationID = conversationId,
                SenderID = null,
                Type = MessageType.Call,
                Content = content,
                SentAt = DateTime.UtcNow
            };

            db.Messages.Add(sysMsg);
            await db.SaveChangesAsync(ct);

            var response = MessageResponse.From(sysMsg);

            await _hubContext.Clients
                .Group(conversationId)
                .SendAsync("MessageReceived", response, ct);
        }

        private static string NewID()
        {
            var bytes = new byte[6];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
