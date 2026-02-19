using System.Text;
using System.Text.Json;

namespace VoiceMCP.Services
{
    /// <summary>
    /// Sends adaptive cards to a Microsoft Teams channel via Incoming Webhook.
    /// </summary>
    public class TeamsWebhookService : ITeamsNotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _webhookUrl;
        private readonly string _projectName;

        public TeamsWebhookService(HttpClient httpClient, string webhookUrl, string projectName)
        {
            _httpClient = httpClient;
            _webhookUrl = webhookUrl;
            _projectName = projectName;
        }

        public async Task SendNotificationAsync(string message, string? title = null)
        {
            var card = BuildAdaptiveCard(title ?? "Notification", message);
            await PostCardAsync(card);
        }

        public async Task<string> AskQuestionAsync(string question)
        {
            var card = BuildAdaptiveCard("Question", question);
            var success = await PostCardAsync(card);

            return success
                ? $"Question posted to Teams channel ({_projectName}): \"{question}\". Teams webhooks are one-way — the user should respond through another channel."
                : $"Failed to post question to Teams. Check stderr for details.";
        }

        private object BuildAdaptiveCard(string title, string message)
        {
            return new
            {
                type = "message",
                attachments = new[]
                {
                    new
                    {
                        contentType = "application/vnd.microsoft.card.adaptive",
                        content = new
                        {
                            schema = "http://adaptivecards.io/schemas/adaptive-card.json",
                            type = "AdaptiveCard",
                            version = "1.4",
                            body = new object[]
                            {
                                new { type = "TextBlock", text = $"Project: {_projectName}", weight = "Bolder", size = "Small", color = "Accent" },
                                new { type = "TextBlock", text = title, weight = "Bolder", size = "Medium" },
                                new { type = "TextBlock", text = message, wrap = true }
                            }
                        }
                    }
                }
            };
        }

        private async Task<bool> PostCardAsync(object card)
        {
            try
            {
                var json = JsonSerializer.Serialize(card, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // The adaptive card schema key must be "$schema" in the JSON payload
                json = json.Replace("\"schema\":", "\"$schema\":");

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_webhookUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    Console.Error.WriteLine($"Teams webhook error: {response.StatusCode} — {body}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Teams webhook exception: {ex.Message}");
                return false;
            }
        }
    }
}
