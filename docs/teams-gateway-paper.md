# Two-Way Teams Communication for AI Coding Agents via Playwright Gateway

**Author:** Keaton (Lead/Architect, VoiceMCP)
**Audience:** Brady Gaster — for replication in Squad or standalone integration
**Date:** 2025-07

---

## 1. The Problem

AI coding agents (Squad, Copilot CLI, custom MCP tools) run in ephemeral terminal sessions. Sometimes the agent needs to ask the human a question — "should I delete this table?" or "which auth provider?" — and block until it gets an answer.

The human might be in a meeting, at lunch, or on their phone. They're not watching the terminal. But they *are* on Teams.

**Goal:** Let an AI agent post a question to a Teams channel and synchronously block until the human replies in-thread, then continue with the answer. No bot framework, no Entra app registration, no admin consent flows.

## 2. Why Not Power Automate?

We tried three "proper" approaches first. All failed in enterprise environments:

| Approach | Failure Mode |
|----------|-------------|
| **Power Automate HTTP connector** | DLP policies block the HTTP connector tenant-wide. You can build the flow, but it silently fails at runtime. |
| **"When a new channel message is added" trigger** | Platform bug: the trigger crashes with a `DateTime` parsing exception on certain message formats. Microsoft-side issue, no workaround. |
| **Outgoing Webhooks** | Also DLP-blocked. Even if allowed, the 5-second response timeout makes async routing impossible — you can't forward to a CLI session and get back in time. |

The only reliable outbound channel that survives DLP policies is the **Incoming Webhook** connector. It's one-way (post cards to Teams), but it works everywhere. The trick is building the return path ourselves.

## 3. The Architecture

```
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ CLI Session 1 │    │ CLI Session 2 │    │ CLI Session N │
│  (MCP Server) │    │  (MCP Server) │    │  (MCP Server) │
└──────┬───────┘    └──────┬───────┘    └──────┬───────┘
       │ POST /register    │ POST /register    │
       │ POST /questions   │                   │
       ▼                   ▼                   ▼
┌─────────────────────────────────────────────────────┐
│              Gateway  (singleton, port 8080)         │
│                                                     │
│  ConcurrentDictionary<instanceId, registration>     │
│  ConcurrentDictionary<questionId, instanceId>       │
│                                                     │
│  ┌─────────────────────────────────────────────┐    │
│  │  TeamsPlaywrightPoller (BackgroundService)   │    │
│  │  - Edge browser with user's work profile     │    │
│  │  - Navigates to wizard channel via sidebar   │    │
│  │  - Polls DOM for sentinel text ⚡ q-xxx-nnn  │    │
│  │  - Detects thread replies                    │    │
│  │  - POSTs answer to CLI callback URL          │    │
│  └─────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────┘
       │                                      ▲
       │ Incoming Webhook                     │ DOM polling
       ▼                                      │
┌─────────────────────────────────────────────────────┐
│                Microsoft Teams Channel              │
│                                                     │
│  [Adaptive Card]         [Thread Reply]             │
│  ⚡ q-a1b2c3d4-001       "Yes, delete it"           │
└─────────────────────────────────────────────────────┘
```

**Key insight:** The Gateway is a singleton process on the dev machine that owns a Playwright browser instance. It multiplexes across all concurrent CLI sessions. CLI sessions are ephemeral; the Gateway is persistent.

### Why Edge with the user's profile?

The Gateway launches Edge using `LaunchPersistentContextAsync` pointed at `%LOCALAPPDATA%\Microsoft\Edge\User Data`. This reuses existing Teams auth cookies — no separate login, no token management, no Entra app. The user's daily browser session *is* the auth.

## 4. How It Works (Step by Step)

### Step 1: CLI posts adaptive card with sentinel

The CLI's `TeamsWebhookService` posts an adaptive card via Incoming Webhook. The card includes a sentinel `TextBlock` at the bottom:

```csharp
// TeamsWebhookService.cs — sentinel embedded in adaptive card
if (!string.IsNullOrEmpty(questionId))
{
    body.Add(new {
        type = "TextBlock",
        text = $"⚡ {questionId}",   // e.g. "⚡ q-a1b2c3d4-001"
        size = "Small",
        isSubtle = true
    });
}
```

Question IDs follow the format `q-{instanceId}-{sequence}` where `instanceId` is an 8-char GUID prefix unique to the CLI session.

### Step 2: CLI registers question with Gateway

Simultaneously, the CLI registers the question so the Gateway knows to watch for it:

```csharp
// TeamsWebhookService.cs — AskQuestionAsync
var tcs = new TaskCompletionSource<string>();
_pendingQuestions[questionId] = tcs;

await _gatewayClient.RegisterQuestionAsync(questionId, _instanceId);

// Block until reply or timeout
var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(120)));
if (completedTask == tcs.Task)
    return await tcs.Task;  // Got the answer
else
    return $"Timeout: No reply within 120 seconds";
```

The Gateway stores the mapping `questionId → instanceId` in a `ConcurrentDictionary`.

### Step 3: Gateway's Playwright poller detects the sentinel

The `TeamsPlaywrightPoller` runs a poll loop. When questions are pending, it polls every 5 seconds (30 seconds when idle):

```csharp
// TeamsPlaywrightPoller.cs — core detection loop
var messageGroups = await _page.Locator("[role=\"group\"]").AllAsync();
foreach (var group in messageGroups.TakeLast(20))
{
    var groupText = await group.InnerTextAsync();

    // Regex matches: ⚡ q-a1b2c3d4-001
    var sentinelMatch = Regex.Match(groupText, @"⚡ (q-[a-zA-Z0-9]+-\d{3})");
    if (!sentinelMatch.Success) continue;

    var questionId = sentinelMatch.Groups[1].Value;
    if (_processedQuestions.Contains(questionId)) continue;
    if (!_questionToInstance.TryGetValue(questionId, out var instanceId)) continue;

    // Check for thread replies
    var replyContainers = await group.Locator("[aria-label*=\"reply\"]").AllAsync();
    // ... extract reply text from <p> elements ...

    await DeliverReplyAsync(questionId, instanceId, replyText, ...);
    _processedQuestions.Add(questionId);
}
```

### Step 4: User replies in the thread

The user sees the adaptive card in Teams, clicks the thread, types their answer. Nothing special here — just a normal Teams reply.

### Step 5: Gateway delivers the reply

When the poller detects a reply in a thread containing a known sentinel, it POSTs to the CLI's callback URL:

```csharp
// TeamsPlaywrightPoller.cs — DeliverReplyAsync
var payload = new {
    questionId,
    answer,
    userId = "teams-user",
    timestamp = DateTime.UtcNow,
    secret = instance.Secret
};
await client.PostAsJsonAsync(instance.CallbackUrl, payload);
```

### Step 6: CLI's TaskCompletionSource completes

The CLI runs a `TeamsReplyListener` — a lightweight `HttpListener` on an auto-allocated port (8090–8190). When the Gateway POSTs:

```csharp
// TeamsReplyListener.cs — callback handler
_teamsService.OnReplyReceived(reply.QuestionId, reply.Answer);

// Inside TeamsWebhookService:
public void OnReplyReceived(string questionId, string answer)
{
    if (_pendingQuestions.TryRemove(questionId, out var tcs))
        tcs.TrySetResult(answer);  // Unblocks the agent
}
```

The agent's `AskQuestionAsync` call returns with the human's answer. The agent continues.

## 5. Key Technical Decisions

### Edge default profile for auth
Using `LaunchPersistentContextAsync` with Edge's actual user data directory means zero auth setup. The tradeoff: Edge must be closed when Gateway starts (browser profile lock). After launch, Edge can be reopened.

### Sidebar click over URL navigation
Direct URL navigation to a Teams channel doesn't scroll to the latest messages. Clicking the channel in the sidebar does. The poller uses `data-testid` attributes to find the channel:

```csharp
var wizardChannel = _page.Locator(
    $"[data-testid*=\"favorite-channel-list-item-{ChannelId}\"]");
await wizardChannel.First.EvaluateAsync("node => node.click()");
```

### InnerTextAsync over TextContentAsync
Adaptive cards render into complex shadow DOM structures in Teams. `TextContentAsync` returns raw concatenated text nodes (often empty for card content). `InnerTextAsync` returns the *visible rendered text*, which reliably includes the sentinel.

### Heartbeat + eviction
CLI sessions send heartbeats every 30 seconds. The `HeartbeatCleanupService` evicts instances silent for 90+ seconds, cleaning up their pending questions. This prevents zombie question registrations when a CLI crashes.

### Singleton mutex
`SingleInstanceMutex` uses a named OS mutex (`Global\VoiceMCP-Gateway`) to guarantee only one Gateway runs. Multiple Gateways polling the same channel would produce duplicate reply deliveries.

### Shared secret per session
Each CLI session generates a `Guid.NewGuid()` secret at startup, sends it during registration, and the Gateway echoes it back on reply delivery. The `TeamsReplyListener` validates it. This prevents adjacent localhost processes from injecting fake replies.

## 6. What You Need to Implement This

| Component | Size | Purpose |
|-----------|------|---------|
| **Gateway** (`Program.cs` + services) | ~300 lines | Singleton HTTP server + Playwright poller |
| **CLI integration** (`TeamsWebhookService`, `GatewayClient`, `TeamsReplyListener`) | ~250 lines | Card posting, Gateway registration, callback listener |
| **Heartbeat + cleanup** | ~70 lines each | Liveness + eviction |

### Prerequisites
- **.NET 10 SDK** (or 8+ with minor adjustments)
- **Playwright for .NET** — `Microsoft.Playwright` NuGet package + `pwsh playwright.ps1 install chromium`
- **Microsoft Edge** (ships with Windows)
- **Teams Incoming Webhook URL** — configure in your target channel
- (Optional) **Task Scheduler** entry for auto-start at logon

### Minimal Gateway setup
```powershell
dotnet new web -n TeamsGateway
cd TeamsGateway
dotnet add package Microsoft.Playwright
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install chromium

# First run: headed mode to capture auth cookies
$env:GATEWAY_HEADLESS="false"
dotnet run
```

## 7. Limitations & Future

### Current limitations
- **Edge must be closed at Gateway startup** — persistent browser context locks the profile directory. After launch, Edge can be reopened normally.
- **No persistent message queue** — if the CLI crashes between posting the card and receiving the reply, the reply is lost. The Gateway delivers to the callback URL once; if it fails, it logs and moves on.
- **Single browser = throughput ceiling** — one Playwright instance polls one channel view. For heavy use, the 5-second poll interval is the bottleneck (not a problem for typical dev workflows).
- **DOM selectors are fragile** — Teams web UI changes can break `[role="group"]` or `[data-testid]` selectors. This has been stable for months but isn't guaranteed.

### Future directions
- **WebSocket push delivery** — replace HTTP callback polling with a WebSocket connection from CLI→Gateway for instant reply delivery and bidirectional health monitoring.
- **Headless after first auth** — run headed once to capture cookies, then switch to headless permanently. Already supported via `GATEWAY_HEADLESS` env var.
- **Persistent question queue** — SQLite backing store so replies survive CLI crashes. Gateway re-delivers on reconnection.
- **Multi-channel support** — route different projects to different Teams channels by mapping project names to channel IDs.
- **Graph API migration** — if/when DLP policies allow, replace Playwright with Microsoft Graph `chatMessage` subscriptions for proper real-time delivery. The Gateway architecture makes this a swap of the poller implementation.

---

## Quick Reference: REST API

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/gateway/register` | POST | Register CLI instance (instanceId, callbackUrl, secret) |
| `/gateway/heartbeat` | POST | Keep-alive (instanceId, secret) |
| `/gateway/questions` | POST | Register question for polling (questionId, instanceId) |
| `/gateway/instances/{id}` | DELETE | Unregister instance |
| `/gateway/health` | GET | Status check (playwright status, active instances, pending questions) |

---

*Built as part of the [VoiceMCP](https://github.com/tamirdresher/mcp-voice-assist) project — an MCP server giving AI coding agents voice and Teams communication channels.*
