# Decision: VoiceMCP Gateway Implementation Complete

**Status:** Implemented  
**Author:** McManus  
**Date:** 2025-01-XX

## Summary

Implemented VoiceMCP.Gateway as a separate .NET 10 console project using ASP.NET Core minimal APIs. The gateway provides centralized routing for two-way Teams communication across multiple VoiceMCP instances.

## Implementation Details

### Project Structure
- **Location:** `VoiceMCP.Gateway/` (sibling to VoiceMCP)
- **Type:** .NET 10 console app with ASP.NET Core minimal APIs
- **Dependencies:** `FrameworkReference` to `Microsoft.AspNetCore.App`

### Endpoints Implemented
1. `POST /gateway/register` — Instance registration
2. `DELETE /gateway/instances/{instanceId}` — Instance unregistration
3. `POST /gateway/questions` — Question registration
4. `POST /gateway/webhook` — Teams Outgoing Webhook callback (HMAC validation, correlation ID parsing, async forwarding)

### Key Technical Decisions

**Port configuration:**
- Tried `builder.WebHost.UseUrls()` — not available on `ConfigureWebHostBuilder` in .NET 10
- Tried `builder.WebHost.UseKestrel()` — same issue
- **Solution:** Use `app.Urls.Add($"http://0.0.0.0:{port}")` after building the app

**HMAC validation:**
- Read request body to compute HMAC
- Reset `request.Body.Position = 0` after validation for JSON deserialization
- If `TEAMS_OUTGOING_WEBHOOK_TOKEN` not set, skip validation (dev mode)

**Async forwarding:**
- Gateway responds to Teams within 5 seconds (200 OK with acknowledgment)
- Forwards answer to instance callback URL asynchronously using `Task.Run()`
- Logs success/failure to stderr

**State management:**
- `ConcurrentDictionary<string, InstanceRegistration>` for instances
- `ConcurrentDictionary<string, string>` for questionId → instanceId mapping
- Background task runs every 1 minute to remove stale instances (no heartbeat in 5 minutes)

## Running the Gateway

```bash
dotnet run --project VoiceMCP.Gateway
```

Environment variables:
- `GATEWAY_PORT` (default: 8080)
- `TEAMS_OUTGOING_WEBHOOK_TOKEN` (optional, enables HMAC validation)

## Next Steps for Integration

VoiceMCP instances will need:
1. Instance ID generation at startup
2. HTTP listener for receiving callbacks (`/voice-mcp/reply` endpoint)
3. Registration with gateway before posting questions
4. Updated adaptive card with correlation ID instructions
5. Graceful unregistration on shutdown

These changes belong to Fenster (core functionality) — McManus has completed the gateway implementation per Keaton's architecture.
