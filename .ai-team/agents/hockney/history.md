# Hockney — History

## Project Context
- **Project:** VoiceMCP — MCP server for multi-channel AI-to-human communication
- **Stack:** C#, .NET 10, Semantic Kernel, ModelContextProtocol SDK, NAudio
- **User:** Tamir Dresher (tamir.dresher@gmail.com)
- **Description:** MCP server giving AI coding agents the ability to communicate with humans through voice (Azure OpenAI TTS + Whisper) and Teams (Incoming Webhook with adaptive cards). Packaged as a dotnet tool, published as NuGet package.

## Learnings
- Created `VoiceMCP.Tests` project using xUnit + Moq, targeting net10.0, added to solution
- McManus landed Teams integration before tests were written — adapted tests to actual implementation rather than contract stubs
- `TeamsWebhookService.PostCardAsync` catches all exceptions and returns bool — `SendNotificationAsync` never throws, it silently swallows errors. Tests verify this graceful degradation.
- `TeamsTools` wraps all service calls in try/catch and returns error strings — no exceptions propagate to MCP callers
- `TeamsTools` deliberately omits `[McpServerToolType]` — Fenster will register it conditionally based on `TEAMS_WEBHOOK_URL` env var
- Program.cs conditional registration is not yet implemented by Fenster — wrote type-level verification tests and documented manual verification steps
- xUnit 2.9.3 + xunit.runner.visualstudio 3.0.2 + Microsoft.NET.Test.Sdk 17.13.0 is the working package combo for net10.0
- Mock `HttpMessageHandler` via `Moq.Protected()` is the standard pattern for testing `HttpClient`-based services
- 34 tests total: 20 for TeamsWebhookService, 10 for TeamsTools, 4 for conditional registration contracts
