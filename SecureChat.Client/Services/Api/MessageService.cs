using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SecureChat.DTOs;

namespace SecureChat.Client.Services.Api
{
    /// <summary>
    /// Wrapper gọi các endpoint REST của Server cho phần conversations / messages.
    /// Phục vụ chức năng "Sync tin nhắn từ MariaDB" trên Client:
    ///   - Lấy danh sách conversation hiện tại của user.
    ///   - Lấy danh sách thành viên (cần để biết MemberID -> UserID khi giải mã / hiển thị).
    ///   - Lấy danh sách tin nhắn gần nhất (paging theo `before`).
    ///   - Đánh dấu đã đọc.
    ///
    /// Mọi hàm đều dùng <see cref="ApiClient"/> (singleton) — đã được set JWT
    /// trong Authorization header sau khi login.
    /// </summary>
    public sealed class MessageService
    {
        private readonly ApiClient _api;

        public MessageService() : this(ApiClient.Instance) { }

        public MessageService(ApiClient apiClient)
        {
            _api = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        }

        /// <summary>GET /api/conversations</summary>
        public Task<(bool Ok, List<ConversationResponse>? Data, string Err)> GetMyConversationsAsync()
            => _api.GetAsync<List<ConversationResponse>>("api/conversations");

        /// <summary>GET /api/conversations/saved — auto-creates Saved Messages if missing</summary>
        public Task<(bool Ok, ConversationResponse? Data, string Err)> GetOrCreateSavedConversationAsync()
            => _api.GetAsync<ConversationResponse>("api/conversations/saved");

        /// <summary>GET /api/conversations/{conversationID}</summary>
        public Task<(bool Ok, ConversationResponse? Data, string Err)> GetConversationAsync(string conversationId)
        {
            EnsureId(conversationId, nameof(conversationId));
            return _api.GetAsync<ConversationResponse>($"api/conversations/{conversationId}");
        }

        /// <summary>GET /api/conversations/{conversationID}/members</summary>
        public Task<(bool Ok, List<MemberResponse>? Data, string Err)> GetMembersAsync(string conversationId)
        {
            EnsureId(conversationId, nameof(conversationId));
            return _api.GetAsync<List<MemberResponse>>($"api/conversations/{conversationId}/members");
        }

        /// <summary>GET /api/conversations/{conversationID}/members/me</summary>
        public Task<(bool Ok, MemberResponse? Data, string Err)> GetMyMembershipAsync(string conversationId)
        {
            EnsureId(conversationId, nameof(conversationId));
            return _api.GetAsync<MemberResponse>($"api/conversations/{conversationId}/members/me");
        }

        /// <summary>
        /// GET /api/conversations/{conversationID}/messages
        /// Server trả về danh sách <see cref="MessageResponse"/> sắp xếp giảm dần
        /// theo SentAt. Limit mặc định = 50, có thể truyền `before` để paging cũ hơn.
        /// </summary>
        public Task<(bool Ok, List<MessageResponse>? Data, string Err)> GetMessagesAsync(
            string conversationId, int limit = 50, DateTime? before = null)
        {
            EnsureId(conversationId, nameof(conversationId));
            if (limit <= 0) limit = 50;

            var endpoint = $"api/conversations/{conversationId}/messages?limit={limit}";
            if (before.HasValue)
                endpoint += $"&before={Uri.EscapeDataString(before.Value.ToUniversalTime().ToString("o"))}";

            return _api.GetAsync<List<MessageResponse>>(endpoint);
        }

        /// <summary>POST /api/conversations/{conversationID}/messages/{messageID}/read</summary>
        public Task<(bool Ok, MessageStatusResponse? Data, string Err)> MarkReadAsync(string conversationId, string messageId)
        {
            EnsureId(conversationId, nameof(conversationId));
            EnsureId(messageId, nameof(messageId));
            return _api.PostAsync<object, MessageStatusResponse>(
                $"api/conversations/{conversationId}/messages/{messageId}/read",
                new { });
        }

        /// <summary>GET /api/conversations/{conversationID}/messages/unread</summary>
        public Task<(bool Ok, UnreadCountResponse? Data, string Err)> GetUnreadCountAsync(string conversationId)
        {
            EnsureId(conversationId, nameof(conversationId));
            return _api.GetAsync<UnreadCountResponse>($"api/conversations/{conversationId}/messages/unread");
        }

        private static void EnsureId(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Id is required.", paramName);
        }
    }
}
