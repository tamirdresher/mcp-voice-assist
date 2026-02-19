# Decision: Test Project Structure & Patterns

**Author:** Hockney (Tester)
**Date:** 2025-07-17

## Decision
Established `VoiceMCP.Tests` as the single test project for the solution, using xUnit + Moq.

## Test Organization
- One test class per production class: `TeamsWebhookServiceTests`, `TeamsToolsTests`, `ConditionalRegistrationTests`
- Tests grouped by `#region` blocks within each class (by method/concern)
- Naming convention: `MethodName_Condition_ExpectedResult`

## Mocking Patterns
- **HttpClient:** Mock `HttpMessageHandler` via `Moq.Protected()` — never mock `HttpClient` directly
- **Service interfaces:** Mock via `Moq` (standard interface mocking)
- **Captured request bodies:** Use `.Callback<HttpRequestMessage, CancellationToken>()` to capture and assert on HTTP payloads

## What's Covered
- Service-level HTTP interactions (adaptive card structure, error handling, network failures)
- Tool-level delegation and error wrapping
- Type-level contract verification (interface implementation, constructor signatures, attribute presence)

## What's NOT Covered (yet)
- Program.cs conditional registration (needs Fenster's implementation or a testable extraction)
- VoiceTools / AskUserTool (existing code — not in scope for this task, but should be next)
- Integration tests against real Teams webhooks

## Packages & Versions
- xUnit 2.9.3, xunit.runner.visualstudio 3.0.2, Microsoft.NET.Test.Sdk 17.13.0, Moq 4.20.72
- Target: net10.0 with RollForward=Major
