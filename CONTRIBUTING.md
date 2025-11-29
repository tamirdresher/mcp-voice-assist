# Contributing to VoiceMCP

First off, thank you for considering contributing to VoiceMCP! It's people like you that make VoiceMCP such a great tool.

## Code of Conduct

This project and everyone participating in it is governed by our commitment to providing a welcoming and inspiring community for all. By participating, you are expected to uphold professional and respectful communication standards.

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check the existing issues to avoid duplicates. When you create a bug report, please include as many details as possible:

* **Use a clear and descriptive title**
* **Describe the exact steps to reproduce the problem**
* **Provide specific examples** (code samples, audio files, etc.)
* **Describe the behavior you observed** and what you expected
* **Include logs and error messages** (from stderr)
* **Specify your environment**:
  - OS version
  - .NET version
  - Azure OpenAI deployments
  - Audio hardware details

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion, please include:

* **Use a clear and descriptive title**
* **Provide a detailed description** of the suggested enhancement
* **Explain why this enhancement would be useful**
* **List any alternative solutions** you've considered

### Pull Requests

1. **Fork the repository** and create your branch from `main`
2. **Make your changes** following our coding standards
3. **Add tests** if applicable
4. **Update documentation** as needed
5. **Ensure the build passes** (`dotnet build`)
6. **Submit your pull request**

## Development Setup

### Prerequisites

- .NET 10.0 SDK or later
- Windows OS (for NAudio compatibility)
- Azure OpenAI account with TTS and Whisper deployments
- Git

### Getting Started

1. **Clone your fork**:
   ```bash
   git clone https://github.com/YOUR-USERNAME/mcp-voice-assist.git
   cd mcp-voice-assist
   ```

2. **Configure secrets**:
   ```bash
   cd VoiceMCP
   dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
   dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
   dotnet user-secrets set "AzureOpenAI:TtsDeploymentName" "tts-1"
   dotnet user-secrets set "AzureOpenAI:WhisperDeploymentName" "whisper-1"
   ```

3. **Build the project**:
   ```bash
   dotnet build VoiceMCP.sln
   ```

4. **Run the server**:
   ```bash
   cd VoiceMCP
   dotnet run
   ```

## Coding Standards

### C# Style Guide

- Follow [Microsoft's C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and concise
- Use async/await for I/O operations

### Project Structure

```
VoiceMCP/
├── Program.cs              # Entry point and DI setup
├── IVoiceService.cs        # Voice service interface
├── SemanticKernelVoiceService.cs  # Azure OpenAI implementation
├── WindowsVoiceService.cs  # Windows Speech implementation
└── VoiceTools.cs           # MCP tool implementations
```

### Documentation

- Update README.md for user-facing changes
- Add XML comments for public methods and classes
- Update technical docs for implementation details
- Include code examples where helpful

## Testing

### Manual Testing

Use the provided test scripts:

```powershell
# Test MCP initialization
.\test-mcp-init.ps1

# Test voice tools
.\test-voice-tool.ps1

# Interactive testing
.\test-mcp-interactive.ps1
```

### Audio Testing Checklist

- [ ] Test with different microphones
- [ ] Verify clear audio quality
- [ ] Test silence detection threshold
- [ ] Verify MP3 conversion
- [ ] Test Whisper API integration
- [ ] Test TTS audio playback
- [ ] Verify confirmation loops
- [ ] Test retry mechanisms

## Publishing NuGet Packages

### Creating a Release

1. **Update version** in `VoiceMCP.csproj`:
   ```xml
   <Version>1.0.1</Version>
   ```

2. **Create and push a tag**:
   ```bash
   git tag v1.0.1
   git push origin v1.0.1
   ```

3. **GitHub Actions** will automatically:
   - Build the project
   - Pack the NuGet package
   - Publish to NuGet.org
   - Create a GitHub release

### Manual Publishing

```bash
# Pack the package
dotnet pack VoiceMCP/VoiceMCP.csproj --configuration Release --output ./nupkg

# Publish to NuGet
dotnet nuget push ./nupkg/VoiceMCP.1.0.0.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

## Commit Message Guidelines

Follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:

- `feat:` New feature
- `fix:` Bug fix
- `docs:` Documentation changes
- `style:` Code style changes (formatting, etc.)
- `refactor:` Code refactoring
- `test:` Adding or updating tests
- `chore:` Maintenance tasks

### Examples

```
feat: add support for custom voice selection
fix: resolve MP3 conversion error on silence detection
docs: update Azure OpenAI setup instructions
refactor: extract confirmation logic into separate method
```

## Branch Naming

- `feature/description` - New features
- `fix/description` - Bug fixes
- `docs/description` - Documentation updates
- `refactor/description` - Code refactoring

## Questions?

Feel free to:
- Open an issue for discussion
- Reach out to maintainers
- Check existing documentation

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

---

Thank you for your contributions! 🎉