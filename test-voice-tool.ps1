# Test script for voice tools
# This script tests the ask_user_voice tool with TTS and STT

Write-Host "Starting Voice Tool Test..." -ForegroundColor Green
Write-Host "This will test the TTS (tts) and Whisper (whisper) deployments" -ForegroundColor Cyan
Write-Host ""

# Change to the VoiceBridgeMCP directory
Set-Location "C:\Users\tamirdresher\source\repos\mcp-voice-assist\VoiceBridgeMCP"

# Build the project
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green
Write-Host ""

# Create test JSON request for ask_user_voice tool (must be single line for MCP)
$testRequest = '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"ask_user_voice","arguments":{"question":"What is your favorite programming language?"}}}'

Write-Host "Test Request:" -ForegroundColor Cyan
Write-Host $testRequest
Write-Host ""

Write-Host "Starting MCP Server..." -ForegroundColor Yellow
Write-Host "The server will:" -ForegroundColor Cyan
Write-Host "  1. Use TTS to speak the question: 'What is your favorite programming language?'" -ForegroundColor White
Write-Host "  2. Wait for you to speak your answer" -ForegroundColor White
Write-Host "  3. Use Whisper to transcribe your speech" -ForegroundColor White
Write-Host "  4. Ask you to confirm with 'yes' or retry with 'no'" -ForegroundColor White
Write-Host ""

Write-Host "INSTRUCTIONS:" -ForegroundColor Yellow
Write-Host "  - Listen for the question to be spoken" -ForegroundColor White
Write-Host "  - Speak your answer clearly into the microphone" -ForegroundColor White
Write-Host "  - Say 'yes' to confirm or 'no' to retry" -ForegroundColor White
Write-Host ""

# Run the server and pipe the test request as a single line
Write-Host "Running test..." -ForegroundColor Green
Write-Output $testRequest | dotnet run --no-build --configuration Release

Write-Host ""
Write-Host "Test completed!" -ForegroundColor Green