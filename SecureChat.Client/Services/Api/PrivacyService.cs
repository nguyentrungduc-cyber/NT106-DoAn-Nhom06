using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using SecureChat.Client.Constants;
using SecureChat.Client.Models;

namespace SecureChat.Client.Services.Api
{
    public record PrivacySettingsDto
    {
        public string LastSeenPrivacy { get; init; } = "Everybody";
        public string ProfilePhotoPrivacy { get; init; } = "Everybody";
        public string ForwardedMessagesPrivacy { get; init; } = "Everybody";
        public string CallsPrivacy { get; init; } = "Everybody";
        public string VoiceMessagesPrivacy { get; init; } = "Everybody";
        public string MessagesPrivacy { get; init; } = "Everybody";
        public string BirthdayPrivacy { get; init; } = "Everybody";
        public string BioPrivacy { get; init; } = "Everybody";
        public string AutoDeleteMode { get; init; } = "Off";
    }

    public record UpdatePrivacySettingsDto
    {
        public string? LastSeenPrivacy { get; init; }
        public string? ProfilePhotoPrivacy { get; init; }
        public string? ForwardedMessagesPrivacy { get; init; }
        public string? CallsPrivacy { get; init; }
        public string? VoiceMessagesPrivacy { get; init; }
        public string? MessagesPrivacy { get; init; }
        public string? BirthdayPrivacy { get; init; }
        public string? BioPrivacy { get; init; }
        public string? AutoDeleteMode { get; init; }
    }

    public sealed class PrivacyService
    {
        private readonly ApiClient _client;

        public PrivacyService()
        {
            _client = ApiClient.Instance;
        }

        public async Task<ServiceResult<PrivacySettingsDto>> GetSettingsAsync()
        {
            try
            {
                var (ok, data, err) = await _client.GetAsync<PrivacySettingsDto>("api/privacy/settings");
                if (ok && data is not null)
                    return ServiceResult<PrivacySettingsDto>.Ok(data, "");
                return ServiceResult<PrivacySettingsDto>.Fail(err ?? "Failed to load settings");
            }
            catch (Exception ex)
            {
                return ServiceResult<PrivacySettingsDto>.Fail(ex.Message);
            }
        }

        public async Task<ServiceResult<PrivacySettingsDto>> UpdateSettingsAsync(UpdatePrivacySettingsDto settings)
        {
            try
            {
                var (ok, data, err) = await _client.PutAsync<UpdatePrivacySettingsDto, PrivacySettingsDto>("api/privacy/settings", settings);
                if (ok && data is not null)
                    return ServiceResult<PrivacySettingsDto>.Ok(data, "");
                return ServiceResult<PrivacySettingsDto>.Fail(err ?? "Failed to update settings");
            }
            catch (Exception ex)
            {
                return ServiceResult<PrivacySettingsDto>.Fail(ex.Message);
            }
        }
    }
}
