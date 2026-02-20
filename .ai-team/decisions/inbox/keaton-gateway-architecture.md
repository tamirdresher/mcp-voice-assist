# Decision: Teams Gateway Architecture

### 2026-02-20: Teams Gateway Architecture — Communication Protocol
**By:** Keaton (with input from Fenster, McManus)  
**Status:** Approved  

**What:** Gateway ↔ CLI communication will use **HTTP REST** for v1. Gateway exposes REST API (register, heartbeat, questions, health); CLI sessions expose callback endpoints. Future v2 may migrate to WebSocket for push-based reply delivery.

**Why:** HTTP REST is already implemented in `GatewayClient`, requires no additional dependencies, debuggable with standard tooling (curl, Fiddler), and works cross-process. WebSocket would eliminate callback port allocation problem but adds complexity; defer until usage patterns justify it.

---

### 2026-02-20: Teams Gateway Architecture — Playwright Isolation
**By:** Keaton (with input from Fenster, McManus)  
**Status:** Approved  

**What:** Gateway will use a **dedicated Playwright browser profile** at `%LOCALAPPDATA%\VoiceMCP\playwright-profile-{port}`, launched via `LaunchPersistentContextAsync` with `msedge` channel. Never shared with CLI sessions or user's normal browser.

**Why:** Prevents profile lock collisions between gateway and other Playwright instances. Using `msedge` avoids downloading Chromium separately. Persistent context preserves Teams auth cookies, reducing re-authentication prompts.

---

### 2026-02-20: Teams Gateway Architecture — Session ID Format
**By:** Keaton (with input from Fenster, McManus)  
**Status:** Approved  

**What:** Change session ID generation from random GUID to **deterministic hash**: `SHA256(project:machine:pid)[..8]`. Format remains `q-{8-hex}-{seq}`.

**Why:** Improves debuggability (can reconstruct which session generated a question), reduces birthday-problem collision risk at scale, enables better observability in production. Current random GUID approach orphans questions on CLI restart.

---

### 2026-02-20: Teams Gateway Architecture — Adaptive Card Session Embedding
**By:** Keaton (with input from Fenster, McManus)  
**Status:** Approved  

**What:** Embed session ID in adaptive card as a **sentinel text pattern**: `⚡ {questionId}` in a small, subtle `TextBlock` footer. Playwright extracts via regex: `text=/⚡ q-[a-f0-9]{8}-\\d{3}/`.

**Why:** More resilient than parsing title text. Incoming Webhooks strip certain card properties (like `id` on body elements) in some tenant configs. Visible text approach works regardless of Teams rendering quirks, with minimal visual footprint.

---

### 2026-02-20: Teams Gateway Architecture — Process Management
**By:** Keaton (with input from Fenster, McManus)  
**Status:** Approved  

**What:** Gateway runs as **single-instance process** enforced via named mutex `Global\VoiceMCP-Gateway`. Implemented as console app (dev) / Windows Service (prod). CLI checks `/gateway/health` endpoint and launches gateway if not running. PID written to `%LOCALAPPDATA%\VoiceMCP\gateway.pid`.

**Why:** Prevents multiple gateway instances from conflicting on browser profile or polling same channel. Mutex provides OS-level enforcement. Auto-launch from CLI simplifies user experience (no manual setup required).

---

### 2026-02-20: Teams Gateway Architecture — Polling Strategy
**By:** Keaton (with input from Fenster, McManus)  
**Status:** Approved  

**What:** Gateway polls Teams channel with **adaptive interval**: 5s when questions pending, 30s when idle, exponential backoff on errors (max 5 min). Scans last 20 messages per cycle (scroll-back) to catch posts that scrolled out of viewport.

**Why:** Balances responsiveness (users expect reply within ~10s) with resource usage. Scroll-back mitigates race condition where webhook post completes but Playwright hasn't refreshed yet. Backoff prevents log spam on transient Teams outages.

---

### 2026-02-20: Teams Gateway Architecture — Security Model
**By:** Keaton (with input from Fenster, McManus)  
**Status:** Approved  

**What:** CLI generates shared secret on registration, gateway validates it on every callback POST. Secret sent in request body (not headers) for simplicity. No TLS required (localhost-only communication).

**Why:** Prevents local process spoofing. Any process can bind to localhost, but only the registered CLI session knows the secret. TLS overhead not justified for localhost IPC. Future: consider named pipes for OS-level access control.

---

### 2026-02-20: Teams Gateway Architecture — Heartbeat & Session Eviction
**By:** Keaton (with input from Fenster, McManus)  
**Status:** Approved  

**What:** CLI must POST `/gateway/heartbeat` every 30s with `instanceId` and `secret`. Gateway evicts sessions after **3× missed heartbeats (90s)**. Gateway returns `heartbeatIntervalSec` in registration response.

**Why:** Gateway has no other way to distinguish live vs dead CLI sessions (crashed, killed, debugger attached). 90s timeout balances responsiveness (clean up dead sessions quickly) with tolerance for transient network hiccups or CPU contention.

---

### 2026-02-20: Teams Gateway Architecture — Critical Code Fixes
**By:** Keaton (with input from Fenster, McManus)  
**Status:** Approved  

**What:** Three P0 fixes before gateway build:
1. **`AskQuestionAsync` must block**: Register `TaskCompletionSource` in `_pendingQuestions`, await with timeout. Gateway triggers completion via callback.
2. **Register hosted services**: Add `builder.Services.AddHostedService<GatewayRegistrationService>()` and `AddHostedService<TeamsReplyListener>()` in `Program.cs`.
3. **Use `IHttpClientFactory`**: Replace `new HttpClient()` in `TeamsWebhookService` to prevent socket exhaustion.

**Why:** Current code has structural bugs preventing two-way flow. `AskQuestionAsync` returns immediately with no wait mechanism. Hosted services are defined but never started. Direct `HttpClient` instantiation leaks sockets in long-running gateway process.

---

### 2026-02-20: Teams Gateway Architecture — API Contracts
**By:** Keaton (with input from Fenster, McManus)  
**Status:** Approved  

**What:** Gateway exposes:
- `POST /gateway/register` → { instanceId, callbackUrl, projectName, secret } ← { ok, heartbeatIntervalSec }
- `POST /gateway/heartbeat` → { instanceId, secret } ← { ok }
- `POST /gateway/questions` → { questionId, instanceId } ← { ok, estimatedPollIntervalMs }
- `DELETE /gateway/instances/{id}` + Header: X-Gateway-Secret
- `GET /gateway/health` ← { status, activeInstances, pendingQuestions, playwrightStatus, uptimeSec }

CLI exposes:
- `POST /voice-mcp/reply` → { questionId, answer, userId, userName, timestamp, isAudioTranscript, secret } ← { status: "ok" | "unknown_question" }

**Why:** Defines clear contracts for integration testing. `unknown_question` response tells gateway to stop retrying delivery. Health endpoint enables ops monitoring. Timestamp field enables future reply ordering/deduplication.

---

### 2026-02-20: Teams Gateway Architecture — Known Risks & Accepted Limitations
**By:** Keaton (with input from Fenster, McManus)  
**Status:** Approved  

**What:** V1 accepts:
- No persistent queue: if CLI crashes after posting question, reply is lost.
- No Teams message ID: must rely on visual scanning (webhook response is just `"1"`).
- Single browser bottleneck: one gateway = one polling loop.
- No edited-message tracking: if user edits reply, re-delivery may be inconsistent.
- Teams DOM instability: selectors may break on Teams updates.

**Why:** These are architectural constraints of the Teams Incoming Webhook API (no message handle) and Teams Web (no stable DOM contract). Mitigations in place: scroll-back scanning, selector health checks, immediate poll on registration. Persistent queue and multi-browser pooling deferred to v2 based on real-world usage patterns.
