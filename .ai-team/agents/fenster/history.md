# Fenster — History

## Project Context
- **Project:** VoiceMCP — MCP server for multi-channel AI-to-human communication
- **Stack:** C#, .NET 10, Semantic Kernel, ModelContextProtocol SDK, NAudio
- **User:** Tamir Dresher (tamir.dresher@gmail.com)
- **Description:** MCP server giving AI coding agents the ability to communicate with humans through voice (Azure OpenAI TTS + Whisper) and Teams (Incoming Webhook with adaptive cards). Packaged as a dotnet tool, published as NuGet package.
- **Key files:** Program.cs, VoiceTools.cs, IVoiceService.cs, SemanticKernelVoiceService.cs, WindowsVoiceService.cs

## Learnings
- **Conditional tool registration pattern:** The MCP SDK (0.4.1-preview.1) supports both `WithToolsFromAssembly()` (attribute-based discovery) and `WithTools<T>()` (explicit registration). These compose — you can call both. This enables keeping `[McpServerToolType]` on always-available tools while conditionally registering optional tool classes that omit the attribute.
- **HttpClient in singleton services:** Avoided pulling in `Microsoft.Extensions.Http` for `IHttpClientFactory`. For a long-lived singleton like `TeamsWebhookService`, a single `new HttpClient()` is fine — the service lives for the process lifetime anyway.
- **Project name from working directory:** `Path.GetFileName(Path.GetFullPath("."))` gives a clean project/worktree name for Teams card context without shelling out to git.
- **Two-way Teams communication architecture:** Implemented Gateway-based routing for AskUserViaTeams replies. Each VoiceMCP instance runs an HttpListener on auto-allocated port (8090-8190), registers with Gateway, and uses TaskCompletionSource to block until reply arrives or timeout. Question IDs use format `q-{instanceId}-{seq}` for routing. Graceful degradation: if Gateway unavailable, falls back to one-way notification mode.
- **HttpListener port allocation:** Used simple loop to test ports 8090-8190 by attempting to start/stop HttpListener. More reliable than socket-based checks on Windows.
- **TaskCompletionSource for blocking MCP tools:** AskUserViaTeams uses ConcurrentDictionary<string, TaskCompletionSource<string>> to map question IDs to pending replies. When Gateway calls back with answer, OnReplyReceived resolves the TCS. Timeout handled via Task.WhenAny with Task.Delay.
- **IHttpClientFactory for all HTTP clients:** Changed from direct `new HttpClient()` to `IHttpClientFactory.CreateClient()` in all services. This prevents socket exhaustion and follows .NET best practices. `Microsoft.Extensions.Http` package was already in the project.
- **Sentinel text for Playwright polling:** Adaptive cards now include a subtle footer `⚡ {questionId}` when a question ID is present. This enables Gateway's Playwright to find and identify specific question cards using regex pattern matching, as Teams Incoming Webhook API doesn't return message IDs.
- **Shared secret authentication:** VoiceMCP generates a random GUID secret on startup, passes it during registration, and validates it on incoming reply callbacks. This prevents local process spoofing attacks. Secret is sent in request body (not headers) as per design decisions.
- **Heartbeat pattern for session management:** `HeartbeatService` (BackgroundService) POSTs to `/gateway/heartbeat` every 30 seconds with `instanceId` and `secret`. This allows Gateway to detect dead CLI sessions (crashed, killed, debugger attached) and evict them after 3 missed heartbeats (90s).
- **Service registration order matters:** `TeamsReplyListener` must be registered as singleton BEFORE `ITeamsNotificationService` so it can be injected into `GatewayRegistrationService` and `HeartbeatService`. Used `AddSingleton()` then `AddHostedService(sp => sp.GetRequiredService<T>())` pattern.
- **Gateway-aware AskQuestionAsync:** Method now blocks on `TaskCompletionSource` when Gateway is available, registers question with Gateway, awaits reply with timeout, and returns actual answer. Falls back to `QUESTION_POSTED:{id}` only when Gateway is NOT available, maintaining backward compatibility.

