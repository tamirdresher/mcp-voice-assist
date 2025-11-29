# Setup script for Azure OpenAI user secrets
# This configures the VoiceBridgeMCP project to use Azure OpenAI with separate TTS and Whisper deployments

Write-Host "Azure OpenAI Setup for VoiceBridgeMCP" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

$projectPath = "C:\Users\tamirdresher\source\repos\mcp-voice-assist\VoiceBridgeMCP\VoiceBridgeMCP.csproj"

Write-Host "This script will configure user secrets for Azure OpenAI." -ForegroundColor Yellow
Write-Host ""
Write-Host "You need to create TWO deployments in Azure OpenAI:" -ForegroundColor Yellow
Write-Host "  1. TTS deployment (model: tts-1 or tts-1-hd)" -ForegroundColor White
Write-Host "  2. Whisper deployment (model: whisper-1)" -ForegroundColor White
Write-Host ""

$endpoint = Read-Host "Enter your Azure OpenAI endpoint (e.g., https://your-resource.openai.azure.com)"
$apiKey = Read-Host "Enter your Azure OpenAI API key" -AsSecureString
$apiKeyPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($apiKey))

Write-Host ""
Write-Host "Enter deployment names (press Enter to use defaults):" -ForegroundColor Cyan
$ttsDeployment = Read-Host "TTS deployment name [default: tts]"
if ([string]::IsNullOrWhiteSpace($ttsDeployment)) { $ttsDeployment = "tts" }

$whisperDeployment = Read-Host "Whisper deployment name [default: whisper]"
if ([string]::IsNullOrWhiteSpace($whisperDeployment)) { $whisperDeployment = "whisper" }

Write-Host ""
Write-Host "Setting user secrets..." -ForegroundColor Cyan

dotnet user-secrets set "AzureOpenAI:Endpoint" "$endpoint" --project $projectPath
dotnet user-secrets set "AzureOpenAI:ApiKey" "$apiKeyPlain" --project $projectPath
dotnet user-secrets set "AzureOpenAI:TtsDeploymentName" "$ttsDeployment" --project $projectPath
dotnet user-secrets set "AzureOpenAI:WhisperDeploymentName" "$whisperDeployment" --project $projectPath

Write-Host ""
Write-Host "✓ User secrets configured successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Cyan
Write-Host "  Endpoint: $endpoint" -ForegroundColor White
Write-Host "  TTS Deployment: $ttsDeployment" -ForegroundColor White
Write-Host "  Whisper Deployment: $whisperDeployment" -ForegroundColor White