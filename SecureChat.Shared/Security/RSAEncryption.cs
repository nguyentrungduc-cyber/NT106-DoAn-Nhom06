using System;
using System.Security.Cryptography;
using System.Text;

namespace SecureChat.Shared.Security
{
    public static class RSAEncryption
    {
        public static (string publicKeyPem, string privateKeyPem) GenerateKeyPair(int keySize = 2048)
        {
            using var rsa = RSA.Create(keySize);
            var publicKey = rsa.ExportSubjectPublicKeyInfoPem();
            var privateKey = rsa.ExportPkcs8PrivateKeyPem();
            return (publicKey, privateKey);
        }

        public static byte[] Encrypt(byte[] data, string publicKeyPem)
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            return rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
        }

        public static byte[] Decrypt(byte[] data, string privateKeyPem)
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);
            return rsa.Decrypt(data, RSAEncryptionPadding.OaepSHA256);
        }
    }
}
