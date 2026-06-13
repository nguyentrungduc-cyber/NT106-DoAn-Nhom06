using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SecureChat.DTOs;

namespace SecureChat.Client.Services
{
    /// <summary>
    /// Service ?? l?y th�ng tin conversation t? server.
    /// D�ng cho vi?c encrypt attachment cho multi-recipient.
    /// </summary>
    public class ConversationService
    {
        private readonly ApiClient _apiClient = ApiClient.Instance;

        /// <summary>
        /// L?y danh s�ch members c?a m?t conversation.
        /// M?i member bao g?m User info v?i public key.
        /// </summary>
        public async Task<(bool Ok, List<MemberResponse>? Members, string Error)> GetConversationMembersAsync(string conversationId)
        {
            ArgumentNullException.ThrowIfNull(conversationId);

            try
            {
                var response = await _apiClient.GetHttpClient().GetAsync($"api/conversations/{conversationId}/members");
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (false, null, $"Failed to fetch members: {content}");
                }

                var opts = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = false,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };

                var members = JsonSerializer.Deserialize<List<MemberResponse>>(content, opts);

                if (members == null || members.Count == 0)
                {
                    return (false, null, "No members found in conversation");
                }

                // Filter out members without public keys (they can't receive encrypted attachments)
                var validMembers = members.Where(m => m.User != null && !string.IsNullOrEmpty(m.User.PublicKey)).ToList();
                if (validMembers.Count == 0)
                {
                    return (false, null, "No members with valid public keys found.");
                }

                return (true, validMembers, string.Empty);
            }
            catch (JsonException jsonEx)
            {
                return (false, null, $"JSON parsing error: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                return (false, null, $"Error fetching members: {ex.Message}");
            }
        }

        /// <summary>
        /// L?y public key c?a m?t user.
        /// </summary>
        public async Task<(bool Ok, string? PublicKey, string Error)> GetUserPublicKeyAsync(string userId)
        {
            ArgumentNullException.ThrowIfNull(userId);

            try
            {
                var response = await _apiClient.GetHttpClient().GetAsync($"api/users/{userId}");
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (false, null, $"Failed to fetch user: {content}");
                }

                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var user = JsonSerializer.Deserialize<UserResponse>(content, opts);

                if (user?.PublicKey == null)
                {
                    return (false, null, "User public key not available");
                }

                return (true, user.PublicKey, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }
    }
}
