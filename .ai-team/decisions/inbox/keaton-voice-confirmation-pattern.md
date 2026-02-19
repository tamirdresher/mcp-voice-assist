### 2025-01-XX: Voice tools use nested confirmation loops with hardcoded retry limits

**By:** Keaton

**What:** AskUser and AskForApproval both implement a nested retry pattern: outer loop for the question (MAX_RETRIES=4), inner loop for confirmation (MAX_RETRIES=4). Total possible interactions: up to 16 attempts (4 questions × 4 confirmations).

**Why:** This pattern ensures accuracy but creates complex flow control and duplicated logic across both tools. The nested loops make it difficult to reason about failure cases and UX. Consider extracting a shared confirmation service with configurable retry policies.

**Risk:** High retry counts may frustrate users. Error handling returns strings like "ERROR: Could not understand response after 4 attempts" rather than structured error types, making it hard for MCP clients to distinguish failure modes programmatically.
