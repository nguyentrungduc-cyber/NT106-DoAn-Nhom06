using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SecureChat.Shared.Security
{
    /// <summary>
    /// AES-256-CBC helpers cho việc mã hóa / giải mã nội dung tin nhắn (text).
    /// Pair với RSAEncryption (hybrid encryption) cho phần trao đổi khóa.
    ///
    /// Quy ước:
    ///  - Key: 32 bytes (AES-256)
    ///  - IV : 16 bytes (AES block size)
    ///  - Ciphertext, IV được lưu/tunnel ở dạng Base64 string.
    /// </summary>
    public static class AesEncryption
    {
        public const int KeySize = 32; // 256-bit
        public const int IvSize = 16;  // 128-bit block

        public static (byte[] Key, byte[] Iv) GenerateKeyAndIv()
        {
            byte[] key = new byte[KeySize];
            byte[] iv = new byte[IvSize];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(key);
            rng.GetBytes(iv);
            return (key, iv);
        }

        /// <summary>
        /// Mã hóa chuỗi UTF-8 plaintext thành (cipherBase64, ivBase64).
        /// </summary>
        public static (string CipherBase64, string IvBase64) EncryptText(string plaintext, byte[] key, byte[]? iv = null)
        {
            if (plaintext is null) throw new ArgumentNullException(nameof(plaintext));
            ValidateKey(key);

            byte[] effectiveIv;
            if (iv is null)
            {
                effectiveIv = new byte[IvSize];
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(effectiveIv);
            }
            else
            {
                ValidateIv(iv);
                effectiveIv = iv;
            }

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = effectiveIv;

            byte[] data = Encoding.UTF8.GetBytes(plaintext);
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
            }

            return (Convert.ToBase64String(ms.ToArray()), Convert.ToBase64String(effectiveIv));
        }

        /// <summary>
        /// Giải mã ciphertext (Base64) thành chuỗi UTF-8 plaintext.
        /// </summary>
        public static string DecryptText(string cipherBase64, string ivBase64, byte[] key)
        {
            if (string.IsNullOrEmpty(cipherBase64))
                throw new ArgumentException("Cipher text is required.", nameof(cipherBase64));
            if (string.IsNullOrEmpty(ivBase64))
                throw new ArgumentException("IV is required.", nameof(ivBase64));
            ValidateKey(key);

            byte[] cipher = Convert.FromBase64String(cipherBase64);
            byte[] iv = Convert.FromBase64String(ivBase64);
            ValidateIv(iv);

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = iv;

            using var ms = new MemoryStream(cipher);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new StreamReader(cs, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static void ValidateKey(byte[] key)
        {
            if (key is null || key.Length != KeySize)
                throw new ArgumentException($"AES key must be {KeySize} bytes (AES-256).", nameof(key));
        }

        private static void ValidateIv(byte[] iv)
        {
            if (iv is null || iv.Length != IvSize)
                throw new ArgumentException($"AES IV must be {IvSize} bytes.", nameof(iv));
        }
    }
}
