# McManus — History

## Project Context
- **Project:** VoiceMCP — MCP server for multi-channel AI-to-human communication
- **Stack:** C#, .NET 10, Semantic Kernel, ModelContextProtocol SDK, NAudio
- **User:** Tamir Dresher (tamir.dresher@gmail.com)
- **Description:** MCP server giving AI coding agents the ability to communicate with humans through voice (Azure OpenAI TTS + Whisper) and Teams (Incoming Webhook with adaptive cards). Packaged as a dotnet tool, published as NuGet package.
- **Teams channel:** Incoming Webhook with adaptive cards, webhook URL from environment variable

## Learnings
- TeamsTools class deliberately omits `[McpServerToolType]` so the assembly scanner won't auto-register it. Fenster's Program.cs uses explicit `WithTools<TeamsTools>()` gated on webhook URL presence.
- Adaptive card JSON uses anonymous objects serialized with `System.Text.Json`. The `$schema` key requires a post-serialization string replace since C# property names can't start with `$`.
- `Microsoft.Extensions.Http` package is required for `IHttpClientFactory` / `AddHttpClient()` — added to csproj.
- Project name is derived from the working directory name in Program.cs and passed through DI to TeamsWebhookService for card branding.
- Teams Incoming Webhooks are one-way (fire-and-forget). `AskQuestionAsync` posts the card but returns a message telling the agent that the user must respond through another channel.
- VoiceMCP.Gateway is a separate console project using ASP.NET Core minimal APIs. Use `FrameworkReference` not `PackageReference` for Microsoft.AspNetCore.App in .NET 10.
- In .NET 10 minimal API, set URLs after building the app using `app.Urls.Add($"http://0.0.0.0:{port}")` before calling `app.Run()`.
- Gateway uses in-memory `ConcurrentDictionary` for instance registry and question mappings. Stale instances cleaned up after 5 minutes of no heartbeat.
- HMAC validation requires reading the request body, validating, then resetting the body position to 0 for JSON deserialization.
- Gateway must respond to Teams within 5 seconds, then forward the answer to instance callback URL asynchronously using `Task.Run()`.
- Playwright polling gateway replaces Teams Outgoing Webhook approach (DLP blocks it). Uses Microsoft.Playwright package with MSEdge channel to avoid downloading Chromium.
- Gateway uses dedicated browser profile at `%LOCALAPPDATA%\VoiceMCP\playwright-profile-{port}` via `LaunchPersistentContextAsync` to avoid conflicts with other Playwright instances.
- Playwright returns `IBrowserContext` from `LaunchPersistentContextAsync`, not `IBrowser`. Use `.Pages` on context, not browser.
- Teams web DOM: replies appear in `[role="group"]` containers. Search for sentinel `⚡ q-{hex}-{seq}` pattern, then check for `[aria-label*="reply"]` with `\d+ repl` regex.
- Audio transcripts in Teams: click `[data-testid="transcript-button"]` then read `[data-testid="transcript-paragraph"]` for text.
- Gateway enforces singleton via `Global\VoiceMCP-Gateway` mutex using `MutexAcl.Create()` (not `new Mutex()` constructor which has wrong signature in .NET 10).
- BackgroundService and ILogger require `Microsoft.Extensions.Hosting` and `Microsoft.Extensions.Logging` usings. HttpClient.PostAsJsonAsync requires `System.Net.Http.Json`.
- Cannot use `AddSingleton(port)` for value types — use lambda factory: `AddSingleton(sp => new Service(..., port))`.
- Adaptive polling: 5s interval when questions pending, 30s when idle, exponential backoff on errors (max 5 min). Prevents resource waste while maintaining responsiveness.
- Shared secret validation: registration payload must include `secret` field, validated on heartbeat and callback. Prevents local process spoofing.
- Health endpoint exposes `playwrightStatus`, `activeInstances`, `pendingQuestions`, `uptimeSec` for ops monitoring and debugging.
