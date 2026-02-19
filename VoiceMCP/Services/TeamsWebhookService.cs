using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace VoiceMCP.Services
{
    /// <summary>
    /// Sends adaptive cards to a Microsoft Teams channel via Incoming Webhook.
    /// Supports two-way communication via Power Automate flow (if configured).
    /// Falls back to Gateway-based replies or one-way notifications.
    /// </summary>
    public class TeamsWebhookService : ITeamsNotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _webhookUrl;
        private readonly string _projectName;
        private readonly string? _flowUrl;
        private readonly IGatewayClient? _gatewayClient;
        private readonly string _instanceId;
        private int _questionSequence;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingQuestions;

        public string InstanceId => _instanceId;

        public TeamsWebhookService(HttpClient httpClient, string webhookUrl, string projectName, string? flowUrl = null, IGatewayClient? gatewayClient = null)
        {
            _httpClient = httpClient;
            _webhookUrl = webhookUrl;
            _projectName = projectName;
            _flowUrl = flowUrl;
            _gatewayClient = gatewayClient;
            _instanceId = Guid.NewGuid().ToString("N")[..8]; // 8-char hex
            _questionSequence = 0;
            _pendingQuestions = new ConcurrentDictionary<string, TaskCompletionSource<string>>();
        }

        public async Task SendNotificationAsync(string message, string? title = null)
        {
            // Use Power Automate flow for notifications (posts interactive card)
            if (!string.IsNullOrEmpty(_flowUrl))
            {
                await PostViaFlowAsync(title ?? "Notification", message, isQuestion: false);
                return;
            }

            var card = BuildAdaptiveCard(title ?? "Notification", message, null);
            await PostCardAsync(card);
        }

        public async Task<string?> AskQuestionAsync(string question, int timeoutSeconds = 120)
        {
            var questionId = $"q-{_instanceId}-{Interlocked.Increment(ref _questionSequence):D3}";

            // Power Automate flow: posts adaptive card with input field, blocks until user submits
            if (!string.IsNullOrEmpty(_flowUrl))
            {
                return await AskViaFlowAsync(questionId, question, timeoutSeconds);
            }

            // Legacy: Gateway-based reply system
            TaskCompletionSource<string>? tcs = null;
            if (_gatewayClient != null)
            {
                tcs = new TaskCompletionSource<string>();
                _pendingQuestions[questionId] = tcs;

                try
                {
                    await _gatewayClient.RegisterQuestionAsync(questionId, _instanceId);
                }
                catch
                {
                    _pendingQuestions.TryRemove(questionId, out _);
                    tcs = null;
                }
            }

            var replyInstructions = tcs != null
                ? $"Reply with @VoiceMCP [{questionId}] your-answer"
                : null;

            var card = BuildAdaptiveCard("Question", question, replyInstructions);
            var success = await PostCardAsync(card);

            if (!success)
            {
                if (tcs != null) _pendingQuestions.TryRemove(questionId, out _);
                return "Failed to post question to Teams. Check stderr for details.";
            }

            if (tcs == null)
            {
                return $"Question posted to Teams channel ({_projectName}): \"{question}\". Teams webhooks are one-way — the user should respond through another channel.";
            }

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            _pendingQuestions.TryRemove(questionId, out _);

            return completedTask == tcs.Task
                ? await tcs.Task
                : $"⏱️ Timeout: No reply received within {timeoutSeconds} seconds. Question was: \"{question}\"";
        }

        /// <summary>
        /// Posts an adaptive card with an input field via Power Automate flow and waits for the user's response.
        /// The flow posts the card in Teams, blocks until the user submits, and returns the answer.
        /// </summary>
        private async Task<string?> AskViaFlowAsync(string questionId, string question, int timeoutSeconds)
        {
            try
            {
                var payload = new
                {
                    questionId,
                    question,
                    projectName = _projectName,
                    callbackUrl = "" // Not needed for flow-based approach
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                var response = await _httpClient.PostAsync(_flowUrl, content, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    Console.Error.WriteLine($"Flow error: {response.StatusCode} — {body}");
                    return $"Failed to post question via flow: {response.StatusCode}";
                }

                // Parse the response — Power Automate returns the adaptive card submission data
                var responseBody = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    return "User submitted response (empty answer).";
                }

                // Try to extract the "answer" field from the response
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("data", out var data) &&
                        data.TryGetProperty("answer", out var answer))
                    {
                        return answer.GetString() ?? "User submitted response (empty answer).";
                    }
                    if (doc.RootElement.TryGetProperty("answer", out var directAnswer))
                    {
                        return directAnswer.GetString() ?? "User submitted response (empty answer).";
                    }
                }
                catch (JsonException)
                {
                    // Not JSON, return raw
                }

                return responseBody;
            }
            catch (OperationCanceledException)
            {
                return $"⏱️ Timeout: No reply received within {timeoutSeconds} seconds. Question was: \"{question}\"";
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Flow exception: {ex.Message}");
                return $"Failed to ask via flow: {ex.Message}";
            }
        }

        /// <summary>
        /// Posts a notification via Power Automate flow (one-way, doesn't wait for response).
        /// </summary>
        private async Task PostViaFlowAsync(string title, string message, bool isQuestion)
        {
            try
            {
                var payload = new
                {
                    questionId = $"n-{_instanceId}-{Interlocked.Increment(ref _questionSequence):D3}",
                    question = $"{title}\n\n{message}",
                    projectName = _projectName,
                    callbackUrl = ""
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Fire-and-forget for notifications — don't wait for card submission
                _ = _httpClient.PostAsync(_flowUrl, content);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Flow notification exception: {ex.Message}");
                // Fall back to webhook
                var card = BuildAdaptiveCard(title, message, null);
                await PostCardAsync(card);
            }
        }

        public void OnReplyReceived(string questionId, string answer)
        {
            if (_pendingQuestions.TryRemove(questionId, out var tcs))
            {
                tcs.TrySetResult(answer);
            }
        }

        private object BuildAdaptiveCard(string title, string message, string? replyInstructions)
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
