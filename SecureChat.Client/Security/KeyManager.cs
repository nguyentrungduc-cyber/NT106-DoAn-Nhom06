using System;
using System.Security.Cryptography;

namespace SecureChat.Client.Security
{
    public static class RSAKeyManager
    {
        /// <summary>
        /// Sinh ra cặp khóa: Public (đưa cho Server) và Private (Giữ lại máy Client)
        /// Dùng chuẩn PEM để tương thích với RSAEncryption trong Shared.Security
        /// </summary>
        public static (string PublicKey, string PrivateKey) GenerateRSAKeys()
        {
            using (var rsa = RSA.Create(2048))
            {
                string publicKey = rsa.ExportSubjectPublicKeyInfoPem();
                string privateKey = rsa.ExportPkcs8PrivateKeyPem();
                return (publicKey, privateKey);
            }
        }
    }
}
