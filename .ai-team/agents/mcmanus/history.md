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
