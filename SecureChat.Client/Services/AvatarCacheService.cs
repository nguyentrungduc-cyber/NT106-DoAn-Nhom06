using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SecureChat.Client.Services
{
    public static class AvatarCacheService
    {
        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SecureChat", "AvatarCache");

        private static readonly ConcurrentDictionary<string, Image> MemoryCache = new();

        static AvatarCacheService()
        {
            Directory.CreateDirectory(CacheDir);
        }

        private static string GetCacheKey(string url)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        }

        public static string? GetCachedPath(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var path = Path.Combine(CacheDir, GetCacheKey(url) + ".png");
            return File.Exists(path) ? path : null;
        }

        public static async Task<string?> DownloadAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                var cached = GetCachedPath(url);
                if (cached != null) return cached;

                var http = ApiClient.Instance.GetHttpClient();
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode) return null;
                using var stream = await response.Content.ReadAsStreamAsync();
                using var img = Image.FromStream(stream);
                var path = Path.Combine(CacheDir, GetCacheKey(url) + ".png");
                img.Save(path, ImageFormat.Png);
                return path;
            }
            catch
            {
                return null;
            }
        }

        public static Image? LoadImage(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                var key = GetCacheKey(url);
                if (MemoryCache.TryGetValue(key, out var cached))
                    return new Bitmap(cached);

                var path = GetCachedPath(url);
                if (path == null) return null;

                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var img = Image.FromStream(fs);
                var copy = new Bitmap(img);
                MemoryCache[key] = copy;
                return copy;
            }
            catch
            {
                return null;
            }
        }

        public static void Invalidate(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            var key = GetCacheKey(url);
            if (MemoryCache.TryRemove(key, out var old))
                old?.Dispose();
            var path = GetCachedPath(url);
            if (path != null)
            {
                try { File.Delete(path); } catch { }
            }
        }

        public static void Clear()
        {
            foreach (var kv in MemoryCache)
            {
                try { kv.Value?.Dispose(); } catch { }
            }
            MemoryCache.Clear();
        }
    }
}
