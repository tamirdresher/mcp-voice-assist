### 2025-07-18: Teams tools class has no [McpServerToolType] attribute
**By:** McManus
**What:** `TeamsTools` is a plain class with `[McpServerTool]` methods but no `[McpServerToolType]` class attribute. This prevents the assembly scanner from auto-discovering it. Fenster registers it explicitly via `mcpBuilder.WithTools<TeamsTools>()` only when the `TEAMS_WEBHOOK_URL` environment variable is set.
**Why:** Teams integration is optional — if no webhook URL is configured, the MCP server should not expose Teams tools to the agent. Using `[McpServerToolType]` would bypass that conditional logic and always register the tools.

### 2025-07-18: Added Microsoft.Extensions.Http package dependency
**By:** McManus
**What:** Added `Microsoft.Extensions.Http` NuGet package to support `IHttpClientFactory` for Teams webhook HTTP calls.
**Why:** The DI registration in Program.cs uses `builder.Services.AddHttpClient()` and resolves `IHttpClientFactory` to create the `HttpClient` injected into `TeamsWebhookService`. This follows .NET best practices for HTTP client lifecycle management.

### 2025-07-18: Adaptive card $schema workaround
**By:** McManus
**What:** The adaptive card JSON payload requires a `$schema` key, but C# anonymous type properties can't start with `$`. The implementation serializes with a `schema` property and does a string replace to `$schema` post-serialization.
**Why:** Keeps the card-building code simple with anonymous objects instead of requiring a separate JSON template or dictionary-based construction.
