# NuGet Publishing Guide

This guide explains how to publish VoiceMCP as a .NET tool on NuGet.org.

## Prerequisites

1. **NuGet Account**
   - Create an account at [nuget.org](https://www.nuget.org/)
   - Generate an API key from your account settings

2. **GitHub Secrets**
   - Add `NUGET_API_KEY` to your repository secrets
   - Navigate to: Repository Settings → Secrets and variables → Actions → New repository secret

## Package Configuration

The package is configured in [`VoiceMCP.csproj`](VoiceMCP/VoiceMCP.csproj) with:

```xml
<PackAsTool>true</PackAsTool>
<ToolCommandName>voicemcp</ToolCommandName>
<PackageId>VoiceMCP</PackageId>
<Version>1.0.0</Version>
```

### Key Properties

- **PackAsTool**: Marks this as a .NET tool
- **ToolCommandName**: The command users will run (`voicemcp`)
- **PackageId**: Unique package identifier on NuGet
- **Version**: Semantic version (update for each release)

## Publishing Methods

### Method 1: Automated via GitHub Actions (Recommended)

#### Release via Git Tag

1. **Update version** in `VoiceMCP/VoiceMCP.csproj`:
   ```xml
   <Version>1.0.1</Version>
   ```

2. **Commit and push** the version change:
   ```bash
   git add VoiceMCP/VoiceMCP.csproj
   git commit -m "chore: bump version to 1.0.1"
   git push
   ```

3. **Create and push a version tag**:
   ```bash
   git tag v1.0.1
   git push origin v1.0.1
   ```

4. **GitHub Actions** automatically:
   - ✅ Builds the project
   - ✅ Packs the NuGet package
   - ✅ Publishes to NuGet.org
   - ✅ Creates a GitHub release

#### Manual Workflow Dispatch

1. Go to **Actions** tab on GitHub
2. Select **Publish to NuGet** workflow
3. Click **Run workflow**
4. Enter the version number (e.g., `1.0.1`)
5. Click **Run workflow**

### Method 2: Local Publishing

#### Prerequisites

```bash
# Ensure you have the latest .NET SDK
dotnet --version  # Should be 10.0.x or later
```

#### Steps

1. **Clean previous builds**:
   ```bash
   dotnet clean VoiceMCP/VoiceMCP.csproj
   ```

2. **Build in Release mode**:
   ```bash
   dotnet build VoiceMCP/VoiceMCP.csproj --configuration Release
   ```

3. **Pack the NuGet package**:
   ```bash
   dotnet pack VoiceMCP/VoiceMCP.csproj --configuration Release --output ./nupkg
   ```

4. **Publish to NuGet**:
   ```bash
   dotnet nuget push ./nupkg/VoiceMCP.1.0.0.nupkg \
     --api-key YOUR_NUGET_API_KEY \
     --source https://api.nuget.org/v3/index.json
   ```

## Installation for End Users

Once published, users can install VoiceMCP as a global .NET tool:

```bash
# Install globally
dotnet tool install --global VoiceMCP

# Run the tool
voicemcp

# Update to latest version
dotnet tool update --global VoiceMCP

# Uninstall
dotnet tool uninstall --global VoiceMCP
```

## MCP Client Configuration

After installation, users configure their MCP clients:

### Configuration via Installed Tool

```json
{
  "mcpServers": {
    "voice-mcp": {
      "command": "voicemcp",
      "env": {
        "AZURE_OPENAI_ENDPOINT": "https://your-resource.openai.azure.com/",
        "AZURE_OPENAI_API_KEY": "your-api-key",
        "AZURE_OPENAI_TTS_DEPLOYMENT": "tts-1",
        "AZURE_OPENAI_WHISPER_DEPLOYMENT": "whisper-1"
      }
    }
  }
}
```

### Configuration via Source

```json
{
  "mcpServers": {
    "voice-mcp": {
      "command": "dotnet",
      "args": [
        "run",
        "--no-build",
        "--project",
        "C:/path/to/VoiceMCP/VoiceMCP.csproj"
      ],
      "env": {
        "AZURE_OPENAI_ENDPOINT": "https://your-resource.openai.azure.com/",
        "AZURE_OPENAI_API_KEY": "your-api-key",
        "AZURE_OPENAI_TTS_DEPLOYMENT": "tts-1",
        "AZURE_OPENAI_WHISPER_DEPLOYMENT": "whisper-1"
      }
    }
  }
}
```

## Version Management

### Semantic Versioning

Follow [SemVer](https://semver.org/) for version numbers:

- **MAJOR.MINOR.PATCH** (e.g., 1.2.3)
- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes (backward compatible)

### Pre-release Versions

For beta/preview releases:

```xml
<Version>1.1.0-beta.1</Version>
```

Users install with:
```bash
dotnet tool install --global VoiceMCP --version 1.1.0-beta.1
```

## Package Metadata

The package includes:

- **README.md**: Displayed on NuGet package page
- **LICENSE**: MIT License
- **Icon**: Package icon (if `icon.png` exists)
- **Tags**: `mcp`, `voice`, `azure-openai`, `tts`, `whisper`, `ai`

## Troubleshooting

### Package Already Exists

If you get "Package already exists" error:
- Increment the version number
- Use `--skip-duplicate` flag:
  ```bash
  dotnet nuget push ./nupkg/VoiceMCP.1.0.0.nupkg \
    --api-key YOUR_KEY \
    --source https://api.nuget.org/v3/index.json \
    --skip-duplicate
  ```

### Build Errors

```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build --configuration Release
```

### API Key Issues

- Verify API key hasn't expired
- Check API key has "Push" permissions
- Ensure API key is correctly set in GitHub Secrets

## Verifying Publication

1. **Check NuGet.org**:
   - Visit https://www.nuget.org/packages/VoiceMCP
   - Package should appear within 15 minutes

2. **Test Installation**:
   ```bash
   dotnet tool install --global VoiceMCP --version 1.0.0
   voicemcp --help
   ```

3. **Verify GitHub Release**:
   - Check https://github.com/tamirdresher/mcp-voice-assist/releases
   - Release should include `.nupkg` file

## Package Statistics

Monitor package usage at:
- https://www.nuget.org/packages/VoiceMCP/
- Package downloads
- Version distribution
- Dependency graph

## Support

For issues with publishing:
- Check [GitHub Actions logs](../../actions)
- Review [NuGet documentation](https://docs.microsoft.com/en-us/nuget/)
- Open an issue in this repository

---

**Happy Publishing! 🚀**