using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SecureChat.Client.Services
{
    public static class VoiceEncryptionService
    {
        public static async Task<(string encryptedPath, byte[] key, byte[] iv)> EncryptAsync(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
                throw new FileNotFoundException("Input file does not exist.", inputPath);

            var fileInfo = new FileInfo(inputPath);
            if (fileInfo.Length == 0)
                throw new InvalidOperationException("Input file is empty.");

            string tempDir = Path.GetTempPath();
            string encryptedPath = Path.Combine(tempDir, $"voiceenc_{Guid.NewGuid():N}.dat");

            byte[] key = new byte[32];
            byte[] iv = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key);
                rng.GetBytes(iv);
            }

            FileStream? inputFs = null;
            FileStream? outputFs = null;
            CryptoStream? cryptoStream = null;
            try
            {
                inputFs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                outputFs = new FileStream(encryptedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var aes = Aes.Create();
                aes.KeySize = 256;
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                cryptoStream = new CryptoStream(outputFs, aes.CreateEncryptor(), CryptoStreamMode.Write);

                byte[] buffer = new byte[81920];
                int read;
                while ((read = await inputFs.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                {
                    await cryptoStream.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                }
                await cryptoStream.FlushFinalBlockAsync().ConfigureAwait(false);
            }
            catch
            {
                try { cryptoStream?.Dispose(); } catch { }
                try { outputFs?.Dispose(); } catch { }
                try { if (File.Exists(encryptedPath)) File.Delete(encryptedPath); } catch { }
                throw;
            }
            finally
            {
                try { cryptoStream?.Dispose(); } catch { }
                try { outputFs?.Dispose(); } catch { }
                try { inputFs?.Dispose(); } catch { }
            }

            return (encryptedPath, key, iv);
        }
    }
}
