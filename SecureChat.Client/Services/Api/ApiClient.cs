using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SecureChat.Client.Services
{
    public class ApiClient
    {
        private const string DefaultBaseUrl = "http://localhost:5097/";
        private readonly HttpClient _httpClient;
        private static ApiClient _instance;
        private string? _accessToken;

        // Singleton Pattern: Đảm bảo toàn bộ App chỉ dùng chung 1 instance HttpClient
        public static ApiClient Instance => _instance ??= new ApiClient();

        private ApiClient()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(ResolveBaseUrl())
            };
        }

        public HttpClient GetHttpClient() => _httpClient;

        // POST multipart/form-data using the singleton HttpClient (this preserves Authorization header)
        public async Task<(bool IsSuccess, string ResponseContent, string ErrorMessage)> PostMultipartAsync(string endpoint, MultipartFormDataContent content)
        {
            try
            {
                var response = await _httpClient.PostAsync(endpoint, content);
                var responseStr = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    return (true, responseStr, string.Empty);
                }
                return (false, responseStr, $"Lỗi server: {responseStr}");
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"Không thể kết nối máy chủ: {ex.Message}");
            }
        }

        public static HttpClient Create(string? baseUrl = null)
        {
            var resolvedBaseUrl = ResolveBaseUrl(baseUrl);
            return new HttpClient
            {
                BaseAddress = new Uri(resolvedBaseUrl, UriKind.Absolute)
            };
        }

        private static string ResolveBaseUrl(string? overrideBaseUrl = null)
        {
            return overrideBaseUrl
                ?? Environment.GetEnvironmentVariable("SECURECHAT_API_BASE_URL")
                ?? DefaultBaseUrl;
        }

        // Lưu JWT Token vào Header cho các request cần xác thực (Chat, Lấy danh sách bạn bè...)
        public void SetAccessToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _accessToken = token;
        }

        public void ClearToken()
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
            _accessToken = null;
        }

        /// <summary>
        /// Gets the current JWT access token, if set.
        /// </summary>
        public string? CurrentAccessToken => _accessToken;

        // Hybrid encryption: Register public key to server
        public async Task RegisterPublicKeyAsync(string publicKeyPem)
        {
            if (string.IsNullOrWhiteSpace(publicKeyPem))
                throw new ArgumentException("Public key is required.", nameof(publicKeyPem));

            var payload = new { publicKey = publicKeyPem };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Patch, "api/users/me/public-key")
            {
                Content = content
            };

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Không thể đăng ký public key: {error}");
            }
        }

        // Hybrid encryption: Fetch receiver public key from server
        public async Task<string?> GetPublicKeyAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("UserId is required.", nameof(userId));

            var response = await _httpClient.GetAsync($"api/users/{userId}");
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("publicKey").GetString();
        }

        public async Task<string?> GetCurrentUserIdAsync()
        {
            var response = await _httpClient.GetAsync("api/users/me");
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("userID").GetString();
        }

        // Attempts to notify server of logout (DELETE /api/auth/logout) and clears the local token.
        // This method never throws; failures are logged internally via return value.
        public async Task<bool> LogoutAsync()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, "api/auth/logout");
                var response = await _httpClient.SendAsync(request);
                // Regardless of response, clear local authorization header
                ClearToken();
                return response.IsSuccessStatusCode;
            }
            catch
            {
                // network errors or other issues - still clear local token to ensure user is logged out locally
                ClearToken();
                return false;
            }
        }

        // Base hàm POST
        public async Task<(bool IsSuccess, TResponse Data, string ErrorMessage)> PostAsync<TRequest, TResponse>(string endpoint, TRequest payload)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(endpoint, content);
                var responseStr = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var data = JsonSerializer.Deserialize<TResponse>(responseStr, options);
                    return (true, data, string.Empty);
                }

                return (false, default, $"Lỗi server: {responseStr}");
            }
            catch (Exception ex)
            {
                return (false, default, $"Không thể kết nối máy chủ: {ex.Message}");
            }
        }

        // Generic GET helper added to support deconstruction calls like: var (ok, data, err) = await ApiClient.Instance.GetAsync<T>(url);
        public async Task<(bool IsSuccess, T? Data, string ErrorMessage)> GetAsync<T>(string endpoint)
        {
            try
            {
                var response = await _httpClient.GetAsync(endpoint);
                var responseStr = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var data = JsonSerializer.Deserialize<T>(responseStr, options);
                    return (true, data, string.Empty);
                }

                return (false, default, $"Lỗi server: {responseStr}");
            }
            catch (Exception ex)
            {
                return (false, default, $"Không thể kết nối máy chủ: {ex.Message}");
            }
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> DeleteAsync(string endpoint)
        {
            try
            {
                var response = await _httpClient.DeleteAsync(endpoint);
                if (response.IsSuccessStatusCode)
                {
                    return (true, string.Empty);
                }
                var error = await response.Content.ReadAsStringAsync();
                return (false, $"Lỗi server: {error}");
            }
            catch (Exception ex)
            {
                return (false, $"Không thể kết nối máy chủ: {ex.Message}");
            }
        }

        // Base hàm GET có deserialize JSON
        public async Task<(bool IsSuccess, TResponse? Data, string ErrorMessage)> GetAsync<TResponse>(string endpoint)
        {
            try
            {
                var response = await _httpClient.GetAsync(endpoint);
                var responseStr = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var data = JsonSerializer.Deserialize<TResponse>(responseStr, options);
                    return (true, data, string.Empty);
                }

                return (false, default, $"Lỗi server: {responseStr}");
            }
            catch (Exception ex)
            {
                return (false, default, $"Không thể kết nối máy chủ: {ex.Message}");
            }
        }
    }
}
