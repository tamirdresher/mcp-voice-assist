<#
.SYNOPSIS
    Creates a Windows Task Scheduler task to auto-start VoiceMCP Gateway at user logon.

.DESCRIPTION
    Creates a scheduled task "VoiceMCP-Gateway" that runs start-gateway.ps1
    when the current user logs on. Requires elevated (admin) privileges.
#>

$ErrorActionPreference = "Stop"

$TaskName = "VoiceMCP-Gateway"
$ScriptPath = Join-Path $PSScriptRoot "start-gateway.ps1"

if (-not (Test-Path $ScriptPath)) {
    Write-Error "start-gateway.ps1 not found at: $ScriptPath"
    exit 1
}

# Check for admin privileges
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Warning "This script should be run as Administrator to create scheduled tasks."
    Write-Warning "Attempting anyway..."
}

# Remove existing task if present
$existingTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($existingTask) {
    Write-Host "Removing existing task '$TaskName'..."
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
}

# Create the scheduled task
$action = New-ScheduledTaskAction `
    -Execute "pwsh.exe" `
    -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$ScriptPath`""

$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -ExecutionTimeLimit (New-TimeSpan -Hours 0) `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1)

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Description "Auto-starts VoiceMCP Gateway for Teams communication" `
    -RunLevel Limited | Out-Null

Write-Host ""
Write-Host "=== VoiceMCP Gateway Startup Task Installed ===" -ForegroundColor Green
Write-Host ""
Write-Host "  Task Name:  $TaskName"
Write-Host "  Trigger:    At logon (user: $env:USERNAME)"
Write-Host "  Action:     pwsh.exe -File `"$ScriptPath`""
Write-Host "  Run Level:  Limited (user context, not elevated)"
Write-Host "  Log File:   $env:LOCALAPPDATA\VoiceMCP\gateway.log"
Write-Host ""
Write-Host "The Gateway will start automatically at next logon." -ForegroundColor Cyan
Write-Host "To start it now, run: .\scripts\start-gateway.ps1" -ForegroundColor Cyan
Write-Host ""
Write-Host "To remove: Unregister-ScheduledTask -TaskName '$TaskName' -Confirm:`$false" -ForegroundColor DarkGray
