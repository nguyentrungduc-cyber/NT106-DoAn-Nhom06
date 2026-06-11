using System;
using System.Threading.Tasks;
using SecureChat.Shared.Security;

namespace SecureChat.Client.Services
{
    /// <summary>
    /// Service chuyên xử lý encryption cho tin nhắn text trước khi gửi lên server.
    /// Đảm bảo E2EE: Client encrypt -> Server store ciphertext -> Receiver decrypt.
    /// </summary>
    public sealed class MessageEncryptionService
    {
        /// <summary>
        /// Mã hóa nội dung tin nhắn text bằng AES-256-CBC.
        /// </summary>
        /// <param name="plaintext">Nội dung tin nhắn gốc (plaintext)</param>
        /// <param name="conversationKey">Khóa AES-256 của conversation (32 bytes)</param>
        /// <returns>Tuple chứa (encryptedContent, contentIV) dạng Base64</returns>
        /// <exception cref="ArgumentNullException">Khi plaintext hoặc conversationKey null</exception>
        /// <exception cref="ArgumentException">Khi plaintext rỗng hoặc conversationKey không đúng kích thước</exception>
        /// <exception cref="System.Security.Cryptography.CryptographicException">Khi encryption thất bại</exception>
        public (string EncryptedContent, string ContentIV) EncryptMessage(string plaintext, byte[] conversationKey)
        {
            // Validation chặt chẽ
            if (plaintext is null)
                throw new ArgumentNullException(nameof(plaintext), "Message content cannot be null.");

            if (string.IsNullOrWhiteSpace(plaintext))
                throw new ArgumentException("Message content cannot be empty or whitespace.", nameof(plaintext));

            if (conversationKey is null)
                throw new ArgumentNullException(nameof(conversationKey), "Conversation key cannot be null.");

            if (conversationKey.Length != AesEncryption.KeySize)
                throw new ArgumentException(
                    $"Conversation key must be {AesEncryption.KeySize} bytes (AES-256), but got {conversationKey.Length} bytes.",
                    nameof(conversationKey));

            try
            {
                // Encrypt message content với AES-256-CBC
                // AesEncryption.EncryptText tự động generate IV mới cho mỗi message
                var (cipherBase64, ivBase64) = AesEncryption.EncryptText(plaintext, conversationKey);

                // Validation output
                if (string.IsNullOrWhiteSpace(cipherBase64))
                    throw new InvalidOperationException("Encryption failed: cipher text is empty.");

                if (string.IsNullOrWhiteSpace(ivBase64))
                    throw new InvalidOperationException("Encryption failed: IV is empty.");

                return (cipherBase64, ivBase64);
            }
            catch (ArgumentException ex)
            {
                // Re-throw với context rõ ràng hơn
                throw new InvalidOperationException($"Message encryption failed due to invalid argument: {ex.Message}", ex);
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                // Re-throw với context rõ ràng hơn
                throw new InvalidOperationException($"Message encryption failed due to cryptographic error: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                // Catch-all cho các lỗi không mong đợi
                throw new InvalidOperationException($"Message encryption failed unexpectedly: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Async wrapper cho EncryptMessage để tương thích với async/await pattern.
        /// </summary>
        public Task<(string EncryptedContent, string ContentIV)> EncryptMessageAsync(string plaintext, byte[] conversationKey)
        {
            // Encryption operation là CPU-bound, không cần async thực sự
            // Nhưng cung cấp async API để tương thích với UI thread
            return Task.Run(() => EncryptMessage(plaintext, conversationKey));
        }

        /// <summary>
        /// Validate conversation key trước khi sử dụng.
        /// </summary>
        /// <param name="conversationKey">Khóa AES cần validate</param>
        /// <returns>True nếu key hợp lệ, False nếu không</returns>
        public bool ValidateConversationKey(byte[] conversationKey)
        {
            if (conversationKey is null)
                return false;

            if (conversationKey.Length != AesEncryption.KeySize)
                return false;

            // Check if key is all zeros (invalid key)
            bool allZeros = true;
            for (int i = 0; i < conversationKey.Length; i++)
            {
                if (conversationKey[i] != 0)
                {
                    allZeros = false;
                    break;
                }
            }

            return !allZeros;
        }

        /// <summary>
        /// Generate một conversation key mới (AES-256).
        /// Dùng khi tạo conversation mới.
        /// </summary>
        /// <returns>32-byte AES key</returns>
        public byte[] GenerateConversationKey()
        {
            var (key, _) = AesEncryption.GenerateKeyAndIv();
            return key;
        }
    }
}
