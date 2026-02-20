# Decision: Gateway Playwright Polling Implementation

### 2026-02-20: Gateway Playwright Polling — Implementation Complete
**By:** McManus  
**Status:** Implemented  

**What:** Upgraded VoiceMCP.Gateway from Teams Outgoing Webhook approach to Playwright headless browser polling. The Outgoing Webhook approach was blocked by DLP policies and is no longer viable.

**Implementation Details:**

1. **Added Microsoft.Playwright NuGet package** (v1.49.0) to VoiceMCP.Gateway.csproj

2. **Created TeamsPlaywrightPoller BackgroundService** (Services/TeamsPlaywrightPoller.cs):
   - Launches persistent browser context with MSEdge channel in headless mode
   - Uses dedicated profile at `%LOCALAPPDATA%\VoiceMCP\playwright-profile-{port}` to avoid conflicts
   - Navigates to Teams web (`https://teams.cloud.microsoft/`) and wizard channel (ID: `19:6gjjSHAUPHJlqyxeJemN9giR8HYZkWGpvsznRDSyagE1@thread.tacv2`)
   - Polling loop with adaptive intervals:
     - Active mode: 5s when pending questions exist
     - Idle mode: 30s when no pending questions
     - Exponential backoff on errors (5s, 10s, 20s, 40s... max 5 min)
   - Scans last 20 message groups per cycle (scroll-back) to catch posts that scrolled out of viewport
   - Searches for sentinel pattern `⚡ q-{hex}-{seq}` in message text
   - Checks for replies via `[aria-label*="reply"]` containers with `\d+ repl` regex
   - Extracts reply text from `p` elements or audio transcripts via `[data-testid="transcript-button"]` → `[data-testid="transcript-paragraph"]`
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
   - Removed deprecated `/gateway/webhook` endpoint (Outgoing Webhook approach)
   - Added `InstanceRegistration.Secret` field
   - Added `HeartbeatPayload` record for heartbeat validation
   - Mutex acquisition on startup with graceful error handling
   - Proper shutdown with mutex disposal in finally block

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
