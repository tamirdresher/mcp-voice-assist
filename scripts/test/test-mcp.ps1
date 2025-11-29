# Test MCP Server via STDIO
# This script sends JSON-RPC messages to test the VoiceBridgeMCP server

$ErrorActionPreference = "Continue"

# Get the script directory and set working directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Write-Host "Testing VoiceBridgeMCP Server..." -ForegroundColor Cyan
Write-Host "Working Directory: $scriptDir" -ForegroundColor Gray
Write-Host ""

# Test 1: Initialize
Write-Host "Test 1: Sending initialize request..." -ForegroundColor Yellow
$initMessage = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'
$initResult = $initMessage | dotnet run --project "VoiceBridgeMCP\VoiceBridgeMCP.csproj" 2>&1

Write-Host "Initialize Response:" -ForegroundColor Green
Write-Host $initResult
Write-Host ""

# Test 2: List Tools
Write-Host "Test 2: Sending tools/list request..." -ForegroundColor Yellow
$toolsMessage = '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
$toolsResult = $toolsMessage | dotnet run --project "VoiceBridgeMCP\VoiceBridgeMCP.csproj" 2>&1

Write-Host "Tools List Response:" -ForegroundColor Green
Write-Host $toolsResult
Write-Host ""

Write-Host "Test completed!" -ForegroundColor Cyan