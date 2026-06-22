using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace SecureChat.Server.Hubs
{
    /// <summary>
    /// Map SignalR connection → UserID từ JWT claim NameIdentifier.
    /// Cho phép dùng Clients.User(userId) trên toàn bộ Hub.
    /// </summary>
    public class UserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
            => connection.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
