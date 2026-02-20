# Architecture Decision: Two-Way Teams Communication via Outgoing Webhook

**Status:** Proposed  
**Author:** Keaton  
**Date:** 2025-06-XX  
**Context:** Enable AskUserViaTeams to receive replies from Teams without bot framework or Entra app registration

---

## Problem Statement

Current VoiceMCP implementation uses Teams Incoming Webhooks for one-way communication. We need two-way communication to allow `AskUserViaTeams` to block and wait for user replies. Constraints:

1. Multiple VoiceMCP instances may run simultaneously (same/different machines, same/different users)
2. All instances post to the same Teams channel via shared Incoming Webhook
3. Teams Outgoing Webhook sends ALL @mentions to ONE callback URL
4. 5-second response timeout on Outgoing Webhooks
5. No bot framework, no Entra app registration

---

## Architecture Decision

### **Routing Strategy: Option A — Shared Gateway/Router Process**

**Selected approach:** Deploy a single, persistent **VoiceMCP Gateway** process that:
- Owns the Outgoing Webhook callback URL (fixed, stable endpoint)
- Receives all @mention callbacks from Teams
- Routes replies to correct VoiceMCP instance based on correlation ID
- Uses HTTP callbacks to instance-specific listener ports

**Why this approach:**
- **Stable endpoint:** Outgoing Webhook callback URL doesn't change when instances start/stop
- **Reliable routing:** Central registry of active instances and their pending questions
- **Simple instance code:** Each VoiceMCP instance just needs a local HTTP listener, no external routing logic
- **Works across machines:** Gateway can route to instances on different machines (via network)
- **5-second timeout:** Gateway responds immediately to Teams ("Acknowledged"), then forwards asynchronously

**Rejected alternatives:**
- **Option B (embed callback in card):** Outgoing Webhook doesn't see card data, only @mention text — can't extract dynamic URLs
- **Option C (polling shared state):** Higher latency, complex state management, requires shared database

---

## Component Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                          Teams Channel                              │
│  ┌──────────────┐                        ┌──────────────┐          │
│  │   Incoming   │ ◄──── POST cards ───── │  Outgoing    │          │
│  │   Webhook    │                        │  Webhook     │          │
│  └──────────────┘                        └───────┬──────┘          │
│                                                   │                  │
└───────────────────────────────────────────────────┼──────────────────┘
                                                    │
                                                    │ POST @mention
                                                    │ (5sec timeout)
                                                    ▼
                               ┌────────────────────────────────────┐
                               │      VoiceMCP Gateway              │
                               │  (Persistent background service)   │
                               │                                    │
                               │  • Receives all @mentions          │
                               │  • Parses correlation ID           │
                               │  • Routes to instance              │
                               │  • Returns 200 to Teams (< 5s)     │
                               │                                    │
                               │  Registry:                         │
                               │  {                                 │
                               │    "q-inst1-001": {                │
                               │      instanceId: "inst1",          │
                               │      callbackUrl: "http://...8091" │
                               │    }                               │
                               │  }                                 │
                               └───────┬────────────────────────────┘
                                       │
                         ┌─────────────┴─────────────┐
                         │                           │
                         ▼                           ▼
              ┌──────────────────┐       ┌──────────────────┐
              │  VoiceMCP Inst1  │       │  VoiceMCP Inst2  │
              │  (Port 8091)     │       │  (Port 8092)     │
              │                  │       │                  │
              │  • Generates Q   │       │  • Generates Q   │
              │  • Registers     │       │  • Registers     │
              │  • Waits         │       │  • Waits         │
              │  • Receives CB   │       │  • Receives CB   │
              └──────────────────┘       └──────────────────┘
```

---

## Correlation System

### Instance Identity
```csharp
// Generated at VoiceMCP startup, persists for lifetime of instance
string instanceId = Guid.NewGuid().ToString("N").Substring(0, 8); // e.g., "a3f7b2c1"
```

### Question ID Format
```csharp
// Format: q-{instanceId}-{sequenceNumber}
// Example: "q-a3f7b2c1-001"
string questionId = $"q-{instanceId}-{sequenceCounter++:D3}";
```

Embedded in adaptive card:
```json
{
  "type": "AdaptiveCard",
  "body": [
    {
      "type": "TextBlock",
      "text": "Project: MyProject",
      "weight": "Bolder",
      "size": "Small",
      "color": "Accent"
    },
    {
      "type": "TextBlock",
      "text": "Question",
      "weight": "Bolder",
      "size": "Medium"
    },
    {
      "type": "TextBlock",
      "text": "Should I proceed with deployment?",
      "wrap": true
    },
    {
      "type": "TextBlock",
      "text": "Reply with @VoiceMCP [q-a3f7b2c1-001] your-answer",
      "size": "Small",
      "color": "Accent",
      "wrap": true
    }
  ]
}
```

### Reply Extraction
Gateway parses Teams @mention text using regex:
```csharp
// Expected format: "@VoiceMCP [q-a3f7b2c1-001] yes please"
// Regex: @VoiceMCP\s+\[([^\]]+)\]\s+(.+)
Match match = Regex.Match(text, @"\[([^\]]+)\]\s+(.+)", RegexOptions.IgnoreCase);
if (match.Success)
{
    string questionId = match.Groups[1].Value; // "q-a3f7b2c1-001"
    string answer = match.Groups[2].Value.Trim(); // "yes please"
}
```

---

## HTTP Listener (Instance)

Each VoiceMCP instance runs a lightweight HTTP listener for receiving callbacks from Gateway.

### Port Allocation
```csharp
// Start at base port, increment if in use
int basePort = 8090;
int maxAttempts = 100;
int assignedPort = 0;

for (int i = 0; i < maxAttempts; i++)
{
    int candidatePort = basePort + i;
    if (IsPortAvailable(candidatePort))
    {
        assignedPort = candidatePort;
        break;
    }
}

if (assignedPort == 0)
    throw new Exception("No available ports in range 8090-8190");
```

### Endpoint
```
POST http://localhost:{assignedPort}/voice-mcp/reply
Content-Type: application/json

{
  "questionId": "q-a3f7b2c1-001",
  "answer": "yes please",
  "userId": "john.doe@company.com",
  "userName": "John Doe"
}
```

---

## Gateway Registration Protocol

### Instance Startup
1. VoiceMCP instance starts, allocates port (e.g., 8091)
2. Generates instanceId (e.g., "a3f7b2c1")
3. Registers with Gateway:
```
POST http://localhost:8080/gateway/register
Content-Type: application/json

{
  "instanceId": "a3f7b2c1",
  "callbackUrl": "http://localhost:8091/voice-mcp/reply",
  "projectName": "MyProject"
}
```

### Question Registration
Before posting question to Teams:
```
POST http://localhost:8080/gateway/questions
Content-Type: application/json

{
  "questionId": "q-a3f7b2c1-001",
  "instanceId": "a3f7b2c1"
}
```

### Instance Shutdown
On graceful exit:
```
DELETE http://localhost:8080/gateway/instances/a3f7b2c1
```

Gateway removes instance and all its pending questions.

---

## Blocking AskUserViaTeams Flow

```
┌─────────────────┐
│ MCP Client      │
│ (Copilot CLI)   │
└────────┬────────┘
         │ call AskUserViaTeams("Should I proceed?")
         ▼
┌─────────────────────────────────────────────┐
│ VoiceMCP Instance                           │
│                                             │
│ 1. Generate questionId: q-a3f7b2c1-001     │
│ 2. Register with Gateway                    │
│ 3. Build adaptive card with reply template  │
│ 4. POST card to Teams Incoming Webhook      │
│ 5. Wait for callback (with 120s timeout)    │◄──────┐
│                                             │       │
│ TaskCompletionSource<string> tcs            │       │
│ _pendingQuestions[questionId] = tcs         │       │
└─────────────────────────────────────────────┘       │
                                                       │
         ┌─────────────────────────────────────────────┘
         │
┌─────────────────────────────────────────────┐
│ Teams Channel                               │
│                                             │
│ User sees: "Should I proceed?"              │
│ User replies: "@VoiceMCP [q-a3f7b2c1-001]   │
│               yes please"                   │
└─────────────────┬───────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────┐
│ VoiceMCP Gateway                            │
│                                             │
│ 1. Receive POST from Teams Outgoing Webhook │
│ 2. Parse: questionId="q-a3f7b2c1-001"       │
│           answer="yes please"               │
│ 3. Lookup: instanceId="a3f7b2c1"            │
│ 4. Return 200 to Teams (< 5s)               │
│ 5. POST to instance callback URL            │
└─────────────────┬───────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────┐
│ VoiceMCP Instance HTTP Listener             │
│ POST /voice-mcp/reply                       │
│                                             │
│ 1. Lookup TCS: _pendingQuestions[qId]      │
│ 2. tcs.SetResult("yes please")              │
│ 3. Remove from pending map                  │
└─────────────────┬───────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────┐
│ AskUserViaTeams returns "yes please"        │
│ to MCP Client                               │
└─────────────────────────────────────────────┘
```

---

## Interface Changes

### ITeamsNotificationService
```csharp
namespace VoiceMCP.Services
{
    public interface ITeamsNotificationService
    {
        /// <summary>
        /// Sends a notification adaptive card to Teams (fire-and-forget).
        /// </summary>
        Task SendNotificationAsync(string message, string? title = null);

        /// <summary>
        /// Posts a question to Teams and waits for user reply via Outgoing Webhook.
        /// BLOCKS until reply received or timeout (default 120s).
        /// </summary>
        /// <param name="question">The question to ask.</param>
        /// <param name="timeoutSeconds">Max wait time (default 120s).</param>
        /// <returns>User's answer text, or null if timeout.</returns>
        /// <exception cref="TimeoutException">If no reply within timeout.</exception>
        Task<string?> AskQuestionAsync(string question, int timeoutSeconds = 120);

        /// <summary>
        /// Called by HTTP listener when Gateway forwards a reply.
        /// </summary>
        void OnReplyReceived(string questionId, string answer);
    }
}
```

### TeamsWebhookService (updated)
```csharp
public class TeamsWebhookService : ITeamsNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;
    private readonly string _projectName;
    private readonly string _instanceId;
    private readonly IGatewayClient _gatewayClient;
    
    private int _questionSequence = 0;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingQuestions = new();

    public TeamsWebhookService(
        HttpClient httpClient,
        string webhookUrl,
        string projectName,
        string instanceId,
        IGatewayClient gatewayClient)
    {
        _httpClient = httpClient;
        _webhookUrl = webhookUrl;
        _projectName = projectName;
        _instanceId = instanceId;
        _gatewayClient = gatewayClient;
    }

    public async Task<string?> AskQuestionAsync(string question, int timeoutSeconds = 120)
    {
        var questionId = $"q-{_instanceId}-{Interlocked.Increment(ref _questionSequence):D3}";
        var tcs = new TaskCompletionSource<string>();
        _pendingQuestions[questionId] = tcs;

        try
        {
            // Register question with Gateway
            await _gatewayClient.RegisterQuestionAsync(questionId, _instanceId);

            // Build and post card with reply template
            var card = BuildQuestionCard(question, questionId);
            var success = await PostCardAsync(card);

            if (!success)
            {
                _pendingQuestions.TryRemove(questionId, out _);
                return null;
            }

            // Wait for reply (or timeout)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            cts.Token.Register(() => tcs.TrySetCanceled());

            return await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine($"Question {questionId} timed out after {timeoutSeconds}s");
            _pendingQuestions.TryRemove(questionId, out _);
            throw new TimeoutException($"No reply received within {timeoutSeconds} seconds.");
        }
    }

    public void OnReplyReceived(string questionId, string answer)
    {
        if (_pendingQuestions.TryRemove(questionId, out var tcs))
        {
            tcs.SetResult(answer);
        }
        else
        {
            Console.Error.WriteLine($"Received reply for unknown question: {questionId}");
        }
    }

    private object BuildQuestionCard(string question, string questionId)
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
                            new { type = "TextBlock", text = "Question", weight = "Bolder", size = "Medium" },
                            new { type = "TextBlock", text = question, wrap = true },
                            new { type = "TextBlock", text = $"Reply: @VoiceMCP [{questionId}] your-answer", size = "Small", color = "Accent", wrap = true }
                        }
                    }
                }
            }
        };
    }
    
    // ... existing SendNotificationAsync, PostCardAsync ...
}
```

### IGatewayClient (new)
```csharp
namespace VoiceMCP.Services
{
    /// <summary>
    /// Client for communicating with VoiceMCP Gateway.
    /// </summary>
    public interface IGatewayClient
    {
        /// <summary>
        /// Registers this VoiceMCP instance with the Gateway.
        /// </summary>
        Task RegisterInstanceAsync(string instanceId, string callbackUrl, string projectName);

        /// <summary>
        /// Registers a pending question with the Gateway.
        /// </summary>
        Task RegisterQuestionAsync(string questionId, string instanceId);

        /// <summary>
        /// Unregisters this instance (called on shutdown).
        /// </summary>
        Task UnregisterInstanceAsync(string instanceId);
    }

    public class GatewayClient : IGatewayClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _gatewayUrl; // e.g., "http://localhost:8080"

        public GatewayClient(HttpClient httpClient, string gatewayUrl)
        {
            _httpClient = httpClient;
            _gatewayUrl = gatewayUrl;
        }

        public async Task RegisterInstanceAsync(string instanceId, string callbackUrl, string projectName)
        {
            var payload = new { instanceId, callbackUrl, projectName };
            var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}/gateway/register", payload);
            response.EnsureSuccessStatusCode();
        }

        public async Task RegisterQuestionAsync(string questionId, string instanceId)
        {
            var payload = new { questionId, instanceId };
            var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}/gateway/questions", payload);
            response.EnsureSuccessStatusCode();
        }

        public async Task UnregisterInstanceAsync(string instanceId)
        {
            await _httpClient.DeleteAsync($"{_gatewayUrl}/gateway/instances/{instanceId}");
        }
    }
}
```

### TeamsReplyListener (new)
```csharp
namespace VoiceMCP.Services
{
    /// <summary>
    /// HTTP listener for receiving reply callbacks from Gateway.
    /// </summary>
    public class TeamsReplyListener : IHostedService
    {
        private readonly ITeamsNotificationService _teamsService;
        private readonly int _port;
        private WebApplication? _app;

        public TeamsReplyListener(ITeamsNotificationService teamsService, int port)
        {
            _teamsService = teamsService;
            _port = port;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"http://localhost:{_port}");
            
            _app = builder.Build();
            _app.MapPost("/voice-mcp/reply", HandleReply);
            
            await _app.StartAsync(cancellationToken);
            Console.Error.WriteLine($"Reply listener started on port {_port}");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_app != null)
                await _app.StopAsync(cancellationToken);
        }

        private IResult HandleReply(ReplyPayload payload)
        {
            _teamsService.OnReplyReceived(payload.QuestionId, payload.Answer);
            return Results.Ok();
        }

        private record ReplyPayload(string QuestionId, string Answer, string? UserId, string? UserName);
    }
}
```

### TeamsTools (updated)
```csharp
public class TeamsTools
{
    private readonly ITeamsNotificationService _teamsService;

    [McpServerTool, Description("Post a question to Teams and WAIT for user reply via @mention. Blocks until reply received or timeout (120s).")]
    public async Task<string> AskUserViaTeams(
        [Description("The question to post to Teams")] string question)
    {
        try
        {
            var answer = await _teamsService.AskQuestionAsync(question, timeoutSeconds: 120);
            return answer ?? "ERROR: No reply received within 120 seconds. User may need to respond via another channel.";
        }
        catch (TimeoutException ex)
        {
            return $"ERROR: {ex.Message}";
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"AskUserViaTeams error: {ex.Message}");
            return $"Failed to post question to Teams: {ex.Message}";
        }
    }

    // ... NotifyViaTeams unchanged ...
}
```

---

## Program.cs Changes

```csharp
// Conditionally register Teams services and tools
var teamsWebhookUrl = Environment.GetEnvironmentVariable("TEAMS_WEBHOOK_URL");
var gatewayUrl = Environment.GetEnvironmentVariable("VOICEMCP_GATEWAY_URL") ?? "http://localhost:8080";

if (!string.IsNullOrEmpty(teamsWebhookUrl))
{
    var projectName = Path.GetFileName(Path.GetFullPath("."));
    var instanceId = Guid.NewGuid().ToString("N").Substring(0, 8);
    
    // Find available port
    int assignedPort = FindAvailablePort(8090, 100);
    
    // Register services
    builder.Services.AddSingleton<IGatewayClient>(sp => 
        new GatewayClient(new HttpClient(), gatewayUrl));
    
    builder.Services.AddSingleton<ITeamsNotificationService>(sp =>
        new TeamsWebhookService(
            new HttpClient(),
            teamsWebhookUrl,
            projectName,
            instanceId,
            sp.GetRequiredService<IGatewayClient>()));
    
    // Start HTTP listener for replies
    builder.Services.AddHostedService(sp =>
        new TeamsReplyListener(
            sp.GetRequiredService<ITeamsNotificationService>(),
            assignedPort));
    
    // Register instance with Gateway on startup
    builder.Services.AddHostedService<GatewayRegistrationService>();
    
    mcpBuilder.WithTools<TeamsTools>();
    
    Console.Error.WriteLine($"Teams integration enabled: instance={instanceId}, port={assignedPort}");
}
```

---

## Error Handling

### Timeout (no reply)
```csharp
// AskQuestionAsync throws TimeoutException after 120s
// TeamsTools catches and returns error message:
return "ERROR: No reply received within 120 seconds. User may need to respond via another channel.";
```

### Gateway unavailable
```csharp
// GatewayClient.RegisterInstanceAsync throws HttpRequestException
// Caught in GatewayRegistrationService, logs error
// Instance continues without two-way Teams (degrades to one-way notifications only)
Console.Error.WriteLine("Gateway unavailable — AskUserViaTeams will not wait for replies.");
```

### Reply to wrong instance
```csharp
// Gateway maintains question→instance mapping
// If questionId not found in Gateway registry:
//   - Gateway logs warning, returns 404 to itself (doesn't affect Teams)
// If questionId not found in instance _pendingQuestions:
//   - Instance logs warning "Received reply for unknown question"
```

### Malformed reply
```csharp
// Gateway regex fails to extract questionId/answer
// Gateway logs error, returns 200 to Teams (acknowledges but ignores)
Console.Error.WriteLine("Could not parse reply format. Expected: @VoiceMCP [q-xxx-nnn] answer");
```

### Gateway crash/restart
- Active questions lost (instances timeout after 120s)
- Instances can re-register on next question
- Consider: Gateway persists registry to disk/Redis for restart resilience (future enhancement)

---

## VoiceMCP Gateway Implementation

The Gateway is a separate, long-running service (not part of VoiceMCP instances). Minimal ASP.NET Core app:

```csharp
// Gateway/Program.cs
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var instances = new ConcurrentDictionary<string, InstanceInfo>();
var questions = new ConcurrentDictionary<string, string>(); // questionId → instanceId

app.MapPost("/gateway/register", (InstanceRegistration reg) =>
{
    instances[reg.InstanceId] = new InstanceInfo(reg.CallbackUrl, reg.ProjectName);
    Console.WriteLine($"Registered instance {reg.InstanceId} ({reg.ProjectName})");
    return Results.Ok();
});

app.MapPost("/gateway/questions", (QuestionRegistration reg) =>
{
    questions[reg.QuestionId] = reg.InstanceId;
    Console.WriteLine($"Registered question {reg.QuestionId} → {reg.InstanceId}");
    return Results.Ok();
});

app.MapDelete("/gateway/instances/{instanceId}", (string instanceId) =>
{
    instances.TryRemove(instanceId, out _);
    // Remove all questions for this instance
    var toRemove = questions.Where(kv => kv.Value == instanceId).Select(kv => kv.Key).ToList();
    foreach (var qId in toRemove)
        questions.TryRemove(qId, out _);
    Console.WriteLine($"Unregistered instance {instanceId}");
    return Results.Ok();
});

app.MapPost("/teams/webhook", async (HttpContext context) =>
{
    // Teams Outgoing Webhook callback
    var body = await context.Request.ReadFromJsonAsync<TeamsWebhookPayload>();
    
    // Parse: "@VoiceMCP [q-xxx-nnn] answer text"
    var match = Regex.Match(body.Text, @"\[([^\]]+)\]\s+(.+)", RegexOptions.IgnoreCase);
    if (!match.Success)
    {
        Console.WriteLine("Malformed reply, ignoring");
        return Results.Ok(new { type = "message", text = "Could not parse reply. Use: @VoiceMCP [question-id] your-answer" });
    }
    
    var questionId = match.Groups[1].Value;
    var answer = match.Groups[2].Value.Trim();
    
    if (!questions.TryGetValue(questionId, out var instanceId))
    {
        Console.WriteLine($"Question {questionId} not found");
        return Results.Ok(new { type = "message", text = "Question ID not recognized or already answered." });
    }
    
    if (!instances.TryGetValue(instanceId, out var instance))
    {
        Console.WriteLine($"Instance {instanceId} not registered");
        return Results.Ok(new { type = "message", text = "The VoiceMCP instance is no longer running." });
    }
    
    // Forward to instance (async, don't block Teams webhook)
    _ = Task.Run(async () =>
    {
        try
        {
            using var client = new HttpClient();
            var payload = new { questionId, answer, userId = body.From?.Id, userName = body.From?.Name };
            await client.PostAsJsonAsync(instance.CallbackUrl, payload);
            Console.WriteLine($"Forwarded reply for {questionId} to {instanceId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to forward reply: {ex.Message}");
        }
    });
    
    // Respond to Teams immediately
    return Results.Ok(new { type = "message", text = "✓ Reply forwarded to VoiceMCP" });
});

app.Run("http://localhost:8080");

record InstanceRegistration(string InstanceId, string CallbackUrl, string ProjectName);
record QuestionRegistration(string QuestionId, string InstanceId);
record InstanceInfo(string CallbackUrl, string ProjectName);
record TeamsWebhookPayload(string Text, FromInfo? From);
record FromInfo(string Id, string Name);
```

**Deployment:**
- Run as systemd service (Linux) or Windows Service
- Configure Teams Outgoing Webhook to: `http://<gateway-host>:8080/teams/webhook`
- Can be hosted on any machine accessible from Teams (often same as instance, but doesn't have to be)

---

## Configuration

### Environment Variables (VoiceMCP Instance)
```bash
TEAMS_WEBHOOK_URL=https://outlook.office.com/webhook/...  # Incoming webhook (existing)
VOICEMCP_GATEWAY_URL=http://localhost:8080                # Gateway URL (new, optional, defaults to localhost:8080)
```

### Teams Configuration
1. **Incoming Webhook** (existing): Used to POST cards to channel
2. **Outgoing Webhook** (new): Configure in Teams app settings:
   - Name: `VoiceMCP`
   - Callback URL: `http://<gateway-host>:8080/teams/webhook`
   - Description: "Enables two-way communication with VoiceMCP"

---

## Testing Strategy

### Unit Tests
- `TeamsWebhookService.AskQuestionAsync`: Mock IGatewayClient, verify question registration + card posting + TCS wait
- `TeamsWebhookService.OnReplyReceived`: Verify TCS completion
- `GatewayClient`: Mock HttpClient, verify correct API calls

### Integration Tests
1. Start Gateway + 2 VoiceMCP instances
2. Instance 1 posts question → verify card in Teams
3. Simulate Teams callback with reply → verify correct instance receives reply
4. Verify timeout: post question, don't reply, expect TimeoutException after 120s
5. Simulate Gateway crash → verify instance degrades gracefully

### Manual Testing
1. Start Gateway: `dotnet run --project Gateway`
2. Start VoiceMCP: `voicemcp` (or via Copilot CLI)
3. Trigger `AskUserViaTeams("Test question?")`
4. In Teams, reply: `@VoiceMCP [q-xxx-001] yes`
5. Verify reply received in VoiceMCP logs + returned to MCP client

---

## Migration Path

### Phase 1: Gateway Implementation
- Implement VoiceMCP.Gateway as separate project
- Deploy Gateway, configure Teams Outgoing Webhook
- No changes to existing VoiceMCP behavior (one-way still works)

### Phase 2: Instance Updates
- Add IGatewayClient, TeamsReplyListener, port allocation to VoiceMCP
- Update TeamsWebhookService to support blocking AskQuestionAsync
- Deploy updated VoiceMCP (backward compatible: works with/without Gateway)

### Phase 3: Enable Two-Way
- Set VOICEMCP_GATEWAY_URL in instance environments
- AskUserViaTeams now blocks and waits for replies
- Old instances without VOICEMCP_GATEWAY_URL continue one-way behavior

---

## Future Enhancements

1. **Gateway persistence:** Redis/SQLite for question registry (survive restarts)
2. **Multi-tenant Gateway:** Support multiple Teams tenants, route by webhook signature
3. **Web UI:** Gateway dashboard showing active instances, pending questions
4. **Retry logic:** Gateway retries instance callback if first attempt fails
5. **Security:** HMAC signature verification for Teams webhook + instance callbacks

---

## Summary

This architecture enables two-way Teams communication via a **shared Gateway** that routes replies to the correct VoiceMCP instance. Key benefits:

- **Stable endpoint** for Teams Outgoing Webhook (no per-instance URL management)
- **Simple instance logic** (just register + wait)
- **Works across machines** (Gateway can route to remote instances)
- **Meets 5-second timeout** (Gateway responds immediately, forwards async)
- **Graceful degradation** (instances work without Gateway, just no blocking AskUserViaTeams)

The Gateway is a minimal, stateless service that can be deployed once and serve all VoiceMCP instances in an organization.
