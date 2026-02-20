using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace VoiceMCP.Gateway.Services;

public class TeamsPlaywrightPoller : BackgroundService
{
    private readonly ILogger<TeamsPlaywrightPoller> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<string, InstanceRegistration> _instances;
    private readonly ConcurrentDictionary<string, string> _questionToInstance;
    private readonly HashSet<string> _processedQuestions = new();
    private readonly int _port;
    
    private IPlaywright? _playwright;
    private IBrowserContext? _browser;
    private IPage? _page;
    private DateTime _startTime;
    private int _consecutiveErrors;
    
    private const string TeamsWebUrl = "https://teams.cloud.microsoft/";
    private const string ChannelId = "19:6gjjSHAUPHJlqyxeJemN9giR8HYZkWGpvsznRDSyagE1@thread.tacv2";
    private const int ActivePollIntervalMs = 5000;
    private const int IdlePollIntervalMs = 30000;
    private readonly bool _headless;
    
    public TeamsPlaywrightPoller(
        ILogger<TeamsPlaywrightPoller> logger,
        IHttpClientFactory httpClientFactory,
        ConcurrentDictionary<string, InstanceRegistration> instances,
        ConcurrentDictionary<string, string> questionToInstance,
        int port,
        bool headless = true)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _instances = instances;
        _questionToInstance = questionToInstance;
        _port = port;
        _headless = headless;
    }
    
    public string PlaywrightStatus { get; private set; } = "initializing";
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _startTime = DateTime.UtcNow;
        _logger.LogInformation("[Playwright] Starting Teams poller");
        
        try
        {
            await InitializeBrowserAsync(stoppingToken);
            await PollLoopAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[Playwright] Polling stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Playwright] Fatal error in polling loop");
            PlaywrightStatus = $"failed: {ex.Message}";
        }
        finally
        {
            await CleanupAsync();
        }
    }
    private IBrowser? _browserInstance;
    
    private async Task InitializeBrowserAsync(CancellationToken stoppingToken)
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            // Use Edge's actual user data dir — already has Teams auth cookies
            var userDataDir = Environment.GetEnvironmentVariable("GATEWAY_BROWSER_PROFILE") 
                ?? Path.Combine(localAppData, "Microsoft", "Edge", "User Data");
            
            _playwright = await Playwright.CreateAsync();
            var channel = Environment.GetEnvironmentVariable("GATEWAY_BROWSER_CHANNEL") ?? "msedge";
            _logger.LogInformation($"[Playwright] Browser channel: {channel}, profile: {userDataDir}, headless: {_headless}");
            
            // Use persistent context with Edge's default profile (has Teams auth)
            _browser = await _playwright.Chromium.LaunchPersistentContextAsync(userDataDir, new()
            {
                Channel = channel,
                Headless = _headless,
                Args = new[] { "--disable-blink-features=AutomationControlled" }
            });
            
            _logger.LogInformation("[Playwright] Browser launched successfully");
            _browserInstance = null;
            _page = _browser.Pages.FirstOrDefault() ?? await _browser.NewPageAsync();
            
            _logger.LogInformation("[Playwright] Navigating to Teams...");
            await _page.GotoAsync(TeamsWebUrl, new() { Timeout = 60000, WaitUntil = WaitUntilState.DOMContentLoaded });
            await Task.Delay(5000, stoppingToken);
            
            if (_page.Url.Contains("login.microsoftonline.com"))
            {
                _logger.LogError("[Playwright] AUTH REQUIRED: Please log in to Teams manually first");
                PlaywrightStatus = "auth_required";
                return;
            }
            
            _logger.LogInformation($"[Playwright] Teams loaded at: {_page.Url}");
            
            // Click wizard channel in sidebar (more reliable than URL navigation)
            _logger.LogInformation("[Playwright] Looking for wizard channel in sidebar...");
            var wizardChannel = _page.Locator($"[data-testid*=\"favorite-channel-list-item-{ChannelId}\"]");
            if (await wizardChannel.CountAsync() == 0)
            {
                // Fallback: try any element containing the channel ID
                wizardChannel = _page.Locator($"[data-tid*=\"{ChannelId}\"]");
            }
            if (await wizardChannel.CountAsync() == 0)
            {
                // Second fallback: navigate by URL
                _logger.LogInformation("[Playwright] Sidebar click failed, navigating by URL");
                var channelUrl = $"{TeamsWebUrl}?#/conversations/{Uri.EscapeDataString(ChannelId)}?ctx=channel";
                await _page.GotoAsync(channelUrl, new() { Timeout = 60000, WaitUntil = WaitUntilState.DOMContentLoaded });
            }
            else
            {
                _logger.LogInformation("[Playwright] Found wizard channel, clicking...");
                await wizardChannel.First.EvaluateAsync("node => node.click()");
            }
            await Task.Delay(5000, stoppingToken);
            
            PlaywrightStatus = "connected";
            _logger.LogInformation("[Playwright] Initialization complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Playwright] Initialization failed");
            PlaywrightStatus = $"init_failed: {ex.Message}";
            throw;
        }
    }
    
    private async Task PollLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_page == null || PlaywrightStatus != "connected")
                {
                    await Task.Delay(10000, stoppingToken);
                    continue;
                }
                
                var hasPendingQuestions = _questionToInstance.Any();
                var pollInterval = hasPendingQuestions ? ActivePollIntervalMs : IdlePollIntervalMs;
                
                await PollForRepliesAsync(stoppingToken);
                
                _consecutiveErrors = 0;
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                var backoffMs = Math.Min(5000 * (int)Math.Pow(2, _consecutiveErrors - 1), 300000);
                _logger.LogError(ex, $"[Playwright] Poll error (consecutive: {_consecutiveErrors}), backing off {backoffMs}ms");
                
                if (_page?.Url.Contains("login.microsoftonline.com") == true)
                {
                    _logger.LogError("[Playwright] AUTH WALL DETECTED: Session expired, manual re-login required");
                    PlaywrightStatus = "auth_required";
                }
                
                await Task.Delay(backoffMs, stoppingToken);
            }
        }
    }
    
    private bool _domDiagDone = false;
    
    private async Task PollForRepliesAsync(CancellationToken stoppingToken)
    {
        if (_page == null) return;
        
        await _page.Keyboard.PressAsync("End");
        await Task.Delay(1500, stoppingToken);
        
        // One-time DOM diagnostic: find what contains our question text
        if (!_domDiagDone && _questionToInstance.Any())
        {
            _domDiagDone = true;
            try
            {
                var diag = await _page.EvaluateAsync<string>(@"() => {
                    const results = [];
                    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
                    let node;
                    while (node = walker.nextNode()) {
                        if (node.textContent && node.textContent.includes('q-')) {
                            let el = node.parentElement;
                            let path = [];
                            for (let i = 0; i < 5 && el; i++) {
                                path.push(el.tagName + (el.getAttribute('role') ? '[role=' + el.getAttribute('role') + ']' : '') + (el.getAttribute('data-tid') ? '[data-tid=' + el.getAttribute('data-tid') + ']' : ''));
                                el = el.parentElement;
                            }
                            results.push({ text: node.textContent.substring(0, 100), path: path.join(' < ') });
                        }
                    }
                    return JSON.stringify(results.slice(-10));
                }");
                _logger.LogInformation("[Playwright] DOM DIAGNOSTIC — elements containing 'q-': {Diag}", diag);
            }
            catch (Exception ex) { _logger.LogWarning("[Playwright] DOM diagnostic failed: {Err}", ex.Message); }
        }
        
        var messageGroups = await _page.Locator("[role=\"group\"]").AllAsync();
        var lastGroups = messageGroups.TakeLast(20).ToList();
        _logger.LogInformation("[Playwright] Poll cycle: found {Count} message groups, scanning last {Scan}", messageGroups.Count, lastGroups.Count);
        
        // Diagnostic: log last 3 groups' text snippets
        foreach (var g in lastGroups.TakeLast(3))
        {
            try
            {
                var snippet = await g.InnerTextAsync();
                if (!string.IsNullOrEmpty(snippet))
                {
                    var truncated = snippet.Length > 200 ? snippet[..200] + "..." : snippet;
                    _logger.LogInformation("[Playwright] Group snippet: {Snippet}", truncated.Replace("\n", " | "));
                }
            }
            catch { }
        }
        
        foreach (var group in lastGroups)
        {
            try
            {
                // Use InnerTextAsync for visible text (TextContentAsync misses adaptive card content)
                var groupText = await group.InnerTextAsync();
                if (string.IsNullOrEmpty(groupText)) continue;
                
                // Try sentinel pattern first: ⚡ q-xxx-nnn
                var sentinelMatch = Regex.Match(groupText, @"⚡ (q-[a-zA-Z0-9]+-\d{3})");
                // Fallback: card title pattern [q-xxx-nnn]
                if (!sentinelMatch.Success)
                    sentinelMatch = Regex.Match(groupText, @"\[(q-[a-zA-Z0-9]+-\d{3})\]");
                // Second fallback: 🤖 Question [q-xxx-nnn] title format (capture inner id)
                if (!sentinelMatch.Success)
                    sentinelMatch = Regex.Match(groupText, @"🤖\s*Question\s*\[(q-[a-zA-Z0-9]+-\d{3})\]");
                
                if (!sentinelMatch.Success) continue;
                
                var questionId = sentinelMatch.Groups[1].Value;
                _logger.LogInformation("[Playwright] Detected question {QuestionId} in message group", questionId);
                
                if (_processedQuestions.Contains(questionId))
                    continue;
                
                if (!_questionToInstance.TryGetValue(questionId, out var instanceId))
                    continue;
                
                var replyContainers = await group.Locator("[aria-label*=\"reply\"]").AllAsync();
                
                bool hasReplies = false;
                foreach (var c in replyContainers)
                {
                    var label = await c.GetAttributeAsync("aria-label");
                    if (label != null && Regex.IsMatch(label, @"\d+ repl"))
                    {
                        hasReplies = true;
                        break;
                    }
                }
                
                if (!hasReplies) continue;
                
                foreach (var replyContainer in replyContainers)
                {
                    var replyText = "";
                    var isAudioTranscript = false;
                    
                    var transcriptButton = replyContainer.Locator("[data-testid=\"transcript-button\"]");
                    if (await transcriptButton.CountAsync() > 0)
                    {
                        await transcriptButton.ClickAsync();
                        await Task.Delay(500, stoppingToken);
                        
                        var transcriptParagraph = replyContainer.Locator("[data-testid=\"transcript-paragraph\"]");
                        if (await transcriptParagraph.CountAsync() > 0)
                        {
                            replyText = (await transcriptParagraph.TextContentAsync()) ?? "";
                            isAudioTranscript = true;
                        }
                    }
                    else
                    {
                        var paragraphs = await replyContainer.Locator("p").AllAsync();
                        var texts = new List<string>();
                        foreach (var p in paragraphs)
                        {
                            var text = await p.TextContentAsync();
                            if (!string.IsNullOrEmpty(text))
                                texts.Add(text);
                        }
                        replyText = string.Join(" ", texts).Trim();
                    }
                    
                    if (string.IsNullOrEmpty(replyText)) continue;
                    
                    await DeliverReplyAsync(questionId, instanceId, replyText, isAudioTranscript, stoppingToken);
                    _processedQuestions.Add(questionId);
                    _questionToInstance.TryRemove(questionId, out _);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Playwright] Error processing message group");
            }
        }
    }
    
    private async Task DeliverReplyAsync(string questionId, string instanceId, string answer, bool isAudioTranscript, CancellationToken stoppingToken)
    {
        if (!_instances.TryGetValue(instanceId, out var instance))
        {
            _logger.LogWarning($"[Playwright] Instance not found for question {questionId}: {instanceId}");
            return;
        }
        
        try
        {
            var client = _httpClientFactory.CreateClient();
            var payload = new
            {
                questionId,
                answer,
                userId = "teams-user",
                userName = "Teams User",
                timestamp = DateTime.UtcNow,
                isAudioTranscript,
                secret = instance.Secret
            };
            
            var response = await client.PostAsJsonAsync(instance.CallbackUrl, payload, stoppingToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"[Playwright] Reply delivered: {questionId} -> {instance.CallbackUrl}");
            }
            else
            {
                _logger.LogWarning($"[Playwright] Reply delivery failed: {questionId} -> {instance.CallbackUrl} (HTTP {response.StatusCode})");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[Playwright] Exception delivering reply: {questionId} -> {instance.CallbackUrl}");
        }
    }
    
    private async Task CleanupAsync()
    {
        try
        {
            if (_page != null) await _page.CloseAsync();
            if (_browserInstance != null) await _browserInstance.CloseAsync();
            if (_browser != null) await _browser.CloseAsync();
            _playwright?.Dispose();
            _logger.LogInformation("[Playwright] Cleanup complete");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Playwright] Error during cleanup");
        }
    }
    
    public int GetUptimeSeconds() => (int)(DateTime.UtcNow - _startTime).TotalSeconds;
}
