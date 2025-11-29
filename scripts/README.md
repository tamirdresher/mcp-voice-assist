# Scripts Directory

This directory contains utility scripts for the VoiceBridgeMCP project, organized by purpose.

## Directory Structure

```
scripts/
├── setup/          # Configuration and setup scripts
│   ├── setup-azure-openai-env.ps1
│   ├── setup-azure-secrets.ps1
│   └── setup-github-repo.ps1
└── test/           # Testing and validation scripts
    ├── test-mcp-interactive.ps1
    ├── test-mcp.ps1
    ├── test-stdio-separation.ps1
    └── test-voice-tool.ps1
```

## Setup Scripts

### [`setup-azure-openai-env.ps1`](setup/setup-azure-openai-env.ps1)
**Purpose:** Configure Azure OpenAI environment variables for VoiceBridgeMCP

**Usage:**
```powershell
cd scripts/setup
.\setup-azure-openai-env.ps1 -Endpoint "https://your-resource.openai.azure.com/" -ApiKey "your-api-key"

# With custom deployment names
.\setup-azure-openai-env.ps1 -Endpoint "https://your-resource.openai.azure.com/" -ApiKey "your-api-key" -TtsDeployment "my-tts" -WhisperDeployment "my-whisper"

# Set for current user (persistent)
.\setup-azure-openai-env.ps1 -Endpoint "..." -ApiKey "..." -SetUser

# Set system-wide (requires admin)
.\setup-azure-openai-env.ps1 -Endpoint "..." -ApiKey "..." -SetSystem
```

**Parameters:**
- `-Endpoint` (required): Azure OpenAI endpoint URL
- `-ApiKey` (required): Azure OpenAI API key
- `-TtsDeployment` (optional): TTS deployment name (default: "tts")
- `-WhisperDeployment` (optional): Whisper deployment name (default: "whisper")
- `-SetUser`: Make variables persistent for current user
- `-SetSystem`: Make variables persistent system-wide (requires admin)

**What it does:**
- Validates and corrects endpoint URL format
- Sets environment variables for current PowerShell session
- Optionally sets persistent environment variables
- Tests the configuration
- Provides MCP client configuration example

### [`setup-azure-secrets.ps1`](setup/setup-azure-secrets.ps1)
**Purpose:** Configure Azure OpenAI credentials using .NET user secrets (more secure than environment variables)

**Usage:**
```powershell
cd scripts/setup
.\setup-azure-secrets.ps1
```

**Interactive prompts:**
- Azure OpenAI endpoint
- API key (hidden input)
- TTS deployment name (default: "tts")
- Whisper deployment name (default: "whisper")

**What it does:**
- Securely prompts for Azure OpenAI credentials
- Stores credentials in .NET user secrets (not in source control)
- Configures both TTS and Whisper deployment names
- Provides confirmation of configured values

**Requirements:**
- .NET SDK installed
- VoiceBridgeMCP project configured for user secrets

### [`setup-github-repo.ps1`](setup/setup-github-repo.ps1)
**Purpose:** One-time setup script for GitHub repository configuration

**Usage:**
```powershell
cd scripts/setup
.\setup-github-repo.ps1
```

**What it does:**
- Configures GitHub repository settings
- Sets up branch protection rules
- Configures workflows and actions
- **Note:** This is typically run once during initial repository setup

## Test Scripts

### [`test-mcp.ps1`](test/test-mcp.ps1)
**Purpose:** Basic MCP server functionality test

**Usage:**
```powershell
cd scripts/test
.\test-mcp.ps1
```

**What it does:**
- Tests MCP server initialization
- Validates tool registration
- Checks basic MCP protocol compliance
- Reports test results

### [`test-mcp-interactive.ps1`](test/test-mcp-interactive.ps1)
**Purpose:** Interactive MCP server testing with manual verification

**Usage:**
```powershell
cd scripts/test
.\test-mcp-interactive.ps1
```

**What it does:**
- Starts MCP server in interactive mode
- Allows manual testing of MCP tools
- Provides interactive command prompt
- Useful for debugging and manual validation

### [`test-stdio-separation.ps1`](test/test-stdio-separation.ps1)
**Purpose:** Test STDIO stream separation (ensures logs don't interfere with MCP protocol)

**Usage:**
```powershell
cd scripts/test
.\test-stdio-separation.ps1
```

**What it does:**
- Validates that console output is properly separated from MCP JSON-RPC
- Ensures logging doesn't corrupt MCP messages
- Tests error stream handling
- Critical for MCP protocol compliance

### [`test-voice-tool.ps1`](test/test-voice-tool.ps1)
**Purpose:** Test voice-specific tools (TTS and Speech-to-Text)

**Usage:**
```powershell
cd scripts/test
.\test-voice-tool.ps1
```

**What it does:**
- Tests text-to-speech functionality
- Tests speech-to-text (Whisper) functionality
- Validates Azure OpenAI integration
- Checks audio file handling

**Requirements:**
- Azure OpenAI credentials configured (via environment variables or user secrets)
- Both TTS and Whisper deployments created in Azure OpenAI

## Prerequisites

### All Scripts
- **PowerShell 5.1+** or **PowerShell Core 7+**
- **Windows** operating system

### Setup Scripts
- **.NET SDK** (for user secrets management)
- **Azure OpenAI** account with:
  - TTS deployment (model: tts-1 or tts-1-hd)
  - Whisper deployment (model: whisper-1)

### Test Scripts
- **VoiceBridgeMCP** project built successfully
- **Azure OpenAI** credentials configured
- **.NET SDK** installed

## Common Workflows

### Initial Setup
```powershell
# 1. Configure Azure OpenAI credentials (choose one method)
.\scripts\setup\setup-azure-secrets.ps1          # Recommended: Uses secure user secrets
# OR
.\scripts\setup\setup-azure-openai-env.ps1 -Endpoint "..." -ApiKey "..." -SetUser

# 2. Test the configuration
.\scripts\test\test-voice-tool.ps1

# 3. Test MCP server
.\scripts\test\test-mcp.ps1
```

### Development Testing
```powershell
# Quick MCP test
.\scripts\test\test-mcp.ps1

# Interactive debugging
.\scripts\test\test-mcp-interactive.ps1

# Validate STDIO separation
.\scripts\test\test-stdio-separation.ps1
```

## Troubleshooting

### "Execution Policy" Errors
If you see errors about execution policy, run PowerShell as Administrator and execute:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Missing Azure OpenAI Credentials
Ensure you've run one of the setup scripts:
- `setup-azure-secrets.ps1` (recommended)
- `setup-azure-openai-env.ps1`

### Test Failures
1. Verify Azure OpenAI deployments exist
2. Check API key is valid
3. Ensure endpoint URL is correct (must start with `https://` and end with `/`)
4. Verify .NET SDK is installed: `dotnet --version`

## Migration Note

These scripts were previously located in the repository root and have been organized into this `scripts/` directory for better project structure and maintainability.