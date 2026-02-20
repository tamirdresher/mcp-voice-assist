# Decision: Two-Way Teams Communication Test Strategy

**Status:** Implemented  
**Author:** Hockney  
**Date:** 2025-01-XX  
**Context:** Testing the two-way Teams communication system being implemented by McManus and Fenster

---

## Test Coverage

Created 59 tests across 4 new test files:

### 1. GatewayClientTests (11 tests)
Tests the client that registers instances/questions with the Gateway:
- Registration endpoints (instance, question, unregister)
- Correct payload serialization
- Error handling (Gateway down, network failures)
- Graceful degradation on shutdown

### 2. TeamsReplyListenerTests (8 tests)
Tests the HTTP listener that receives callbacks from Gateway:
- Port allocation (8090-8190 range)
- Reply forwarding to service
- Payload validation (malformed JSON, missing fields)
- Quick response times (< 5s Teams requirement)
- Optional user fields handling

### 3. TeamsWebhookServiceReplyTests (15 tests)
Tests the updated webhook service with blocking ask:
- Correlation ID generation (q-{instanceId}-{seq})
- Gateway registration before posting card
- Blocking until reply or timeout
- Multiple concurrent questions handled independently
- Reply routing to correct pending question
- Timeout handling (default 120s)
- Sequence number incrementing

### 4. GatewayIntegrationTests (25 tests)
Tests the Gateway routing logic:
- In-memory Gateway implementation for testing
- Multi-instance registration and routing
- Correlation ID parsing from Teams @mentions
- Unknown question ID handling
- Malformed reply handling
- Stale instance cleanup
- Response time verification (< 5s)
- HMAC validation spec (placeholder for future)

---

## Testing Approach

**Tests written against INTERFACES, not implementation:**
- Used dynamic typing to work before implementation exists
- Tests will bind to actual types when code lands
- Verifies behavior specified in Keaton's architecture doc
- No coupling to implementation details

**Edge cases covered:**
- Reply arrives after timeout (ignored)
- Multiple instances, same Gateway
- Concurrent questions on same instance
- Malformed @mention text
- Instance crashes without unregistering
- Gateway unavailable (graceful degradation)
- Network failures during registration
- Port allocation when ports are busy

**Test patterns:**
- xUnit + Moq (consistent with existing tests)
- Mock HttpMessageHandler for HTTP calls
- In-memory Gateway for integration tests
- Port availability checking with fallback

---

## Key Decisions

1. **Dynamic typing for pre-implementation testing:**
   - Tests reference types via `Type.GetType()` and `dynamic`
   - Skip tests if types not yet available
   - Will automatically work once implementation lands

2. **In-memory Gateway for integration tests:**
   - Simulates full routing logic without actual ASP.NET hosting
   - Verifies correlation ID parsing, routing, cleanup
   - Can be extended to TestServer/WebApplicationFactory later

3. **No source file modifications:**
   - Only created test files in VoiceMCP.Tests/
   - Did not touch TeamsWebhookService, TeamsTools, or any Services files
   - Respects parallel work by McManus and Fenster

4. **Correlation ID format validation:**
   - Tests verify exact format: `q-{instanceId}-{sequenceNumber}`
   - Sequence number must be 3-digit zero-padded (001, 002, etc.)
   - Instance ID must be 8-char hex string

---

## Dependencies

Tests will run once implementation includes:
- `VoiceMCP.Services.IGatewayClient` interface
- `VoiceMCP.Services.GatewayClient` class
- `VoiceMCP.Services.TeamsReplyListener` class
- Updated `TeamsWebhookService` constructor (with instanceId and IGatewayClient)
- Updated `ITeamsNotificationService.AskQuestionAsync` signature (with timeout, returns string?)
- `OnReplyReceived` method on ITeamsNotificationService

Tests are complete and ready for validation as soon as the implementation lands.

---

## Next Steps

1. McManus/Fenster land the implementation
2. Run tests to verify implementation matches spec
3. Fix any deviations from expected behavior
4. Add Microsoft.AspNetCore.Mvc.Testing if needed for full Gateway hosting tests
5. Consider adding performance tests (latency, throughput, concurrent instances)
