# Voice Tool Test Results

## Test Date
2025-11-28

## Issue Discovered
When testing the `ask_user_voice` tool, we encountered an Azure OpenAI configuration issue:

### Problem
The code was attempting to use the `gpt-4o` deployment for Text-to-Speech (TTS), but Azure OpenAI's `gpt-4o` model doesn't support audio operations.

**Error Message:**
```
HTTP 400 (OperationNotSupported)
The audio operation does not work with the specified model, gpt-4o.
```

### Root Cause
The [`Program.cs`](../mcp-voice-assist/VoiceBridgeMCP/Program.cs:41) was using a single `AzureOpenAI:DeploymentName` for both TTS and Whisper services, but Azure OpenAI requires separate deployments for different model types.

### Solution Implemented

1. **Updated [`Program.cs`](../mcp-voice-assist/VoiceBridgeMCP/Program.cs:26)** to use separate deployment names:
   - `AzureOpenAI:TtsDeploymentName` (default: "tts")
   - `AzureOpenAI:WhisperDeploymentName` (default: "whisper")

2. **Updated [`.env.example`](../mcp-voice-assist/VoiceBridgeMCP/.env.example:4)** with documentation for Azure OpenAI configuration

3. **Created [`setup-azure-secrets.ps1`](../mcp-voice-assist/setup-azure-secrets.ps1:1)** to help users configure Azure OpenAI deployments

4. **Updated user secrets** with correct deployment names:
   ```powershell
   dotnet user-secrets set "AzureOpenAI:TtsDeploymentName" "tts"
   dotnet user-secrets set "AzureOpenAI:WhisperDeploymentName" "whisper"
   ```

## Azure OpenAI Deployment Requirements

To use the voice tools with Azure OpenAI, you need to create **two separate deployments**:

1. **TTS Deployment**
   - Model: `tts-1` or `tts-1-hd`
   - Deployment name: `tts` (or your custom name)

2. **Whisper Deployment**
   - Model: `whisper-1`
   - Deployment name: `whisper` (or your custom name)

## Configuration

### Using Azure OpenAI (Recommended)
```powershell
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
dotnet user-secrets set "AzureOpenAI:TtsDeploymentName" "tts"
dotnet user-secrets set "AzureOpenAI:WhisperDeploymentName" "whisper"
```

### Using OpenAI API (Alternative)
Set `OPENAI_API_KEY` environment variable or in `.env` file.

## Next Steps

To properly test the voice tool, the user needs to:

1. **Create Azure OpenAI deployments** for TTS and Whisper models
2. **Configure user secrets** with the correct deployment names
3. **Close any running VoiceBridgeMCP.exe processes** manually (Task Manager may be needed if PowerShell access is denied)
4. **Run the test again** using [`test-voice-tool.ps1`](test-voice-tool.ps1:1)

## Test Script

The test script [`test-voice-tool.ps1`](test-voice-tool.ps1:1) sends these MCP messages:
1. `initialize` - Sets up the MCP connection
2. `tools/call` with `ask_user_voice` - Asks "What is your favorite programming language?"

The tool should:
1. Play the question via TTS
2. Record the user's voice response
3. Transcribe it using Whisper
4. Ask for confirmation
5. Return the transcribed text

## Files Modified

1. [`Program.cs`](../mcp-voice-assist/VoiceBridgeMCP/Program.cs:1) - Added separate TTS and Whisper deployment configuration
2. [`.env.example`](../mcp-voice-assist/VoiceBridgeMCP/.env.example:1) - Added Azure OpenAI configuration documentation
3. [`setup-azure-secrets.ps1`](../mcp-voice-assist/setup-azure-secrets.ps1:1) - Created setup helper script
4. [`test-voice-tool.ps1`](test-voice-tool.ps1:1) - Created test script for voice tool

## Status

⚠️ **Configuration Fixed, Testing Blocked**

The configuration issues have been resolved, but actual voice testing requires:
- Azure OpenAI deployments to be created
- Running processes to be terminated (requires elevated privileges)