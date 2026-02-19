### 2025-01-XX: Error handling returns strings instead of structured types

**By:** Keaton

**What:** Tools return error messages as strings (e.g., "ERROR: Could not understand response after 4 attempts"). Exceptions in ListenAsync are caught and return empty strings. TeamsTools returns success/failure messages as strings.

**Why:** String-based error handling makes it difficult for MCP clients to programmatically distinguish between:
- User said "ERROR: something" (actual user input)
- System failure (recognition timeout, API error, network issue)
- Partial success (message posted to Teams but user didn't respond)

**Recommendation:** Consider structured error responses via MCP error codes or a consistent JSON error format. At minimum, document the error string conventions so clients can parse them reliably. For critical failures (API credentials invalid, service unreachable), consider throwing exceptions to fail fast rather than returning error strings.
