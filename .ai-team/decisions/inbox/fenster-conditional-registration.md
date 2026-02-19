# Decision: Conditional Teams Tool Registration

**Author:** Fenster  
**Date:** 2025-07-22  
**Status:** Implemented

## Context
Teams tools should only appear in the MCP tool list when `TEAMS_WEBHOOK_URL` is configured. Without the webhook URL, the tools are useless and would confuse the agent.

## Decision
- Keep `WithToolsFromAssembly()` for voice tools (AskUserTool has `[McpServerToolType]`).
- McManus's `TeamsTools` class intentionally omits `[McpServerToolType]`, so assembly scanning ignores it.
- When `TEAMS_WEBHOOK_URL` is set, Program.cs explicitly calls `mcpBuilder.WithTools<TeamsTools>()` to register it.
- These two registration methods compose cleanly — the SDK merges both sets of tools.

## Rationale
This avoids modifying the existing voice tool registration and keeps the conditional logic isolated to a single `if` block in Program.cs. No new packages were added — `HttpClient` is created directly since the service is a process-lifetime singleton.

## Impact
- **McManus:** TeamsTools must NOT have `[McpServerToolType]`. This is already the case.
- **Hockney:** Integration tests should verify that Teams tools appear/disappear based on the env var.
