# Decisions

Team decisions are recorded here. Append-only — never edit past entries.

---

## 2026-02-20

### User Directive: Always Monitor Teams
**By:** Tamir Dresher (via Copilot)

Always keep monitoring Teams for responses. Never go idle. After every action, check Teams for new messages or thread replies.

---

### User Directive: Notify via Teams
**By:** Tamir Dresher (via Copilot)

When you need Tamir's input or when you're done with a task, ping him in Teams via the voicemcp-notify_via_teams MCP tool.

---

### Conditional Teams Tool Registration
**Author:** Fenster  
**Status:** Implemented

Teams tools should only appear in the MCP tool list when `TEAMS_WEBHOOK_URL` is configured. Without the webhook URL, the tools are useless and would confuse the agent.

**Decision:**
- Keep `WithToolsFromAssembly()` for voice tools (AskUserTool has `[McpServerToolType]`).
- McManus's `TeamsTools` class intentionally omits `[McpServerToolType]`, so assembly scanning ignores it.
- When `TEAMS_WEBHOOK_URL` is set, Program.cs explicitly calls `mcpBuilder.WithTools<TeamsTools>()` to register it.
- These two registration methods compose cleanly — the SDK merges both sets of tools.

**Rationale:** Avoids modifying the existing voice tool registration and keeps the conditional logic isolated to a single `if` block in Program.cs.

**Impact:** 
- McManus: TeamsTools must NOT have `[McpServerToolType]`. This is already the case.
- Hockney: Integration tests should verify that Teams tools appear/disappear based on the env var.

---

### Gateway Integration P0 Fixes
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

**Why:** These were critical P0 fixes identified in the design review. Without these:
- `AskQuestionAsync` returns immediately with no wait mechanism (breaks two-way flow)
- Hosted services are defined but never started (Gateway never receives registration)
- Direct `HttpClient` instantiation causes socket exhaustion in long-running processes
- Playwright cannot identify which card corresponds to which question
- No authentication on callbacks (local process spoofing risk)
- Gateway has no way to detect dead CLI sessions (orphaned registrations accumulate)

**Testing:** Build succeeded with no C# compilation errors. All changes compile cleanly.

---

### Two-Way Teams Communication Implementation
**Author:** Fenster  
**Status:** Implemented

Implemented Gateway-based two-way Teams communication for VoiceMCP instances. AskUserViaTeams now blocks and returns actual user replies when Gateway is available, with graceful degradation to one-way notification mode when Gateway is unavailable.

**Components Created:**
1. **IGatewayClient / GatewayClient** — HTTP client for Gateway communication (register instance, register question, unregister)
2. **TeamsReplyListener** — IHostedService that runs HttpListener on auto-allocated port (8090-8190) to receive reply callbacks
3. **GatewayRegistrationService** — IHostedService that registers/unregisters instance with Gateway on startup/shutdown
4. **TeamsWebhookService updates** — Added blocking AskQuestionAsync with TaskCompletionSource-based reply handling and timeout

**Architecture Decisions:**
- **Graceful degradation everywhere:** All Gateway interactions wrapped in try/catch. If Gateway unreachable, log warning and continue. One-way Teams notifications always work.
- **Port allocation strategy:** Simple loop from 8090-8190 testing HttpListener start/stop. More reliable than socket checks on Windows.
- **Instance identity:** 8-char GUID generated at TeamsWebhookService construction, exposed via InstanceId property for Gateway registration.
- **Question correlation:** Format `q-{instanceId}-{seq}` where seq is an Interlocked.Increment counter.
- **Blocking mechanism:** ConcurrentDictionary maps questionId → TaskCompletionSource<string>. OnReplyReceived resolves TCS. Timeout via Task.WhenAny.

**Dependencies:**
- Gateway must implement endpoints per Keaton's architecture spec:
  - `POST /gateway/register` — Register instance
  - `POST /gateway/questions` — Register question
  - `DELETE /gateway/instances/{id}` — Unregister instance
- Gateway must call back to `POST /voice-mcp/reply` with `{questionId, answer, userId, userName}`

**Environment Variables:**
- `VOICEMCP_GATEWAY_URL` — Gateway base URL (default: `http://localhost:8080`)
- `TEAMS_WEBHOOK_URL` — Teams Incoming Webhook (required for Teams integration)

**Testing Notes:**
- Build succeeds with no compilation errors (warnings are file locking from running process)
- Requires actual Gateway implementation to test two-way flow
- One-way flow tested and working (existing functionality preserved)

---

### Test Project Structure & Patterns
**Author:** Hockney (Tester)  
**Status:** Implemented

Established `VoiceMCP.Tests` as the single test project for the solution, using xUnit + Moq.

**Test Organization:**
- One test class per production class: `TeamsWebhookServiceTests`, `TeamsToolsTests`, `ConditionalRegistrationTests`
- Tests grouped by `#region` blocks within each class (by method/concern)
- Naming convention: `MethodName_Condition_ExpectedResult`

**Mocking Patterns:**
- **HttpClient:** Mock `HttpMessageHandler` via `Moq.Protected()` — never mock `HttpClient` directly
- **Service interfaces:** Mock via `Moq` (standard interface mocking)
- **Captured request bodies:** Use `.Callback<HttpRequestMessage, CancellationToken>()` to capture and assert on HTTP payloads

**What's Covered:**
- Service-level HTTP interactions (adaptive card structure, error handling, network failures)
- Tool-level delegation and error wrapping
- Type-level contract verification (interface implementation, constructor signatures, attribute presence)

**What's NOT Covered (yet):**
- Program.cs conditional registration (needs Fenster's implementation or a testable extraction)
- VoiceTools / AskUserTool (existing code — not in scope for this task, but should be next)
- Integration tests against real Teams webhooks

**Packages & Versions:**
- xUnit 2.9.3, xunit.runner.visualstudio 3.0.2, Microsoft.NET.Test.Sdk 17.13.0, Moq 4.20.72
- Target: net10.0 with RollForward=Major

---

### Two-Way Teams Communication Test Strategy
**Status:** Implemented  
**Author:** Hockney  

Testing the two-way Teams communication system being implemented by McManus and Fenster.

**Test Coverage:** Created 59 tests across 4 new test files:

1. **GatewayClientTests (11 tests)** — Tests the client that registers instances/questions with the Gateway:
   - Registration endpoints (instance, question, unregister)
   - Correct payload serialization
   - Error handling (Gateway down, network failures)
   - Graceful degradation on shutdown

2. **TeamsReplyListenerTests (8 tests)** — Tests the HTTP listener that receives callbacks from Gateway:
   - Port allocation (8090-8190 range)
   - Reply forwarding to service
   - Payload validation (malformed JSON, missing fields)
   - Quick response times (< 5s Teams requirement)
   - Optional user fields handling

3. **TeamsWebhookServiceReplyTests (15 tests)** — Tests the updated webhook service with blocking ask:
   - Correlation ID generation (q-{instanceId}-{seq})
   - Gateway registration before posting card
   - Blocking until reply or timeout
   - Multiple concurrent questions handled independently
   - Reply routing to correct pending question
   - Timeout handling (default 120s)
   - Sequence number incrementing

4. **GatewayIntegrationTests (25 tests)** — Tests the Gateway routing logic:
   - In-memory Gateway implementation for testing
   - Multi-instance registration and routing
   - Correlation ID parsing from Teams @mentions
   - Unknown question ID handling
   - Malformed reply handling
   - Stale instance cleanup
   - Response time verification (< 5s)
   - HMAC validation spec (placeholder for future)

**Testing Approach:**
- Tests written against INTERFACES, not implementation
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

**Key Decisions:**
1. Dynamic typing for pre-implementation testing — tests reference types via `Type.GetType()` and `dynamic`, skip tests if types not yet available
2. In-memory Gateway for integration tests — simulates full routing logic without actual ASP.NET hosting
3. No source file modifications — only created test files in VoiceMCP.Tests/
4. Correlation ID format validation — verifies exact format: `q-{instanceId}-{sequenceNumber}`

---

### Code Review: WindowsVoiceService.cs is Dead Code
**By:** Keaton

**What:** WindowsVoiceService.cs contains only an empty internal class with no implementation. It's never registered in DI or referenced anywhere.

**Why:** This should either be implemented (if there's a plan for native Windows Speech API fallback) or deleted. Keeping dead code in the repo creates confusion and maintenance overhead.

**Recommendation:** Delete WindowsVoiceService.cs unless there's an active plan to implement it.

---

### Code Review: Error Handling Returns Strings Instead of Structured Types
**By:** Keaton

**What:** Tools return error messages as strings (e.g., "ERROR: Could not understand response after 4 attempts"). Exceptions in ListenAsync are caught and return empty strings. TeamsTools returns success/failure messages as strings.

**Why:** String-based error handling makes it difficult for MCP clients to programmatically distinguish between:
- User said "ERROR: something" (actual user input)
- System failure (recognition timeout, API error, network issue)
- Partial success (message posted to Teams but user didn't respond)

**Recommendation:** Consider structured error responses via MCP error codes or a consistent JSON error format. At minimum, document the error string conventions so clients can parse them reliably.

---

### Teams Gateway Architecture
**By:** Keaton (with input from Fenster, McManus)  
**Status:** Approved  

**Communication Protocol:** Gateway ↔ CLI communication will use **HTTP REST** for v1. Gateway exposes REST API (register, heartbeat, questions, health); CLI sessions expose callback endpoints. Future v2 may migrate to WebSocket for push-based reply delivery.

**Why:** HTTP REST is already implemented in `GatewayClient`, requires no additional dependencies, debuggable with standard tooling (curl, Fiddler), and works cross-process. WebSocket would eliminate callback port allocation problem but adds complexity; defer until usage patterns justify it.

---

### Playwright Isolation
**By:** Keaton  
**Status:** Approved  

Gateway will use a **dedicated Playwright browser profile** at `%LOCALAPPDATA%\VoiceMCP\playwright-profile-{port}`, launched via `LaunchPersistentContextAsync` with `msedge` channel. Never shared with CLI sessions or user's normal browser.

**Why:** Prevents profile lock collisions between gateway and other Playwright instances. Using `msedge` avoids downloading Chromium separately. Persistent context preserves Teams auth cookies, reducing re-authentication prompts.

---

### Session ID Format (Deterministic Hash)
**By:** Keaton  
**Status:** Approved  

Change session ID generation from random GUID to **deterministic hash**: `SHA256(project:machine:pid)[..8]`. Format remains `q-{8-hex}-{seq}`.

**Why:** Improves debuggability (can reconstruct which session generated a question), reduces birthday-problem collision risk at scale, enables better observability in production. Current random GUID approach orphans questions on CLI restart.

---

### Adaptive Card Session Embedding
**By:** Keaton  
**Status:** Approved  

Embed session ID in adaptive card as a **sentinel text pattern**: `⚡ {questionId}` in a small, subtle `TextBlock` footer. Playwright extracts via regex: `text=/⚡ q-[a-f0-9]{8}-\\d{3}/`.

**Why:** More resilient than parsing title text. Incoming Webhooks strip certain card properties (like `id` on body elements) in some tenant configs. Visible text approach works regardless of Teams rendering quirks, with minimal visual footprint.

---

### Process Management (Single-Instance Mutex)
**By:** Keaton  
**Status:** Approved  

Gateway runs as **single-instance process** enforced via named mutex `Global\VoiceMCP-Gateway`. Implemented as console app (dev) / Windows Service (prod). CLI checks `/gateway/health` endpoint and launches gateway if not running. PID written to `%LOCALAPPDATA%\VoiceMCP\gateway.pid`.

**Why:** Prevents multiple gateway instances from conflicting on browser profile or polling same channel. Mutex provides OS-level enforcement. Auto-launch from CLI simplifies user experience (no manual setup required).

---

### Polling Strategy (Adaptive Intervals)
**By:** Keaton  
**Status:** Approved  

Gateway polls Teams channel with **adaptive interval**: 5s when questions pending, 30s when idle, exponential backoff on errors (max 5 min). Scans last 20 messages per cycle (scroll-back) to catch posts that scrolled out of viewport.

**Why:** Balances responsiveness (users expect reply within ~10s) with resource usage. Scroll-back mitigates race condition where webhook post completes but Playwright hasn't refreshed yet. Backoff prevents log spam on transient Teams outages.

---

### Security Model (Shared Secret)
**By:** Keaton  
**Status:** Approved  

CLI generates shared secret on registration, gateway validates it on every callback POST. Secret sent in request body (not headers) for simplicity. No TLS required (localhost-only communication).

**Why:** Prevents local process spoofing. Any process can bind to localhost, but only the registered CLI session knows the secret. TLS overhead not justified for localhost IPC. Future: consider named pipes for OS-level access control.

---

### Heartbeat & Session Eviction
**By:** Keaton  
**Status:** Approved  

CLI must POST `/gateway/heartbeat` every 30s with `instanceId` and `secret`. Gateway evicts sessions after **3× missed heartbeats (90s)**. Gateway returns `heartbeatIntervalSec` in registration response.

**Why:** Gateway has no other way to distinguish live vs dead CLI sessions (crashed, killed, debugger attached). 90s timeout balances responsiveness (clean up dead sessions quickly) with tolerance for transient network hiccups or CPU contention.

---

### Critical Code Fixes (P0)
**By:** Keaton  
**Status:** Approved  

Three P0 fixes before gateway build:
1. **`AskQuestionAsync` must block**: Register `TaskCompletionSource` in `_pendingQuestions`, await with timeout. Gateway triggers completion via callback.
2. **Register hosted services**: Add `builder.Services.AddHostedService<GatewayRegistrationService>()` and `AddHostedService<TeamsReplyListener>()` in `Program.cs`.
3. **Use `IHttpClientFactory`**: Replace `new HttpClient()` in `TeamsWebhookService` to prevent socket exhaustion.

**Why:** Current code has structural bugs preventing two-way flow. `AskQuestionAsync` returns immediately with no wait mechanism. Hosted services are defined but never started. Direct `HttpClient` instantiation leaks sockets in long-running gateway process.

---

### API Contracts
**By:** Keaton  
**Status:** Approved  

**Gateway exposes:**
- `POST /gateway/register` → { instanceId, callbackUrl, projectName, secret } ← { ok, heartbeatIntervalSec }
- `POST /gateway/heartbeat` → { instanceId, secret } ← { ok }
- `POST /gateway/questions` → { questionId, instanceId } ← { ok, estimatedPollIntervalMs }
- `DELETE /gateway/instances/{id}` + Header: X-Gateway-Secret
- `GET /gateway/health` ← { status, activeInstances, pendingQuestions, playwrightStatus, uptimeSec }

**CLI exposes:**
- `POST /voice-mcp/reply` → { questionId, answer, userId, userName, timestamp, isAudioTranscript, secret } ← { status: "ok" | "unknown_question" }

**Why:** Defines clear contracts for integration testing. `unknown_question` response tells gateway to stop retrying delivery. Health endpoint enables ops monitoring. Timestamp field enables future reply ordering/deduplication.

---

### Known Risks & Accepted Limitations
**By:** Keaton  
**Status:** Approved  

V1 accepts:
- No persistent queue: if CLI crashes after posting question, reply is lost.
- No Teams message ID: must rely on visual scanning (webhook response is just `"1"`).
- Single browser bottleneck: one gateway = one polling loop.
- No edited-message tracking: if user edits reply, re-delivery may be inconsistent.
- Teams DOM instability: selectors may break on Teams updates.

**Why:** These are architectural constraints of the Teams Incoming Webhook API (no message handle) and Teams Web (no stable DOM contract). Mitigations in place: scroll-back scanning, selector health checks, immediate poll on registration. Persistent queue and multi-browser pooling deferred to v2 based on real-world usage patterns.

---

## 2025-07-18

### HttpClient Lifetime Anti-Pattern
**By:** Keaton

**What:** Program.cs line 35 instantiates TeamsWebhookService with `new HttpClient()`, passing it to the constructor. This HttpClient is stored as an instance field and used for all webhook posts.

**Why:** Creating HttpClient instances directly can lead to socket exhaustion under load. While VoiceMCP likely has low request volume (human interaction pace), this is still an anti-pattern in .NET.

**Recommendation:** Use IHttpClientFactory pattern:
1. Register HttpClient in DI: `builder.Services.AddHttpClient<ITeamsNotificationService, TeamsWebhookService>()`
2. Inject HttpClient into TeamsWebhookService constructor
3. Remove manual HttpClient instantiation from Program.cs

This is low priority for an MCP server (not high-throughput), but it's best practice and makes testing easier.

---

### Credential Logging Risk
**By:** Keaton

**What:** Program.cs logs diagnostic messages like "Loaded Azure OpenAI credentials from environment variables" and "Loaded Azure OpenAI credentials from user secrets/appsettings.json" to stderr. While the keys themselves aren't logged, the messages confirm whether credentials were loaded and from which source.

**Why:** This is relatively low risk but could expose information about the configuration source in logs. More concerning: if someone adds debug logging of the variables themselves, keys would leak to stderr.

**Recommendation:** 
1. Ensure no debug logging accidentally exposes keys (current code is safe)
2. Consider logging credential source at Debug level rather than Error level
3. Add a code comment warning against logging credential values
4. Document that stderr may contain diagnostic information about configuration sources

---

### Voice Tool Confirmation Pattern
**By:** Keaton

**What:** AskUser and AskForApproval both implement a nested retry pattern: outer loop for the question (MAX_RETRIES=4), inner loop for confirmation (MAX_RETRIES=4). Total possible interactions: up to 16 attempts (4 questions × 4 confirmations).

**Why:** This pattern ensures accuracy but creates complex flow control and duplicated logic across both tools. The nested loops make it difficult to reason about failure cases and UX. Consider extracting a shared confirmation service with configurable retry policies.

**Risk:** High retry counts may frustrate users. Error handling returns strings like "ERROR: Could not understand response after 4 attempts" rather than structured error types, making it hard for MCP clients to distinguish failure modes programmatically.

---

## 2025-01-XX

### Two-Way Teams Communication via Outgoing Webhook
**Status:** Proposed (Later replaced by Playwright polling)  
**Author:** Keaton  

Full architecture document (see keaton-teams-reply-architecture.md for complete design with component diagrams, correlation system, HTTP listener, gateway registration protocol, blocking flow, interface changes, error handling, and testing strategy).

**Key points:**
- **Routing Strategy:** Shared Gateway/Router process receives all @mention callbacks from Teams, routes to correct VoiceMCP instance based on correlation ID
- **Instance Identity:** 8-char GUID generated at startup; question IDs use format `q-{instanceId}-{seq}`
- **Port Allocation:** Simple loop from 8090-8190 testing HttpListener start/stop
- **Blocking mechanism:** ConcurrentDictionary maps questionId → TaskCompletionSource<string>
- **Graceful degradation:** All Gateway interactions wrapped in try/catch; one-way Teams notifications always work
- **No reflection hacks:** InstanceId exposed via clean property, not reflected
- **Environment variables:** `VOICEMCP_GATEWAY_URL` (default: `http://localhost:8080`), `TEAMS_WEBHOOK_URL`

---

### VoiceMCP Gateway Implementation Complete
**Status:** Implemented  
**Author:** McManus  

Implemented VoiceMCP.Gateway as a separate .NET 10 console project using ASP.NET Core minimal APIs. The gateway provides centralized routing for two-way Teams communication across multiple VoiceMCP instances.

**Implementation Details:**
- **Location:** `VoiceMCP.Gateway/` (sibling to VoiceMCP)
- **Type:** .NET 10 console app with ASP.NET Core minimal APIs
- **Dependencies:** `FrameworkReference` to `Microsoft.AspNetCore.App`

**Endpoints Implemented:**
1. `POST /gateway/register` — Instance registration
2. `DELETE /gateway/instances/{instanceId}` — Instance unregistration
3. `POST /gateway/questions` — Question registration
4. `POST /gateway/webhook` — Teams Outgoing Webhook callback (HMAC validation, correlation ID parsing, async forwarding)

**Key Technical Decisions:**
- **Port configuration:** Use `app.Urls.Add($"http://0.0.0.0:{port}")` after building the app
- **HMAC validation:** Read request body to compute HMAC, reset `request.Body.Position = 0` after validation
- **Async forwarding:** Gateway responds to Teams within 5 seconds (200 OK), forwards answer asynchronously using `Task.Run()`
- **State management:** ConcurrentDictionary for instances and question mapping; background task cleans stale instances after 5 minutes of no heartbeat

**Running the Gateway:**
```bash
dotnet run --project VoiceMCP.Gateway
```

Environment variables:
- `GATEWAY_PORT` (default: 8080)
- `TEAMS_OUTGOING_WEBHOOK_TOKEN` (optional, enables HMAC validation)

**Next Steps for Integration:**
VoiceMCP instances will need:
1. Instance ID generation at startup
2. HTTP listener for receiving callbacks (`/voice-mcp/reply` endpoint)
3. Registration with gateway before posting questions
4. Updated adaptive card with correlation ID instructions
5. Graceful unregistration on shutdown

---

### Teams Tools Conditional Registration
**By:** McManus

**What:** `TeamsTools` is a plain class with `[McpServerTool]` methods but no `[McpServerToolType]` class attribute. This prevents the assembly scanner from auto-discovering it. Fenster registers it explicitly via `mcpBuilder.WithTools<TeamsTools>()` only when the `TEAMS_WEBHOOK_URL` environment variable is set.

**Why:** Teams integration is optional — if no webhook URL is configured, the MCP server should not expose Teams tools to the agent. Using `[McpServerToolType]` would bypass that conditional logic and always register the tools.

---

### IHttpClientFactory Adoption
**By:** McManus

**What:** Added `Microsoft.Extensions.Http` NuGet package to support `IHttpClientFactory` for Teams webhook HTTP calls.

**Why:** The DI registration in Program.cs uses `builder.Services.AddHttpClient()` and resolves `IHttpClientFactory` to create the `HttpClient` injected into `TeamsWebhookService`. This follows .NET best practices for HTTP client lifecycle management.

---

### Adaptive Card Schema Workaround
**By:** McManus

**What:** The adaptive card JSON payload requires a `$schema` key, but C# anonymous type properties can't start with `$`. The implementation serializes with a `schema` property and does a string replace to `$schema` post-serialization.

**Why:** Keeps the card-building code simple with anonymous objects instead of requiring a separate JSON template or dictionary-based construction.

---

### Gateway Playwright Polling Implementation
**By:** McManus  
**Status:** Implemented  

**What:** Upgraded VoiceMCP.Gateway from Teams Outgoing Webhook approach to Playwright headless browser polling. The Outgoing Webhook approach was blocked by DLP policies and is no longer viable.

**Implementation Details:**

1. **Added Microsoft.Playwright NuGet package** (v1.49.0) to VoiceMCP.Gateway.csproj

2. **Created TeamsPlaywrightPoller BackgroundService** (Services/TeamsPlaywrightPoller.cs):
   - Launches persistent browser context with MSEdge channel in headless mode
   - Uses dedicated profile at `%LOCALAPPDATA%\VoiceMCP\playwright-profile-{port}` to avoid conflicts
   - Navigates to Teams web (`https://teams.cloud.microsoft/`) and wizard channel
   - Polling loop with adaptive intervals: 5s when pending questions, 30s when idle, exponential backoff on errors
   - Scans last 20 message groups per cycle (scroll-back)
   - Searches for sentinel pattern `⚡ q-{hex}-{seq}` in message text
   - Checks for replies via `[aria-label*="reply"]` containers with `\d+ repl` regex
   - Extracts reply text from `p` elements or audio transcripts
   - POSTs reply to CLI callback URL with secret validation
   - Tracks processed question IDs to prevent duplicate delivery

3. **Created HeartbeatCleanupService BackgroundService** (Services/HeartbeatCleanupService.cs):
   - Runs cleanup cycle every 30 seconds
   - Evicts instances after 90s (3× missed heartbeats) timeout
   - Removes associated pending questions on eviction

4. **Created SingleInstanceMutex** (Services/SingleInstanceMutex.cs):
   - Enforces single gateway instance via `Global\VoiceMCP-Gateway` named mutex
   - Uses `MutexAcl.Create()` (not constructor) for .NET 10 compatibility
   - Exits with clear error if another instance is running

5. **Refactored Program.cs**:
   - Proper DI registration for all services
   - Added `POST /gateway/heartbeat` endpoint with `instanceId` and `secret` validation
   - Added `GET /gateway/health` endpoint returning `{ status, activeInstances, pendingQuestions, playwrightStatus, uptimeSec }`
   - Updated `POST /gateway/register` to require `secret` field, returns `{ ok, heartbeatIntervalSec: 30 }`
   - Updated `POST /gateway/questions` to return `{ ok, estimatedPollIntervalMs }`
   - Updated `DELETE /gateway/instances/{id}` to validate secret via `X-Gateway-Secret` header
   - Removed deprecated `/gateway/webhook` endpoint
   - Added `InstanceRegistration.Secret` field
   - Added `HeartbeatPayload` record for heartbeat validation
   - Mutex acquisition on startup with graceful error handling

**Why:** Teams Outgoing Webhooks are blocked by DLP policies in the target tenant. Playwright polling is the only viable approach for two-way Teams communication. This implementation provides:
- Resilient polling with adaptive intervals (balances responsiveness vs resource usage)
- Auth wall detection (logs clear error when session expires)
- Scroll-back scanning (catches posts that scrolled out of viewport)
- Audio transcript support (clicks transcript button, reads text)
- Singleton enforcement (prevents profile lock collisions)
- Shared secret validation (prevents local process spoofing)
- Health monitoring (ops observability via /gateway/health)

**Known Limitations Accepted:**
- No persistent queue (if CLI crashes after posting question, reply is lost)
- Single browser bottleneck (one gateway = one polling loop)
- Teams DOM instability (selectors may break on Teams updates)
- Requires manual Teams login on first run (auth cookies persist in profile afterward)

**Next Steps:**
- CLI must be updated to generate and send `secret` field on registration
- CLI must implement `POST /voice-mcp/reply` callback endpoint with secret validation
- CLI must send heartbeats every 30s via `POST /gateway/heartbeat`
- Teams adaptive card must embed sentinel `⚡ q-{hex}-{seq}` in footer TextBlock
- Install Playwright browsers: `pwsh bin/Debug/net10.0/playwright.ps1 install msedge`
