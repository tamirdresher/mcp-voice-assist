<#
.SYNOPSIS
    Starts the VoiceMCP Gateway if not already running.
    Suitable for Windows Task Scheduler (run at logon).

.DESCRIPTION
    - Checks if Gateway is already running via health endpoint
    - Kills Edge processes (Gateway needs exclusive Edge profile access)
    - Starts Gateway in headed mode if no auth cookies exist, headless otherwise
    - Logs output to %LOCALAPPDATA%\VoiceMCP\gateway.log
#>

$ErrorActionPreference = "Stop"

$HealthUrl = "http://localhost:8080/gateway/health"
$LogDir = Join-Path $env:LOCALAPPDATA "VoiceMCP"
$LogFile = Join-Path $LogDir "gateway.log"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$GatewayProject = Join-Path $RepoRoot "VoiceMCP.Gateway"

# Ensure log directory exists
if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
}

function Write-Log {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "$timestamp $Message" | Tee-Object -FilePath $LogFile -Append
}

# Check if Gateway is already running
try {
    $response = Invoke-RestMethod -Uri $HealthUrl -TimeoutSec 3 -ErrorAction Stop
    if ($response.status -eq "ok") {
        Write-Log "[start-gateway] Gateway already running (uptime: $($response.uptimeSec)s, instances: $($response.activeInstances))"
        exit 0
    }
}
catch {
    Write-Log "[start-gateway] Gateway not running, starting..."
}

# Determine headed vs headless mode
# First time (no Edge cookies/profile data) requires headed mode for auth
$EdgeCookiesPath = Join-Path $env:LOCALAPPDATA "Microsoft\Edge\User Data\Default\Cookies"
if (Test-Path $EdgeCookiesPath) {
    $env:GATEWAY_HEADLESS = if ($env:GATEWAY_HEADLESS) { $env:GATEWAY_HEADLESS } else { "true" }
    Write-Log "[start-gateway] Edge profile found, using GATEWAY_HEADLESS=$($env:GATEWAY_HEADLESS)"
}
else {
    $env:GATEWAY_HEADLESS = "false"
    Write-Log "[start-gateway] No Edge profile found, starting in headed mode for first-time auth"
}

# Kill Edge processes — Gateway needs exclusive access to the Edge user data dir
$edgeProcesses = Get-Process -Name "msedge" -ErrorAction SilentlyContinue
if ($edgeProcesses) {
    Write-Log "[start-gateway] Closing Edge ($($edgeProcesses.Count) processes)..."
    $edgeProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

# Start the Gateway
Write-Log "[start-gateway] Starting Gateway (project: $GatewayProject, headless: $($env:GATEWAY_HEADLESS))"
try {
    $process = Start-Process -FilePath "dotnet" `
        -ArgumentList "run", "--project", $GatewayProject `
        -RedirectStandardError $LogFile `
        -WindowStyle Hidden `
        -PassThru

    Write-Log "[start-gateway] Gateway started (PID: $($process.Id))"

    # Wait a few seconds and verify it's healthy
    Start-Sleep -Seconds 10
    try {
        $response = Invoke-RestMethod -Uri $HealthUrl -TimeoutSec 5 -ErrorAction Stop
        Write-Log "[start-gateway] Gateway healthy: status=$($response.status), playwright=$($response.playwrightStatus)"
    }
    catch {
        Write-Log "[start-gateway] WARNING: Gateway started but health check failed (may still be initializing)"
    }
}
catch {
    Write-Log "[start-gateway] ERROR: Failed to start Gateway: $_"
    exit 1
}
