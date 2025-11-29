# Azure OpenAI Credentials Loading Fix

## Problem Summary

The VoiceBridgeMCP server was using a mock "test-key" API key instead of loading real Azure OpenAI credentials from user secrets, causing HTTP 401 authentication errors when started by external MCP clients.

## Root Cause

When MCP clients start the server via `dotnet run`, user secrets may not be accessible in that context. The code was falling back to the mock "test-key" value, which is rejected by Azure OpenAI.

## Solution Implemented

Implemented a **hybrid credential loading strategy** that prioritizes environment variables (for MCP clients) over user secrets (for development):

1. **Try environment variables first** (ideal for MCP clients)
2. **Fall back to user secrets/appsettings.json** (for development)
3. **Fail with clear instructions** if no valid credentials found

### Changes Made

#### 1. Updated [`Program.cs`](VoiceBridgeMCP/Program.cs)

- Added explicit environment variable checking before configuration lookup
- Prioritized environment variables for all Azure OpenAI settings
- Added validation to reject mock credentials ("test-key")
- Improved error messages with clear setup instructions

**Key Environment Variables:**
- `AZURE_OPENAI_ENDPOINT` - Azure OpenAI endpoint URL
- `AZURE_OPENAI_API_KEY` - Azure OpenAI API key
- `AZURE_OPENAI_TTS_DEPLOYMENT` - TTS deployment name (optional, defaults to "tts")
- `AZURE_OPENAI_WHISPER_DEPLOYMENT` - Whisper deployment name (optional, defaults to "whisper")

#### 2. Updated [`README.md`](VoiceBridgeMCP/README.md)

- Reorganized configuration section to prioritize environment variables
- Added MCP client configuration examples
- Improved troubleshooting section for HTTP 401 errors
- Added setup script usage instructions

#### 3. Created Setup Script [`setup-azure-openai-env.ps1`](setup-azure-openai-env.ps1)

PowerShell script to easily configure environment variables:

```powershell
.\setup-azure-openai-env.ps1 `
  -Endpoint "https://your-resource.openai.azure.com/" `
  -ApiKey "your-api-key" `
  -TtsDeployment "tts" `
  -WhisperDeployment "whisper" `
  -SetUser  # Optional: persist for current user
```

#### 4. Created Example MCP Configuration [`mcp-config-example.json`](mcp-config-example.json)

Template for MCP client configuration with environment variables.

## Testing

### Option 1: Test with Environment Variables (Recommended)

1. **Set environment variables** using the setup script:
   ```powershell
   .\setup-azure-openai-env.ps1 `
     -Endpoint "https://your-resource.openai.azure.com/" `
     -ApiKey "your-actual-api-key" `
     -TtsDeployment "tts" `
     -WhisperDeployment "whisper"
   ```

2. **Run the server**:
   ```bash
   cd VoiceBridgeMCP
   dotnet run
   ```

3. **Verify** - You should see:
   ```
   Loaded Azure OpenAI credentials from environment variables
   Using Azure OpenAI configuration
   Endpoint: https://your-resource.openai.azure.com/
   ```

### Option 2: Test with MCP Client

1. **Update your `.roo/mcp.json`** (or equivalent):
   ```json
   {
     "mcpServers": {
       "voice-bridge": {
         "command": "dotnet",
         "args": [
           "run",
           "--project",
           "C:/Users/YourUsername/source/repos/mcp-voice-assist/VoiceBridgeMCP/VoiceBridgeMCP.csproj"
         ],
         "env": {
           "AZURE_OPENAI_ENDPOINT": "https://your-resource.openai.azure.com/",
           "AZURE_OPENAI_API_KEY": "your-actual-api-key",
           "AZURE_OPENAI_TTS_DEPLOYMENT": "tts",
           "AZURE_OPENAI_WHISPER_DEPLOYMENT": "whisper"
         }
       }
     }
   }
   ```

2. **Restart your MCP client** (Roo, Cline, etc.)

3. **Test the voice tools** - They should now authenticate successfully with Azure OpenAI

### Option 3: Test with User Secrets (Development)

1. **Set user secrets**:
   ```bash
   dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
   dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
   dotnet user-secrets set "AzureOpenAI:TtsDeploymentName" "tts"
   dotnet user-secrets set "AzureOpenAI:WhisperDeploymentName" "whisper"
   ```

2. **Run the server**:
   ```bash
   cd VoiceBridgeMCP
   dotnet run
   ```

3. **Verify** - You should see:
   ```
   Loaded Azure OpenAI credentials from user secrets/appsettings.json
   Using Azure OpenAI configuration
   ```

## Expected Behavior

### Success Cases

✅ **With valid environment variables:**
```
Loaded Azure OpenAI credentials from environment variables
Using Azure OpenAI configuration
Endpoint: https://your-resource.openai.azure.com/
TTS Deployment: tts, Whisper Deployment: whisper
VoiceBridgeMCP Server Started with Semantic Kernel...
```

✅ **With valid user secrets (fallback):**
```
Loaded Azure OpenAI credentials from user secrets/appsettings.json
Using Azure OpenAI configuration
Endpoint: https://your-resource.openai.azure.com/
TTS Deployment: tts, Whisper Deployment: whisper
VoiceBridgeMCP Server Started with Semantic Kernel...
```

### Failure Cases

❌ **No credentials configured:**
```
Fatal Error: System.InvalidOperationException: Azure OpenAI credentials not configured.

For MCP clients, set environment variables:
  AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
  AZURE_OPENAI_API_KEY=your-api-key
  ...
```

❌ **Mock credentials ("test-key"):**
```
Fatal Error: System.InvalidOperationException: Azure OpenAI credentials not configured.
(Same error message as above - rejects test keys)
```

## Verification Checklist

- [x] Program.cs updated with hybrid credential loading
- [x] Environment variables checked before configuration
- [x] Mock credentials ("test-key") are rejected
- [x] Clear error messages guide users to proper setup
- [x] README.md updated with environment variable setup
- [x] Setup script created for easy configuration
- [x] Example MCP configuration provided
- [ ] Tested with real Azure OpenAI credentials
- [ ] Tested with MCP client (Roo/Cline)
- [ ] Voice tools work correctly (no HTTP 401 errors)

## Migration Path

### For Existing Deployments

If you were previously using user secrets and the server was working locally:

1. **Continue using user secrets** - They still work as a fallback
2. **OR** migrate to environment variables for better MCP compatibility:
   ```powershell
   # Export your user secrets
   dotnet user-secrets list --project VoiceBridgeMCP
   
   # Set as environment variables
   .\setup-azure-openai-env.ps1 -Endpoint "..." -ApiKey "..." -SetUser
   ```

### For MCP Clients

1. **Update your MCP configuration** to include the `env` section
2. **Add your Azure OpenAI credentials** to the environment variables
3. **Restart your MCP client** to apply changes

## Troubleshooting

### Still getting HTTP 401 errors?

1. **Verify environment variables are set**:
   ```powershell
   $env:AZURE_OPENAI_ENDPOINT
   $env:AZURE_OPENAI_API_KEY
   ```

2. **Check server startup logs** for:
   - "Loaded Azure OpenAI credentials from environment variables"
   - "Using Azure OpenAI configuration"

3. **Verify your API key is valid**:
   - Not a mock value like "test-key"
   - Has proper permissions in Azure
   - Matches your endpoint

4. **For MCP clients**, ensure:
   - Environment variables are in the MCP config
   - Absolute path to VoiceBridgeMCP.csproj
   - MCP client was restarted after config changes

## Security Notes

- **Never commit API keys to source control**
- **Use environment variables** for production deployments
- **User secrets** are safe for local development
- The **setup script** supports persistent user-level variables with `-SetUser` flag
- Consider using **Azure Key Vault** for production secrets

## Related Files

- [`Program.cs`](VoiceBridgeMCP/Program.cs) - Credential loading logic
- [`README.md`](VoiceBridgeMCP/README.md) - Updated configuration documentation
- [`setup-azure-openai-env.ps1`](setup-azure-openai-env.ps1) - Environment setup script
- [`mcp-config-example.json`](mcp-config-example.json) - Example MCP configuration