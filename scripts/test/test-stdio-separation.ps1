# Test STDIO Separation for MCP Server
# This verifies that STDOUT contains ONLY JSON-RPC messages and STDERR contains diagnostics

$ErrorActionPreference = "Continue"

# Get the script directory and set working directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Write-Host "Testing STDIO Separation for VoiceBridgeMCP" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# Create temporary files for STDOUT and STDERR
$stdoutFile = [System.IO.Path]::GetTempFileName()
$stderrFile = [System.IO.Path]::GetTempFileName()

try {
    Write-Host "Test 1: Sending initialize request..." -ForegroundColor Yellow
    Write-Host ""
    
    # Build the project first to ensure latest code
    Write-Host "Building project..." -ForegroundColor Gray
    dotnet build "VoiceBridgeMCP\VoiceBridgeMCP.csproj" --nologo --verbosity quiet
    
    # Send initialize message
    $initMessage = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'
    
    # Create a temporary input file
    $inputFile = [System.IO.Path]::GetTempFileName()
    $initMessage | Out-File -FilePath $inputFile -Encoding ASCII -NoNewline
    
    # Run the server, capturing STDOUT and STDERR separately
    $process = Start-Process -FilePath "dotnet" `
        -ArgumentList "run","--project","VoiceBridgeMCP\VoiceBridgeMCP.csproj","--no-build" `
        -RedirectStandardInput $inputFile `
        -RedirectStandardOutput $stdoutFile `
        -RedirectStandardError $stderrFile `
        -NoNewWindow `
        -PassThru
    
    # Wait for process to complete (with timeout)
    $completed = $process.WaitForExit(10000)
    if (-not $completed) {
        Write-Host "WARNING: Process did not complete within timeout, stopping..." -ForegroundColor Yellow
        $process.Kill()
    }
    
    # Read the outputs
    $stdout = Get-Content $stdoutFile -Raw
    $stderr = Get-Content $stderrFile -Raw
    
    # Display results
    Write-Host "=== STDOUT (should be JSON-RPC only) ===" -ForegroundColor Green
    if ([string]::IsNullOrWhiteSpace($stdout)) {
        Write-Host "(empty)" -ForegroundColor Gray
    } else {
        Write-Host $stdout
    }
    Write-Host ""
    
    Write-Host "=== STDERR (diagnostics) ===" -ForegroundColor Cyan
    if ([string]::IsNullOrWhiteSpace($stderr)) {
        Write-Host "(empty)" -ForegroundColor Gray
    } else {
        Write-Host $stderr
    }
    Write-Host ""
    
    # Analyze STDOUT
    Write-Host "=== ANALYSIS ===" -ForegroundColor Magenta
    if ([string]::IsNullOrWhiteSpace($stdout)) {
        Write-Host "[FAIL] STDOUT is empty - server may have exited early" -ForegroundColor Red
    } else {
        # Check if STDOUT contains only JSON
        $lines = $stdout -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        $allJson = $true
        $nonJsonLines = @()
        
        foreach ($line in $lines) {
            $trimmed = $line.Trim()
            if ($trimmed -and $trimmed -notmatch '^\s*\{.*\}\s*$') {
                $allJson = $false
                $nonJsonLines += $trimmed
            }
        }
        
        if ($allJson) {
            Write-Host "[PASS] STDOUT contains only JSON messages" -ForegroundColor Green
        } else {
            Write-Host "[FAIL] STDOUT contains non-JSON content:" -ForegroundColor Red
            foreach ($line in $nonJsonLines) {
                Write-Host "  $line" -ForegroundColor Yellow
            }
        }
    }
    
    # Check for common contamination patterns
    if ($stdout -match "ERROR:" -or $stdout -match "WARNING:" -or $stdout -match "MSB3026") {
        Write-Host "[FAIL] STDOUT contains error/warning messages (should be in STDERR)" -ForegroundColor Red
    }
    
    if ($stdout -match "VoiceBridgeMCP" -and $stdout -notmatch '"jsonrpc"') {
        Write-Host "[FAIL] STDOUT contains diagnostic text (should be in STDERR)" -ForegroundColor Red
    }
    
} finally {
    # Cleanup
    if (Test-Path $stdoutFile) { Remove-Item $stdoutFile }
    if (Test-Path $stderrFile) { Remove-Item $stderrFile }
    if (Test-Path $inputFile) { Remove-Item $inputFile }
}

Write-Host ""
Write-Host "Test completed!" -ForegroundColor Cyan