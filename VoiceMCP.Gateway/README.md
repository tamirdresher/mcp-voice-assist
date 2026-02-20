# VoiceMCP Gateway

**Central routing service for two-way Teams communication across multiple VoiceMCP instances.**

## Purpose

The Gateway solves the multi-instance routing problem:
- Multiple VoiceMCP instances may run simultaneously (same/different machines, same/different users)
- All instances post to the same Teams channel via shared Incoming Webhook
- Teams Outgoing Webhook sends ALL @mentions to ONE callback URL
- Gateway routes replies to the correct instance based on correlation ID embedded in the question

## Architecture

```
Teams Channel → Outgoing Webhook → Gateway → Routes to correct VoiceMCP instance
```

See `.ai-team/decisions/inbox/keaton-teams-reply-architecture.md` for full architecture details.

## Running the Gateway

```bash
dotnet run --project VoiceMCP.Gateway
```

## Configuration

Environment variables:
- `GATEWAY_PORT` — Port to listen on (default: 8080)
- `TEAMS_OUTGOING_WEBHOOK_TOKEN` — Optional HMAC validation token for Teams webhook security. If not set, HMAC validation is skipped (development mode).

## Endpoints

### `POST /gateway/register`
Register a VoiceMCP instance with the gateway.

**Request:**
```json
{
  "instanceId": "a3f7b2c1",
  "callbackUrl": "http://localhost:8091/voice-mcp/reply",
  "projectName": "MyProject"
}
```

**Response:** `200 OK`

### `DELETE /gateway/instances/{instanceId}`
Unregister an instance (removes instance and all pending questions).

**Response:** `200 OK` or `404 Not Found`

### `POST /gateway/questions`
Register a pending question before posting to Teams.

**Request:**
```json
{
  "questionId": "q-a3f7b2c1-001",
  "instanceId": "a3f7b2c1"
}
```

**Response:** `200 OK` or `404 Not Found` (if instance not registered)

### `POST /gateway/webhook`
Teams Outgoing Webhook callback endpoint. Receives @mentions from Teams channel.

**Expected @mention format:**
```
@VoiceMCP [q-a3f7b2c1-001] yes please proceed
```

**Response:** Immediate `200 OK` acknowledgment to Teams (within 5 seconds), then async callback to instance.

## State Management

- **In-memory state:** `ConcurrentDictionary` for instances and question mappings
- **Stale cleanup:** Instances that haven't heartbeated in 5 minutes are automatically removed
- **No persistence:** State is lost on gateway restart (instances will re-register)

## Security

- **HMAC validation:** If `TEAMS_OUTGOING_WEBHOOK_TOKEN` is set, validates incoming webhook requests
- **Development mode:** If token not set, HMAC validation is skipped

## Logging

All diagnostics written to `stderr` for consistency with VoiceMCP pattern.
