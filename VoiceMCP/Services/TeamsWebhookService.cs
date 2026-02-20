using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace VoiceMCP.Services
{
    /// <summary>
    /// Sends adaptive cards to a Microsoft Teams channel via Incoming Webhook.
    /// Supports two-way communication via Playwright browser polling for thread replies.
    /// </summary>
    public class TeamsWebhookService : ITeamsNotificationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _webhookUrl;
        private readonly string _projectName;
        private readonly IGatewayClient? _gatewayClient;
        private readonly string _instanceId;
        private int _questionSequence;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingQuestions;

        public string InstanceId => _instanceId;

        public TeamsWebhookService(IHttpClientFactory httpClientFactory, string webhookUrl, string projectName, IGatewayClient? gatewayClient = null)
        {
            _httpClientFactory = httpClientFactory;
            _webhookUrl = webhookUrl;
            _projectName = projectName;
            _gatewayClient = gatewayClient;
            _instanceId = Guid.NewGuid().ToString("N")[..8];
            _questionSequence = 0;
            _pendingQuestions = new ConcurrentDictionary<string, TaskCompletionSource<string>>();
        }

        public async Task SendNotificationAsync(string message, string? title = null)
        {
            var card = BuildAdaptiveCard(title ?? "Notification", message, null, null);
            await PostCardAsync(card);
        }

        /// <summary>
        /// Posts a question card to Teams and waits for reply.
        /// If Gateway is available, blocks until user replies or timeout.
        /// If Gateway unavailable, degrades to one-way notification.
        /// </summary>
        public async Task<string?> AskQuestionAsync(string question, int timeoutSeconds = 120)
        {
            var questionId = $"q-{_instanceId}-{Interlocked.Increment(ref _questionSequence):D3}";

            var card = BuildAdaptiveCard($"🤖 Question [{questionId}]", question, "💬 Reply in the thread below", questionId);
            var success = await PostCardAsync(card);

            if (!success)
            {
                return "Failed to post question to Teams. Check stderr for details.";
            }

            // If gateway client is available, block and wait for reply
            if (_gatewayClient != null)
            {
                var tcs = new TaskCompletionSource<string>();
                _pendingQuestions[questionId] = tcs;

                try
                {
                    // Register question with gateway
                    await _gatewayClient.RegisterQuestionAsync(questionId, _instanceId);

                    // Wait for reply or timeout
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
                    var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                    if (completedTask == tcs.Task)
                    {
                        return await tcs.Task;
                    }
                    else
                    {
                        _pendingQuestions.TryRemove(questionId, out _);
                        return $"Timeout: No reply received within {timeoutSeconds} seconds for question {questionId}.";
                    }
                }
                catch (Exception ex)
                {
                    _pendingQuestions.TryRemove(questionId, out _);
                    Console.Error.WriteLine($"Error waiting for question reply: {ex.Message}");
                    return $"QUESTION_POSTED:{questionId} (gateway error, falling back to one-way mode)";
                }
            }

            // Fallback: Gateway not available
            return $"QUESTION_POSTED:{questionId}";
        }

        public void OnReplyReceived(string questionId, string answer)
        {
            if (_pendingQuestions.TryRemove(questionId, out var tcs))
            {
                tcs.TrySetResult(answer);
            }
        }

        private object BuildAdaptiveCard(string title, string message, string? replyInstructions, string? questionId)
        {
            var body = new List<object>
            {
                new { type = "TextBlock", text = $"Project: {_projectName}", weight = "Bolder", size = "Small", color = "Accent" },
                new { type = "TextBlock", text = title, weight = "Bolder", size = "Medium" },
                new { type = "TextBlock", text = message, wrap = true }
            };

            if (!string.IsNullOrEmpty(replyInstructions))
            {
                body.Add(new { type = "TextBlock", text = replyInstructions, size = "Small", color = "Accent", wrap = true });
            }

            // Add sentinel text for Gateway Playwright polling
            if (!string.IsNullOrEmpty(questionId))
            {
                body.Add(new { type = "TextBlock", text = $"⚡ {questionId}", size = "Small", isSubtle = true });
            }

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
                            body
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
                
                using var httpClient = _httpClientFactory.CreateClient("teams");
                var response = await httpClient.PostAsync(_webhookUrl, content);

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
