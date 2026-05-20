using System;
using System.Collections.Concurrent;

namespace SecureChat.Shared.Security
{
    public static class KeyManager
    {
        private static string? _publicKeyPem;
        private static string? _privateKeyPem;
        private static readonly ConcurrentDictionary<string, (byte[] Key, byte[] IV)> _aesKeyCache = new();

        public static void SetKeyPair(string publicKeyPem, string privateKeyPem)
        {
            _publicKeyPem = publicKeyPem;
            _privateKeyPem = privateKeyPem;
        }

        public static (string? publicKeyPem, string? privateKeyPem) GetKeyPair()
        {
            return (_publicKeyPem, _privateKeyPem);
        }

        public static void CacheAesKey(string messageId, byte[] key, byte[] iv)
        {
            _aesKeyCache[messageId] = (key, iv);
        }

        public static bool TryGetAesKey(string messageId, out byte[]? key, out byte[]? iv)
        {
            if (_aesKeyCache.TryGetValue(messageId, out var tuple))
            {
                key = tuple.Key;
                iv = tuple.IV;
                return true;
            }
            key = null;
            iv = null;
            return false;
        }

        public static void Clear()
        {
            _publicKeyPem = null;
            _privateKeyPem = null;
            _aesKeyCache.Clear();
        }
    }
}
