using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

namespace SecureChat.Shared.Security
{
    public static class KeyManager
    {
        private static string? _publicKeyPem;
        private static string? _privateKeyPem;
        private static readonly ConcurrentDictionary<string, (byte[] Key, byte[] IV)> _aesKeyCache = new();
        private static readonly object _lock = new();

        private static string KeyFilePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SecureChat",
                "rsa_key.json"
            );

        public static void SetKeyPair(string publicKeyPem, string privateKeyPem)
        {
            lock (_lock)
            {
                _publicKeyPem = publicKeyPem;
                _privateKeyPem = privateKeyPem;
                SaveToDisk();
            }
        }

        public static (string? publicKeyPem, string? privateKeyPem) GetKeyPair()
        {
            lock (_lock)
            {
                if (_publicKeyPem is not null && _privateKeyPem is not null)
                    return (_publicKeyPem, _privateKeyPem);

                LoadFromDisk();
                return (_publicKeyPem, _privateKeyPem);
            }
        }

        private static void SaveToDisk()
        {
            try
            {
                var dir = Path.GetDirectoryName(KeyFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var data = new { PublicKey = _publicKeyPem, PrivateKey = _privateKeyPem };
                var json = JsonSerializer.Serialize(data);
                File.WriteAllText(KeyFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KeyManager] Failed to save keys: {ex.Message}");
            }
        }

        private static void LoadFromDisk()
        {
            try
            {
                if (!File.Exists(KeyFilePath))
                    return;

                var json = File.ReadAllText(KeyFilePath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("PublicKey", out var pub) && pub.ValueKind == JsonValueKind.String)
                    _publicKeyPem = pub.GetString();
                if (root.TryGetProperty("PrivateKey", out var priv) && priv.ValueKind == JsonValueKind.String)
                    _privateKeyPem = priv.GetString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KeyManager] Failed to load keys: {ex.Message}");
            }
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

        /// <summary>
        /// Clear in-memory state only (RSA keys + AES cache). Does NOT delete the persisted key file.
        /// Called on normal app exit / form close to clear sensitive data from memory.
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _publicKeyPem = null;
                _privateKeyPem = null;
                _aesKeyCache.Clear();
            }
        }

        /// <summary>
        /// Clear in-memory state AND delete the persisted key file from disk.
        /// Called on logout (account switch) so the next user generates their own key pair.
        /// </summary>
        public static void Purge()
        {
            lock (_lock)
            {
                _publicKeyPem = null;
                _privateKeyPem = null;
                _aesKeyCache.Clear();

                try
                {
                    if (File.Exists(KeyFilePath))
                        File.Delete(KeyFilePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[KeyManager] Failed to delete key file: {ex.Message}");
                }
            }
        }
    }
}
