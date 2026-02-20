using System.Text;
using System.Text.Json;

namespace VoiceMCP.Services
{
    /// <summary>
    /// HTTP client for VoiceMCP Gateway communication.
    /// Gracefully degrades if Gateway is unavailable (logs warning, continues).
    /// </summary>
    public class GatewayClient : IGatewayClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _gatewayBaseUrl;

        public GatewayClient(HttpClient httpClient, string gatewayBaseUrl)
        {
            _httpClient = httpClient;
            _gatewayBaseUrl = gatewayBaseUrl.TrimEnd('/');
        }

        public async Task RegisterInstanceAsync(string instanceId, string callbackUrl, string projectName, string secret)
        {
            var payload = new
            {
                instanceId,
                callbackUrl,
                projectName,
                secret
            };

            await PostAsync("/gateway/register", payload, "register instance");
        }

        public async Task RegisterQuestionAsync(string questionId, string instanceId)
        {
            var payload = new
            {
                questionId,
                instanceId
            };

            await PostAsync("/gateway/questions", payload, "register question");
        }

        public async Task HeartbeatAsync(string instanceId, string secret)
        {
            var payload = new
            {
                instanceId,
                secret
            };

            await PostAsync("/gateway/heartbeat", payload, "heartbeat");
        }

        public async Task UnregisterInstanceAsync(string instanceId)
        {
            try
            {
                var url = $"{_gatewayBaseUrl}/gateway/instances/{instanceId}";
                var response = await _httpClient.DeleteAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Gateway unregister failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Gateway unregister warning: {ex.Message}");
            }
        }

        private async Task PostAsync(string path, object payload, string operationName)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = $"{_gatewayBaseUrl}{path}";

                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    Console.Error.WriteLine($"Gateway {operationName} failed: {response.StatusCode} — {body}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Gateway {operationName} warning: {ex.Message}. AskUserViaTeams will degrade to one-way mode.");
            }
        }
    }
}
