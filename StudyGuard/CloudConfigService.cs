using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace StudyGuard;

public class CloudConfigService
{
    private readonly HttpClient _httpClient;

    public CloudConfigService()
    {
        string? serverUrl =
            Environment.GetEnvironmentVariable(
                "STUDYGUARD_SERVER_URL"
            );

        string? deviceKey =
            Environment.GetEnvironmentVariable(
                "STUDYGUARD_DEVICE_KEY"
            );

        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            throw new InvalidOperationException(
                "STUDYGUARD_SERVER_URL tanımlı değil."
            );
        }

        if (string.IsNullOrWhiteSpace(deviceKey))
        {
            throw new InvalidOperationException(
                "STUDYGUARD_DEVICE_KEY tanımlı değil."
            );
        }

        serverUrl =
            serverUrl.TrimEnd('/') + "/";

        _httpClient =
            new HttpClient
            {
                BaseAddress =
                    new Uri(serverUrl),

                Timeout =
                    TimeSpan.FromSeconds(20)
            };

        _httpClient
            .DefaultRequestHeaders
            .Add(
                "X-Device-Key",
                deviceKey
            );
    }


    // ==================================================
    // CONFIG
    // ==================================================

    public async Task<CloudConfigDto?> GetConfigAsync()
    {
        try
        {
            HttpResponseMessage response =
                await _httpClient.GetAsync(
                    "api/config"
                );

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<CloudConfigDto>();
        }
        catch
        {
            return null;
        }
    }


    // ==================================================
    // VIOLATION
    // ==================================================

    public async Task<bool> SendViolationAsync(
        string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        try
        {
            var body =
                new
                {
                    domain =
                        domain.Trim()
                };

            HttpResponseMessage response =
                await _httpClient.PostAsJsonAsync(
                    "api/violations",
                    body
                );

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }


    // ==================================================
    // HEARTBEAT
    // ==================================================

    public async Task<bool> SendHeartbeatAsync()
    {
        try
        {
            var body =
                new
                {
                    deviceName =
                        Environment.MachineName
                };

            HttpResponseMessage response =
                await _httpClient.PostAsJsonAsync(
                    "api/device/heartbeat",
                    body
                );

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}


// ==================================================
// CLOUD DTO
// ==================================================

public class CloudConfigDto
{
    public string BlockMessage
    {
        get;
        set;
    } = "";

    public bool StudyModeEnabled
    {
        get;
        set;
    }

    public DateTimeOffset UpdatedAt
    {
        get;
        set;
    }
}