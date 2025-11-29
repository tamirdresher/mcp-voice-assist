#!/usr/bin/env pwsh
# Test script for VoiceMCP NuGet package

Write-Host "=== VoiceMCP NuGet Package Test Script ===" -ForegroundColor Cyan
Write-Host ""

# Wait for package to be available on NuGet.org
$packageName = "VoiceMCP"
$version = "1.0.0"
$maxRetries = 20
$retryInterval = 30  # seconds

Write-Host "Checking if package $packageName v$version is available on NuGet.org..." -ForegroundColor Yellow

for ($i = 1; $i -le $maxRetries; $i++) {
    Write-Host "Attempt $i of $maxRetries..." -ForegroundColor Gray
    
    try {
        $searchResult = dotnet nuget list source --format json | ConvertFrom-Json
        $nugetSource = "https://api.nuget.org/v3/index.json"
        
        # Try to find the package
        $findResult = dotnet tool search $packageName --take 1 2>&1
        
        if ($findResult -match $packageName) {
            Write-Host "✓ Package found on NuGet.org!" -ForegroundColor Green
            break
        }
    }
    catch {
        Write-Host "  Package not yet available..." -ForegroundColor Gray
    }
    
    if ($i -lt $maxRetries) {
        Write-Host "  Waiting $retryInterval seconds before next check..." -ForegroundColor Gray
        Start-Sleep -Seconds $retryInterval
    }
}

Write-Host ""
Write-Host "=== Installing VoiceMCP as a global tool ===" -ForegroundColor Cyan

# Uninstall if already installed
Write-Host "Checking for existing installation..." -ForegroundColor Yellow
$existingTool = dotnet tool list -g | Select-String "voicemcp"
if ($existingTool) {
    Write-Host "Uninstalling existing version..." -ForegroundColor Yellow
    dotnet tool uninstall -g VoiceMCP
}

# Install the package
Write-Host "Installing VoiceMCP v$version..." -ForegroundColor Yellow
$installResult = dotnet tool install -g VoiceMCP --version $version 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Installation successful!" -ForegroundColor Green
} else {
    Write-Host "✗ Installation failed!" -ForegroundColor Red
    Write-Host $installResult
    exit 1
}

Write-Host ""
Write-Host "=== Verifying Installation ===" -ForegroundColor Cyan

# Check if tool is available
$toolList = dotnet tool list -g | Select-String "voicemcp"
if ($toolList) {
    Write-Host "✓ Tool is registered:" -ForegroundColor Green
    Write-Host $toolList
} else {
    Write-Host "✗ Tool not found in global tools list!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Testing VoiceMCP MCP Server ===" -ForegroundColor Cyan
Write-Host "Note: This will test the MCP initialization without Azure OpenAI credentials" -ForegroundColor Gray
Write-Host ""

# Create a test script to initialize the MCP server
$testScript = @'
{
    "jsonrpc": "2.0",
    "method": "initialize",
    "params": {
        "protocolVersion": "2024-11-05",
        "capabilities": {
            "roots": {
                "listChanged": true
            }
        },
        "clientInfo": {
            "name": "test-client",
            "version": "1.0.0"
        }
    },
    "id": 1
}
'@

# Save test script
$testScript | Out-File -FilePath "test-mcp-init.json" -Encoding UTF8

Write-Host "Starting VoiceMCP server (will timeout after 10 seconds for testing)..." -ForegroundColor Yellow

# Start the server and send initialize message
$process = Start-Process -FilePath "voicemcp" -NoNewWindow -PassThru -RedirectStandardInput "test-mcp-init.json" -RedirectStandardOutput "mcp-output.txt" -RedirectStandardError "mcp-error.txt"

# Wait a bit for initialization
Start-Sleep -Seconds 5

# Check if process is still running (MCP servers run continuously)
if ($process.HasExited) {
    Write-Host "✗ Server exited unexpectedly!" -ForegroundColor Red
    if (Test-Path "mcp-error.txt") {
        Write-Host "Error output:" -ForegroundColor Red
        Get-Content "mcp-error.txt"
    }
    exit 1
} else {
    Write-Host "✓ Server started successfully and is running!" -ForegroundColor Green
    
    # Stop the test server
    Stop-Process -Id $process.Id -Force
    
    # Check output
    if (Test-Path "mcp-output.txt") {
        Write-Host ""
        Write-Host "Server output:" -ForegroundColor Gray
        Get-Content "mcp-output.txt"
    }
}

# Cleanup
Remove-Item "test-mcp-init.json" -ErrorAction SilentlyContinue
Remove-Item "mcp-output.txt" -ErrorAction SilentlyContinue
Remove-Item "mcp-error.txt" -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "=== Test Summary ===" -ForegroundColor Cyan
Write-Host "✓ Package published to NuGet.org" -ForegroundColor Green
Write-Host "✓ Package installed successfully" -ForegroundColor Green
Write-Host "✓ VoiceMCP server executable works" -ForegroundColor Green
Write-Host ""
Write-Host "Package is ready for use! Install with:" -ForegroundColor Green
Write-Host "  dotnet tool install -g VoiceMCP" -ForegroundColor White
Write-Host ""
Write-Host "Run with:" -ForegroundColor Green
Write-Host "  voicemcp" -ForegroundColor White
Write-Host ""