# Decision: Gateway Integration P0 Fixes

### 2026-02-20: VoiceMCP CLI Gateway Integration — P0 Fixes Implemented
**By:** Fenster (Backend Developer)  
**Status:** Implemented  

**What:** Implemented all P0 fixes required for proper Gateway integration in VoiceMCP CLI:

1. **Blocking AskQuestionAsync:** `TeamsWebhookService.AskQuestionAsync()` now registers a `TaskCompletionSource<string>` in `_pendingQuestions`, registers the question with Gateway via `IGatewayClient.RegisterQuestionAsync()`, and awaits the TCS with a 120-second timeout. Returns the actual answer from Gateway callback or a timeout message. Falls back to `QUESTION_POSTED:{id}` only when Gateway is NOT available.

2. **Hosted services registration:** `Program.cs` now checks for `VOICEMCP_GATEWAY_URL` environment variable. When set, it:
   - Registers `IGatewayClient` as singleton with `GatewayClient`
   - Registers `TeamsReplyListener` as singleton AND hosted service
   - Registers `GatewayRegistrationService` as hosted service
   - Registers `HeartbeatService` as hosted service
   - Passes `IGatewayClient` to `TeamsWebhookService`

3. **IHttpClientFactory usage:** Replaced direct `new HttpClient()` instantiation in `TeamsWebhookService` with `IHttpClientFactory`. The package `Microsoft.Extensions.Http` was already in the csproj. Constructor now accepts `IHttpClientFactory` and creates named clients.

4. **Sentinel text in adaptive cards:** `BuildAdaptiveCard()` now accepts optional `questionId` parameter. When provided, adds a subtle footer: `{ "type": "TextBlock", "text": "⚡ {questionId}", "size": "Small", "isSubtle": true }`. This enables Gateway's Playwright poller to find which card corresponds to which question.

5. **Shared secret authentication:** Generated random secret (GUID) on startup. Passed in registration payload via `IGatewayClient.RegisterInstanceAsync(..., secret)`. `TeamsReplyListener` validates secret on incoming reply callbacks and returns 401 if invalid.

6. **Heartbeat support:** Created `HeartbeatService` (BackgroundService) that POSTs to `/gateway/heartbeat` every 30 seconds with `instanceId` and `secret`. Added `HeartbeatAsync()` method to `IGatewayClient` and `GatewayClient`.

**Files Modified:**
- `VoiceMCP/Services/TeamsWebhookService.cs` — Blocking AskQuestionAsync, IHttpClientFactory, sentinel text
- `VoiceMCP/Services/GatewayClient.cs` — HeartbeatAsync method, secret in registration
- `VoiceMCP/Services/IGatewayClient.cs` — HeartbeatAsync signature, secret parameter
- `VoiceMCP/Services/GatewayRegistrationService.cs` — Secret parameter, passes to RegisterInstanceAsync
- `VoiceMCP/Services/TeamsReplyListener.cs` — Secret validation on callbacks
- `VoiceMCP/Program.cs` — Hosted services registration, IHttpClientFactory, secret generation

**Files Created:**
- `VoiceMCP/Services/HeartbeatService.cs` — BackgroundService for Gateway heartbeat

**Why:** These were critical P0 fixes identified in the design review (keaton-gateway-architecture.md). Without these:
- `AskQuestionAsync` returns immediately with no wait mechanism (breaks two-way flow)
- Hosted services are defined but never started (Gateway never receives registration)
- Direct `HttpClient` instantiation causes socket exhaustion in long-running processes
- Playwright cannot identify which card corresponds to which question
- No authentication on callbacks (local process spoofing risk)
- Gateway has no way to detect dead CLI sessions (orphaned registrations accumulate)

**Testing:** Build succeeded with no C# compilation errors. All changes compile cleanly.
