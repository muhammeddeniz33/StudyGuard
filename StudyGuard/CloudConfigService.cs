using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace StudyGuard
{
    public class CloudConfigDto
    {
        public string BlockMessage { get; set; } = "";

        public bool StudyModeEnabled { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class CloudConfigService
    {
        private readonly HttpClient httpClient;

        public CloudConfigService()
        {
            httpClient = new HttpClient
            {
                BaseAddress =
                    new Uri("http://localhost:5182")
            };

            httpClient.DefaultRequestHeaders.Add(
                "X-Device-Key",
                "SG-Device-4521-Y"
            );
        }

        public async Task<CloudConfigDto?> GetConfigAsync()
        {
            try
            {
                return await httpClient
                    .GetFromJsonAsync<CloudConfigDto>(
                        "/api/config"
                    );
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> SendViolationAsync(
            string domain)
        {
            try
            {
                var response =
                    await httpClient.PostAsJsonAsync(
                        "/api/violations",
                        new
                        {
                            domain = domain
                        }
                    );

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> SendHeartbeatAsync()
        {
            try
            {
                var response =
                    await httpClient.PostAsJsonAsync(
                        "/api/device/heartbeat",
                        new
                        {
                            deviceName =
                                Environment.MachineName
                        }
                    );

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}