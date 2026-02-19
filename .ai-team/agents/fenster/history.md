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
