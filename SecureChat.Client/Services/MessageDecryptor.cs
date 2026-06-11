using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using SecureChat.Client.Services.Api;
using SecureChat.DTOs;
using SecureChat.Shared.Security;

namespace SecureChat.Client.Services
{
    /// <summary>
    /// Pipeline thống nhất cho luồng "Nhận tin -> Decrypt -> Hiển thị":
    ///
    ///  1. <see cref="ProcessAsync"/> nhận một <see cref="MessageResponse"/>
    ///     từ Server (do đồng bộ MariaDB hoặc do SignalR đẩy đến).
    ///  2. Cho mỗi attachment có hybrid-encrypted AES key dành cho user hiện
    ///     tại, RSA-giải mã rồi cache vào <see cref="KeyManager"/>
    ///     (giữ nguyên tương thích với code voice/file đang sử dụng).
    ///  3. Nếu message có <c>ContentIV</c> và có conversation key đang được
    ///     cache (lấy từ <see cref="EnsureConversationKeyAsync"/>) thì AES-256
    ///     giải mã <c>Content</c>. Ngược lại trả về plaintext cũ.
    ///
    /// Lưu ý:
    ///  - Toàn bộ logic giải mã chạy trên client. Server không bao giờ thấy
    ///    plaintext (theo nguyên tắc E2EE).
    ///  - Conversation key được suy ra từ
    ///    <see cref="MemberResponse.EncryptedKey"/> của chính user (RSA-bọc
    ///    bằng public key của user) — fetch một lần rồi cache.
    /// </summary>
    public sealed class MessageDecryptor
    {
        private readonly MessageService _messageService;
        private readonly ConcurrentDictionary<string, byte[]> _conversationKeys = new();

        public MessageDecryptor() : this(new MessageService()) { }

        public MessageDecryptor(MessageService messageService)
        {
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        }

        /// <summary>UserID của user đang đăng nhập. Bắt buộc set trước khi xử lý.</summary>
        public string CurrentUserId { get; set; } = string.Empty;

        /// <summary>
        /// Lấy / cache conversation AES key cho user hiện tại.
        /// Trả về null nếu server chưa có key hợp lệ (legacy data) — caller
        /// sẽ fallback sang plaintext content.
        /// </summary>
        public async Task<byte[]?> EnsureConversationKeyAsync(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                return null;

            if (_conversationKeys.TryGetValue(conversationId, out var cached))
                return cached;

            var (ok, me, _) = await _messageService.GetMyMembershipAsync(conversationId).ConfigureAwait(false);
            if (!ok || me is null || string.IsNullOrWhiteSpace(me.EncryptedKey))
                return null;

            var (_, privateKeyPem) = KeyManager.GetKeyPair();
            if (string.IsNullOrWhiteSpace(privateKeyPem))
                return null;

            try
            {
                byte[] cipher = Convert.FromBase64String(me.EncryptedKey);
                byte[] key = RSAEncryption.Decrypt(cipher, privateKeyPem);
                if (key.Length != AesEncryption.KeySize)
                    return null;

                _conversationKeys[conversationId] = key;
                return key;
            }
            catch (FormatException)
            {
                return null;
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return null;
            }
        }

        /// <summary>
        /// Xóa conversation key đã cache (gọi khi rời / xoá conversation).
        /// </summary>
        public void ForgetConversation(string conversationId)
        {
            if (!string.IsNullOrWhiteSpace(conversationId))
                _conversationKeys.TryRemove(conversationId, out _);
        }

        public void ForgetAll() => _conversationKeys.Clear();

        /// <summary>
        /// Giải mã 1 message và build view-model dạng tuple đã được
        /// <c>frmMainChat</c> sử dụng: (Id, Text, Out, Time, Sender).
        /// </summary>
        public async Task<DecryptedMessage> ProcessAsync(MessageResponse message, string? myMemberId = null)
        {
            ArgumentNullException.ThrowIfNull(message);

            // 1. Hybrid AES key cho từng attachment (file / voice).
            if (message.Attachments is not null)
            {
                foreach (var attachment in message.Attachments)
                    TryCacheAttachmentKey(message.MessageID, attachment);
            }

            // 2. Text content
            string content = message.Content ?? string.Empty;
            if (!string.IsNullOrEmpty(content) && !string.IsNullOrEmpty(message.ContentIV))
            {
                var key = await EnsureConversationKeyAsync(message.ConversationID).ConfigureAwait(false);
                if (key is not null)
                {
                    try
                    {
                        content = AesEncryption.DecryptText(content, message.ContentIV!, key);
                    }
                    catch (FormatException)
                    {
                        // Ciphertext không phải Base64 hợp lệ -> giữ nguyên content
                    }
                    catch (System.Security.Cryptography.CryptographicException)
                    {
                        // Sai key/iv -> giữ nguyên (UI sẽ hiển thị placeholder)
                    }
                }
            }

            bool isOut = !string.IsNullOrWhiteSpace(myMemberId)
                ? string.Equals(message.SenderID, myMemberId, StringComparison.Ordinal)
                : !string.IsNullOrWhiteSpace(message.SenderUsername)
                    && string.Equals(message.SenderUsername, CurrentUserId, StringComparison.Ordinal);

            return new DecryptedMessage(
                message.MessageID,
                content,
                isOut,
                message.SentAt.ToLocalTime().ToString("h:mm tt"),
                message.SenderUsername ?? string.Empty,
                message);
        }

        private static void TryCacheAttachmentKey(string messageId, AttachmentResponse attachment)
        {
            if (attachment is null) return;
            if (string.IsNullOrWhiteSpace(attachment.EncryptedAesKey)
                || string.IsNullOrWhiteSpace(attachment.EncryptedAesIv))
                return;

            var (_, privateKey) = KeyManager.GetKeyPair();
            if (string.IsNullOrWhiteSpace(privateKey))
                return;

            try
            {
                byte[] aesKey = RSAEncryption.Decrypt(Convert.FromBase64String(attachment.EncryptedAesKey), privateKey);
                byte[] aesIv = RSAEncryption.Decrypt(Convert.FromBase64String(attachment.EncryptedAesIv), privateKey);
                KeyManager.CacheAesKey(messageId, aesKey, aesIv);
            }
            catch (FormatException)
            {
                // Bỏ qua attachment có ciphertext không hợp lệ
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // Attachment dành cho receiver khác -> không decrypt được, bỏ qua
            }
        }
    }

    /// <summary>
    /// Kết quả giải mã phục vụ render UI. <see cref="Raw"/> giữ nguyên payload
    /// gốc cho các tác vụ cần thêm metadata (reactions, attachments, ...).
    /// </summary>
    public sealed record DecryptedMessage(
        string Id,
        string Text,
        bool Out,
        string Time,
        string Sender,
        MessageResponse Raw);
}
