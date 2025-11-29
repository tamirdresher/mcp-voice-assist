# Setup script for Azure OpenAI environment variables
# This script helps configure VoiceBridgeMCP for use with MCP clients

param(
    [Parameter(Mandatory=$true, HelpMessage="Azure OpenAI endpoint (e.g., https://your-resource.openai.azure.com/)")]
    [string]$Endpoint,
    
    [Parameter(Mandatory=$true, HelpMessage="Azure OpenAI API key")]
    [string]$ApiKey,
    
    [Parameter(HelpMessage="TTS deployment name (default: tts)")]
    [string]$TtsDeployment = "tts",
    
    [Parameter(HelpMessage="Whisper deployment name (default: whisper)")]
    [string]$WhisperDeployment = "whisper",
    
    [Parameter(HelpMessage="Set environment variables for current user (persistent)")]
    [switch]$SetUser,
    
    [Parameter(HelpMessage="Set environment variables for system (requires admin)")]
    [switch]$SetSystem
)

Write-Host "=== VoiceBridgeMCP Azure OpenAI Configuration ===" -ForegroundColor Cyan
Write-Host ""

# Validate endpoint format
if (-not $Endpoint.StartsWith("https://") -or -not $Endpoint.EndsWith("/")) {
    Write-Host "Warning: Endpoint should start with 'https://' and end with '/'" -ForegroundColor Yellow
    $Endpoint = $Endpoint.TrimEnd('/') + '/'
    if (-not $Endpoint.StartsWith("https://")) {
        $Endpoint = "https://" + $Endpoint
    }
    Write-Host "Corrected endpoint to: $Endpoint" -ForegroundColor Yellow
}

# Display configuration
Write-Host "Configuration:" -ForegroundColor Green
Write-Host "  AZURE_OPENAI_ENDPOINT: $Endpoint"
Write-Host "  AZURE_OPENAI_API_KEY: $('*' * 8)$(($ApiKey.Substring([Math]::Max(0, $ApiKey.Length - 4))))"
Write-Host "  AZURE_OPENAI_TTS_DEPLOYMENT: $TtsDeployment"
Write-Host "  AZURE_OPENAI_WHISPER_DEPLOYMENT: $WhisperDeployment"
Write-Host ""

# Set environment variables for current session
Write-Host "Setting environment variables for current PowerShell session..." -ForegroundColor Cyan
$env:AZURE_OPENAI_ENDPOINT = $Endpoint
$env:AZURE_OPENAI_API_KEY = $ApiKey
$env:AZURE_OPENAI_TTS_DEPLOYMENT = $TtsDeployment
$env:AZURE_OPENAI_WHISPER_DEPLOYMENT = $WhisperDeployment
Write-Host "✓ Current session configured" -ForegroundColor Green
Write-Host ""

# Set persistent environment variables if requested
if ($SetUser) {
    Write-Host "Setting environment variables for current user (persistent)..." -ForegroundColor Cyan
    [Environment]::SetEnvironmentVariable("AZURE_OPENAI_ENDPOINT", $Endpoint, "User")
    [Environment]::SetEnvironmentVariable("AZURE_OPENAI_API_KEY", $ApiKey, "User")
    [Environment]::SetEnvironmentVariable("AZURE_OPENAI_TTS_DEPLOYMENT", $TtsDeployment, "User")
    [Environment]::SetEnvironmentVariable("AZURE_OPENAI_WHISPER_DEPLOYMENT", $WhisperDeployment, "User")
    Write-Host "✓ User environment variables set (will persist across sessions)" -ForegroundColor Green
    Write-Host ""
}

if ($SetSystem) {
    Write-Host "Setting environment variables for system (persistent)..." -ForegroundColor Cyan
    try {
        [Environment]::SetEnvironmentVariable("AZURE_OPENAI_ENDPOINT", $Endpoint, "Machine")
        [Environment]::SetEnvironmentVariable("AZURE_OPENAI_API_KEY", $ApiKey, "Machine")
        [Environment]::SetEnvironmentVariable("AZURE_OPENAI_TTS_DEPLOYMENT", $TtsDeployment, "Machine")
        [Environment]::SetEnvironmentVariable("AZURE_OPENAI_WHISPER_DEPLOYMENT", $WhisperDeployment, "Machine")
        Write-Host "✓ System environment variables set (requires admin privileges)" -ForegroundColor Green
    }
    catch {
        Write-Host "✗ Failed to set system environment variables. Run PowerShell as Administrator." -ForegroundColor Red
    }
    Write-Host ""
}

# Test the configuration
Write-Host "Testing configuration..." -ForegroundColor Cyan
$testPassed = $true

if ([string]::IsNullOrWhiteSpace($env:AZURE_OPENAI_ENDPOINT)) {
    Write-Host "✗ AZURE_OPENAI_ENDPOINT is not set" -ForegroundColor Red
    $testPassed = $false
} else {
    Write-Host "✓ AZURE_OPENAI_ENDPOINT is set" -ForegroundColor Green
}

if ([string]::IsNullOrWhiteSpace($env:AZURE_OPENAI_API_KEY)) {
    Write-Host "✗ AZURE_OPENAI_API_KEY is not set" -ForegroundColor Red
    $testPassed = $false
} else {
    Write-Host "✓ AZURE_OPENAI_API_KEY is set" -ForegroundColor Green
}

if ([string]::IsNullOrWhiteSpace($env:AZURE_OPENAI_TTS_DEPLOYMENT)) {
    Write-Host "✗ AZURE_OPENAI_TTS_DEPLOYMENT is not set" -ForegroundColor Red
    $testPassed = $false
} else {
    Write-Host "✓ AZURE_OPENAI_TTS_DEPLOYMENT is set" -ForegroundColor Green
}

if ([string]::IsNullOrWhiteSpace($env:AZURE_OPENAI_WHISPER_DEPLOYMENT)) {
    Write-Host "✗ AZURE_OPENAI_WHISPER_DEPLOYMENT is not set" -ForegroundColor Red
    $testPassed = $false
} else {
    Write-Host "✓ AZURE_OPENAI_WHISPER_DEPLOYMENT is set" -ForegroundColor Green
}

Write-Host ""

if ($testPassed) {
    Write-Host "=== Configuration Complete ===" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can now run VoiceBridgeMCP with:" -ForegroundColor Cyan
    Write-Host "  dotnet run --project VoiceBridgeMCP" -ForegroundColor White
    Write-Host ""
    Write-Host "Or configure your MCP client (.roo/mcp.json):" -ForegroundColor Cyan
    Write-Host @"
{
  "mcpServers": {
    "voice-bridge": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/VoiceBridgeMCP"],
      "env": {
        "AZURE_OPENAI_ENDPOINT": "$Endpoint",
        "AZURE_OPENAI_API_KEY": "your-api-key",
        "AZURE_OPENAI_TTS_DEPLOYMENT": "$TtsDeployment",
        "AZURE_OPENAI_WHISPER_DEPLOYMENT": "$WhisperDeployment"
      }
    }
  }
}
"@ -ForegroundColor White
} else {
    Write-Host "=== Configuration Failed ===" -ForegroundColor Red
    Write-Host "Please check the errors above and try again." -ForegroundColor Yellow
}