using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        private readonly ConcurrentDictionary<string, List<byte[]>> _oldConversationKeys = new();
        private static readonly string _keyHistoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SecureChat", "key_history.json");

        public MessageDecryptor() : this(new MessageService()) { }

        public MessageDecryptor(MessageService messageService)
        {
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            LoadKeyHistory();
        }

        /// <summary>UserID của user đang đăng nhập. Bắt buộc set trước khi xử lý.</summary>
        public string CurrentUserId { get; set; } = string.Empty;
        public string CurrentUsername { get; set; } = string.Empty;

        /// <summary>
        /// Lấy / cache conversation AES key cho user hiện tại.
        /// Nếu server chưa có key hợp lệ (legacy "TBD") hoặc key cũ không giải
        /// mã được với key pair hiện tại, tự động rekey toàn bộ conversation:
        /// sinh AES key mới, mã hoá RSA cho từng member, PATCH lên server.
        /// </summary>
        public async Task<byte[]?> EnsureConversationKeyAsync(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                return null;

            if (_conversationKeys.TryGetValue(conversationId, out var cached))
                return cached;

            // Get current user's member info with encrypted conversation key
            var (ok, me, err) = await _messageService.GetMyMembershipAsync(conversationId).ConfigureAwait(false);
            if (!ok || me is null)
            {
                System.Diagnostics.Debug.WriteLine($"[MessageDecryptor] Failed to get my membership for {conversationId}: {err}");
                return null;
            }

            // Try to decrypt existing key; if it fails, trigger rekey
            byte[]? key = TryDecryptKey(me.EncryptedKey);
            if (key is not null)
            {
                _conversationKeys[conversationId] = key;
                SaveKeyHistory();
                return key;
            }

            System.Diagnostics.Debug.WriteLine($"[MessageDecryptor] Existing key invalid for {conversationId}, triggering rekey...");
            return await RekeyConversationAsync(conversationId).ConfigureAwait(false);
        }

        private byte[]? TryDecryptKey(string? encryptedKeyBase64)
        {
            if (string.IsNullOrWhiteSpace(encryptedKeyBase64))
                return null;

            var (_, privateKeyPem) = KeyManager.GetKeyPair();
            if (string.IsNullOrWhiteSpace(privateKeyPem))
                return null;

            try
            {
                byte[] cipher = Convert.FromBase64String(encryptedKeyBase64);
                byte[] key = RSAEncryption.Decrypt(cipher, privateKeyPem);

                if (key.Length != AesEncryption.KeySize)
                {
                    System.Diagnostics.Debug.WriteLine($"[MessageDecryptor] Decrypted key wrong size: {key.Length} vs {AesEncryption.KeySize}");
                    return null;
                }

                return key;
            }
            catch (FormatException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MessageDecryptor] Base64 decode failed: {ex.Message}");
                return null;
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MessageDecryptor] RSA decryption failed: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MessageDecryptor] Unexpected error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sinh AES key mới, RSA-encrypt cho từng active member, PATCH lên server.
        /// Cache key local rồi trả về.
        /// </summary>
        private async Task<byte[]?> RekeyConversationAsync(string conversationId)
        {
            var (ok, members, err) = await _messageService.GetMembersAsync(conversationId).ConfigureAwait(false);
            if (!ok || members is null || members.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[MessageDecryptor] Rekey: failed to get members: {err}");
                return null;
            }

            // Only active members with valid public keys
            var active = members
                .Where(m => m.LeftAt is null && m.User?.PublicKey is not null)
                .ToList();

            if (active.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[MessageDecryptor] Rekey: no active members with public keys");
                return null;
            }

            // Generate new AES-256 key
            byte[] newKey = new byte[AesEncryption.KeySize];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(newKey);
            }

            // RSA-encrypt the key for each member
            var updates = new System.Collections.Generic.List<(string MemberId, string EncryptedB64)>();
            foreach (var member in active)
            {
                try
                {
                    byte[] enc = RSAEncryption.Encrypt(newKey, member.User!.PublicKey);
                    updates.Add((member.MemberID, Convert.ToBase64String(enc)));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MessageDecryptor] Rekey: failed to encrypt for member {member.MemberID}: {ex.Message}");
                }
            }

            if (updates.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[MessageDecryptor] Rekey: no members could be encrypted for");
                return null;
            }

            // PATCH each member's encrypted key
            var api = ApiClient.Instance;
            foreach (var (memberId, encryptedB64) in updates)
            {
                var req = new UpdateMemberRequest(null, null, null, null, encryptedB64);
                var (patchOk, _, patchErr) = await api.PatchAsync<UpdateMemberRequest, MemberResponse>(
                    $"api/conversations/{conversationId}/members/{memberId}", req).ConfigureAwait(false);
                if (!patchOk)
                {
                    System.Diagnostics.Debug.WriteLine($"[MessageDecryptor] Rekey: failed PATCH for member {memberId}: {patchErr}");
                }
            }

            // Save old key before overwriting
            if (_conversationKeys.TryGetValue(conversationId, out var oldKey))
            {
                var history = _oldConversationKeys.GetOrAdd(conversationId, _ => new List<byte[]>());
                lock (history)
                {
                    history.Insert(0, oldKey);
                }
            }

            _conversationKeys[conversationId] = newKey;
            SaveKeyHistory();
            System.Diagnostics.Debug.WriteLine($"[MessageDecryptor] Rekey complete for conversation {conversationId}");
            return newKey;
        }

        /// <summary>
        /// Xóa conversation key đã cache (gọi khi rời / xoá conversation).
        /// </summary>
        public void ForgetConversation(string conversationId)
        {
            if (!string.IsNullOrWhiteSpace(conversationId))
            {
                _conversationKeys.TryRemove(conversationId, out _);
                _oldConversationKeys.TryRemove(conversationId, out _);
                SaveKeyHistory();
            }
        }

        public void ForgetAll()
        {
            _conversationKeys.Clear();
            _oldConversationKeys.Clear();
        }

        /// <summary>
        /// Thử giải mã với các key cũ (từ lịch sử rekey).
        /// </summary>
        private string? TryDecryptWithOldKeys(string content, string contentIV, string conversationId)
        {
            if (!_oldConversationKeys.TryGetValue(conversationId, out var history) || history.Count == 0)
                return null;

            lock (history)
            {
                foreach (var oldKey in history)
                {
                    try
                    {
                        return AesEncryption.DecryptText(content, contentIV, oldKey);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            return null;
        }

        public void SaveKeyHistory()
        {
            try
            {
                var dir = Path.GetDirectoryName(_keyHistoryPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var data = new KeyHistoryData
                {
                    CurrentKeys = _conversationKeys.ToDictionary(kvp => kvp.Key, kvp => Convert.ToBase64String(kvp.Value)),
                    OldKeys = _oldConversationKeys.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Select(b => Convert.ToBase64String(b)).ToList())
                };

                var json = JsonSerializer.Serialize(data);
                File.WriteAllText(_keyHistoryPath, json);
            }
            catch
            {
                // Best-effort — nếu không ghi được thì ignore
            }
        }

        public void LoadKeyHistory()
        {
            try
            {
                if (!File.Exists(_keyHistoryPath)) return;

                var json = File.ReadAllText(_keyHistoryPath);
                var data = JsonSerializer.Deserialize<KeyHistoryData>(json);
                if (data is null) return;

                foreach (var kvp in data.CurrentKeys)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Value))
                        _conversationKeys[kvp.Key] = Convert.FromBase64String(kvp.Value);
                }

                foreach (var kvp in data.OldKeys)
                {
                    var list = kvp.Value
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(Convert.FromBase64String)
                        .ToList();
                    if (list.Count > 0)
                        _oldConversationKeys[kvp.Key] = list;
                }
            }
            catch
            {
                // File hỏng → bắt đầu lại với lịch sử trống
                try { File.Delete(_keyHistoryPath); } catch { }
            }
        }

        private sealed class KeyHistoryData
        {
            public Dictionary<string, string>? CurrentKeys { get; set; }
            public Dictionary<string, List<string>>? OldKeys { get; set; }
        }

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
                        // Key mismatch — possibly a rekey happened.
                        // Forget cache, force re-fetch, retry once.
                        System.Diagnostics.Debug.WriteLine(
                            $"[MessageDecryptor] Decrypt failed for {message.MessageID}, re-fetching key...");
                        ForgetConversation(message.ConversationID);
                        var freshKey = await EnsureConversationKeyAsync(message.ConversationID).ConfigureAwait(false);
                        if (freshKey is not null)
                        {
                            try
                            {
                                content = AesEncryption.DecryptText(content, message.ContentIV!, freshKey);
                            }
                            catch
                            {
                                // Current key fails → try old keys from history
                                content = TryDecryptWithOldKeys(content, message.ContentIV!, message.ConversationID)
                                    ?? "[Tin nhắn cũ không thể giải mã - khóa đã thay đổi]";
                            }
                        }
                        else
                        {
                            content = TryDecryptWithOldKeys(content, message.ContentIV!, message.ConversationID)
                                ?? "[Tin nhắn cũ không thể giải mã - khóa đã thay đổi]";
                        }
                    }
                }
            }

            bool isOut = !string.IsNullOrWhiteSpace(myMemberId)
                ? string.Equals(message.SenderID, myMemberId, StringComparison.Ordinal)
                : !string.IsNullOrWhiteSpace(message.SenderUsername)
                    && string.Equals(message.SenderUsername, CurrentUsername, // ← đổi CurrentUserId → CurrentUsername
                        StringComparison.Ordinal);

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
        MessageResponse Raw)
    {
        public string SenderDisplayName => !string.IsNullOrWhiteSpace(Raw.SenderDisplayName) ? Raw.SenderDisplayName : Sender;
    }
}
