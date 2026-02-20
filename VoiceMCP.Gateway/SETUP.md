# VoiceMCP Gateway — Setup Guide

## Prerequisites

- **.NET 10 SDK** — [download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Microsoft Edge** (system install) — Gateway uses Edge's user profile for Teams auth
- **Playwright browsers** — install after building:
  ```powershell
  pwsh bin/Debug/net10.0/playwright.ps1 install chromium
  ```

## First-Time Setup

1. **Build the Gateway:**
   ```powershell
   dotnet build VoiceMCP.Gateway
   ```

2. **Close Edge completely** — Gateway needs exclusive access to Edge's user data directory (`%LOCALAPPDATA%\Microsoft\Edge\User Data`). Edge and Gateway cannot use the profile simultaneously.

3. **Run in headed mode** (required for first login to capture auth cookies):
   ```powershell
   $env:GATEWAY_HEADLESS="false"; dotnet run --project VoiceMCP.Gateway
   ```

4. **Teams loads in the Edge window** with your work profile cookies. Wait for Playwright to connect and navigate to the Teams wizard channel.

5. **Verify the Gateway is healthy:**
   ```powershell
   curl http://localhost:8080/gateway/health
   ```
   Response should include `"playwrightStatus": "connected"`.

6. **Stop the Gateway** (Ctrl+C), then reopen Edge normally.

After the first headed run, subsequent starts can use headless mode (the default).

## Auto-Start on Boot

### Option A: Automated install

Run as Administrator:
```powershell
.\scripts\install-gateway-startup.ps1
```

This creates a Windows Task Scheduler task ("VoiceMCP-Gateway") that runs `start-gateway.ps1` at user logon.

### Option B: Manual Task Scheduler

1. Open Task Scheduler → Create Task
2. **Name:** VoiceMCP-Gateway
3. **Trigger:** At log on (your user)
4. **Action:** Start a program
   - Program: `pwsh.exe`
   - Arguments: `-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "C:\path\to\scripts\start-gateway.ps1"`
5. **Conditions:** Uncheck "Start only if on AC power"
6. **Settings:** Allow task to run on demand

## Using with Copilot CLI

Add the Gateway URL to your MCP configuration at `~/.copilot/mcp-config.json`:

```json
{
  "mcpServers": {
    "voicemcp": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/VoiceMCP/VoiceMCP.csproj"],
      "env": {
        "VOICEMCP_GATEWAY_URL": "http://localhost:8080"
      }
    }
  }
}
```

When configured, the VoiceMCP MCP server will:
- Auto-register with the Gateway on startup
- Send heartbeats every 30 seconds
- Block on `AskUserViaTeams` until you reply in Teams

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `GATEWAY_PORT` | `8080` | HTTP API port |
| `GATEWAY_HEADLESS` | `true` | Set `false` for first login to capture auth cookies |
| `GATEWAY_BROWSER_CHANNEL` | `msedge` | Browser channel (Playwright) |
| `GATEWAY_BROWSER_PROFILE` | Edge default profile | Custom browser profile path |

## Architecture

```
CLI Session 1 ──┐
CLI Session 2 ──┤──→ Gateway (port 8080) ──→ Playwright (Edge) ──→ Teams
CLI Session N ──┘         ↑                        ↓
                    HTTP REST API            Polls wizard channel
                    (register,              for thread replies
                     heartbeat,
                     questions)
```

The Gateway solves the multi-instance routing problem:
- Multiple VoiceMCP instances post questions to Teams via the Gateway
- Each question has a correlation ID (`q-<instanceId>-<seq>`)
- Gateway polls the Teams channel via Playwright and routes replies back to the correct instance

**Edge profile constraint:** The Gateway uses Edge's default user data directory for Teams authentication. Edge must be closed when Gateway starts. After Gateway launches, you can reopen Edge, but the profile is shared — only one process can use it at a time.

## Troubleshooting

| Problem | Solution |
|---------|----------|
| **Edge profile lock** | Close Edge completely before starting Gateway |
| **Auth expired** | Restart Gateway with `$env:GATEWAY_HEADLESS="false"` to re-authenticate |
| **Multiple instances** | Gateway uses a mutex — only one can run. Check `Get-Process dotnet` |
| **Gateway not responding** | `curl http://localhost:8080/gateway/health` — check `playwrightStatus` |
| **Playwright install missing** | Run `pwsh bin/Debug/net10.0/playwright.ps1 install chromium` |
| **Startup task not working** | Check `%LOCALAPPDATA%\VoiceMCP\gateway.log` for errors |
