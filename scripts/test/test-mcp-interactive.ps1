# Interactive MCP Server Test
# Tests the MCP server by sending JSON-RPC messages via stdin

$ErrorActionPreference = "Continue"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Write-Host "Interactive MCP Server Test" -ForegroundColor Cyan
Write-Host "===========================" -ForegroundColor Cyan
Write-Host ""

# Build first
Write-Host "Building project..." -ForegroundColor Gray
dotnet build "VoiceBridgeMCP\VoiceBridgeMCP.csproj" --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful" -ForegroundColor Green
Write-Host ""

# Test messages
$initMessage = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'
$toolsMessage = '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'

Write-Host "Starting MCP server..." -ForegroundColor Yellow
Write-Host ""

# Start the process
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "dotnet"
$psi.Arguments = "run --project VoiceBridgeMCP\VoiceBridgeMCP.csproj --no-build"
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $psi

# Event handlers for output
$stdoutBuilder = New-Object System.Text.StringBuilder
$stderrBuilder = New-Object System.Text.StringBuilder

$stdoutEvent = Register-ObjectEvent -InputObject $process -EventName OutputDataReceived -Action {
    if ($EventArgs.Data) {
        $Event.MessageData.AppendLine($EventArgs.Data)
        Write-Host "[STDOUT] $($EventArgs.Data)" -ForegroundColor Green
    }
} -MessageData $stdoutBuilder

$stderrEvent = Register-ObjectEvent -InputObject $process -EventName ErrorDataReceived -Action {
    if ($EventArgs.Data) {
        $Event.MessageData.AppendLine($EventArgs.Data)
        Write-Host "[STDERR] $($EventArgs.Data)" -ForegroundColor Cyan
    }
} -MessageData $stderrBuilder

try {
    # Start the process
    $process.Start() | Out-Null
    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()
    
    # Wait a bit for server to initialize
    Start-Sleep -Milliseconds 500
    
    # Send initialize
    Write-Host ""
    Write-Host "Sending initialize request..." -ForegroundColor Yellow
    $process.StandardInput.WriteLine($initMessage)
    $process.StandardInput.Flush()
    
    # Wait for response
    Start-Sleep -Milliseconds 1000
    
    # Send tools/list
    Write-Host ""
    Write-Host "Sending tools/list request..." -ForegroundColor Yellow
    $process.StandardInput.WriteLine($toolsMessage)
    $process.StandardInput.Flush()
    
    # Wait for response
    Start-Sleep -Milliseconds 1000
    
    # Close stdin to signal end
    $process.StandardInput.Close()
    
    # Wait a bit more
    Start-Sleep -Milliseconds 500
    
    Write-Host ""
    Write-Host "=== TEST RESULTS ===" -ForegroundColor Magenta
    Write-Host ""
    
    $stdout = $stdoutBuilder.ToString()
    $stderr = $stderrBuilder.ToString()
    
    if ($stdout) {
        Write-Host "STDOUT Content:" -ForegroundColor Green
        Write-Host $stdout
        
        # Check if valid JSON
        $jsonLines = $stdout -split "`n" | Where-Object { $_.Trim() -and $_.Trim().StartsWith("{") }
        if ($jsonLines) {
            Write-Host ""
            Write-Host "[PASS] Found JSON-RPC messages in STDOUT" -ForegroundColor Green
        }
    } else {
        Write-Host "[FAIL] No STDOUT output received" -ForegroundColor Red
    }
    
    Write-Host ""
    if ($stderr) {
        Write-Host "STDERR Content:" -ForegroundColor Cyan
        Write-Host $stderr
    }
    
} finally {
    # Cleanup
    if (!$process.HasExited) {
        $process.Kill()
    }
    Unregister-Event -SourceIdentifier $stdoutEvent.Name -ErrorAction SilentlyContinue
    Unregister-Event -SourceIdentifier $stderrEvent.Name -ErrorAction SilentlyContinue
    $process.Dispose()
}

Write-Host ""
Write-Host "Test completed!" -ForegroundColor Cyan