# Decision: Two-Way Teams Communication Implementation

**Author:** Fenster  
**Date:** 2025-06-XX  
**Status:** Implemented

## Summary

Implemented Gateway-based two-way Teams communication for VoiceMCP instances. AskUserViaTeams now blocks and returns actual user replies when Gateway is available, with graceful degradation to one-way notification mode when Gateway is unavailable.

## Components Created

1. **IGatewayClient / GatewayClient** — HTTP client for Gateway communication (register instance, register question, unregister)
2. **TeamsReplyListener** — IHostedService that runs HttpListener on auto-allocated port (8090-8190) to receive reply callbacks
3. **GatewayRegistrationService** — IHostedService that registers/unregisters instance with Gateway on startup/shutdown
4. **TeamsWebhookService updates** — Added blocking AskQuestionAsync with TaskCompletionSource-based reply handling and timeout

## Architecture Decisions

- **Graceful degradation everywhere:** All Gateway interactions wrapped in try/catch. If Gateway unreachable, log warning and continue. One-way Teams notifications always work.
- **Port allocation strategy:** Simple loop from 8090-8190 testing HttpListener start/stop. More reliable than socket checks on Windows.
- **Instance identity:** 8-char GUID generated at TeamsWebhookService construction, exposed via InstanceId property for Gateway registration.
- **Question correlation:** Format `q-{instanceId}-{seq}` where seq is an Interlocked.Increment counter.
- **Blocking mechanism:** ConcurrentDictionary maps questionId → TaskCompletionSource<string>. OnReplyReceived resolves TCS. Timeout via Task.WhenAny.
- **No reflection hacks:** Initially tried reflecting into TeamsWebhookService for instanceId, replaced with clean InstanceId property.

## Dependencies

- Gateway must implement endpoints per Keaton's architecture spec:
  - `POST /gateway/register` — Register instance
  - `POST /gateway/questions` — Register question
  - `DELETE /gateway/instances/{id}` — Unregister instance
- Gateway must call back to `POST /voice-mcp/reply` with `{questionId, answer, userId, userName}`

## Environment Variables

- `VOICEMCP_GATEWAY_URL` — Gateway base URL (default: `http://localhost:8080`)
- `TEAMS_WEBHOOK_URL` — Teams Incoming Webhook (required for Teams integration)

## Testing Notes

- Build succeeds with no compilation errors (warnings are file locking from running process)
- Requires actual Gateway implementation to test two-way flow
- One-way flow tested and working (existing functionality preserved)
