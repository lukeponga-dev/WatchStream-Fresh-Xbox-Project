using System.Net.Http.Json;
using System.Text.Json.Serialization;
using WatchStream.Application.Interfaces;
using WatchStream.Domain.Models;

namespace WatchStream.Infrastructure.Stremio;

public class StremioAccountClient : IStremioAccountService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.strem.io";
    private string? _sessionToken;

    public StremioAccountClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<StremioAccount?> GetAccountAsync()
    {
        if (string.IsNullOrEmpty(_sessionToken)) return null;

        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/user/profile");
            if (!response.IsSuccessStatusCode) return null;

            var profile = await response.Content.ReadFromJsonAsync<StremioProfileResponse>();
            if (profile == null) return null;

            return new StremioAccount(
                UserId: profile.Id,
                Username: profile.Username,
                Email: profile.Email,
                AddonIds: profile.Addons ?? new List<string>()
            );
        }
        catch
        {
            return null;
        }
    }

    public async Task<DeviceAuthRequest> InitiateDeviceAuthAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync($"{BaseUrl}/token/device", null);
            if (!response.IsSuccessStatusCode) throw new Exception("Failed to initiate device auth");

            var result = await response.Content.ReadFromJsonAsync<DeviceAuthRequest>();
            return result ?? throw new Exception("Empty response from auth server");
        }
        catch (Exception ex)
        {
            // Log error here
            throw;
        }
    }

    public async Task<bool> PollForTokenAsync(string deviceCode)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/token/verify", new { device_code = deviceCode });

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) return false; // Still waiting
            if (!response.IsSuccessStatusCode) return false;

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.Token)) return false;

            _sessionToken = tokenResponse.Token;
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _sessionToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        _sessionToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
        await Task.CompletedTask;
    }

    private record StremioProfileResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("addons")] List<string>? Addons
    );

    private record TokenResponse(
        [property: JsonPropertyName("token")] string Token
    );
}
