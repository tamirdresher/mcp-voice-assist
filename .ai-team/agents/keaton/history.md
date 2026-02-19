# Keaton — History

## Project Context
- **Project:** VoiceMCP — MCP server for multi-channel AI-to-human communication
- **Stack:** C#, .NET 10, Semantic Kernel, ModelContextProtocol SDK, NAudio
- **User:** Tamir Dresher (tamir.dresher@gmail.com)
- **Description:** MCP server giving AI coding agents the ability to communicate with humans through voice (Azure OpenAI TTS + Whisper) and Teams (Incoming Webhook with adaptive cards). Packaged as a dotnet tool, published as NuGet package.
- **Key tools:** AskUser (voice), AskForApproval (voice), NotifyViaTeams, AskUserViaTeams (Teams)

## Learnings

### Architecture & Project Structure
- VoiceMCP is an MCP server packaged as a dotnet tool (voicemcp) exposing voice and Teams notification channels
- Core files: Program.cs (DI setup), VoiceTools.cs (MCP voice tools), TeamsTools.cs (MCP Teams tools)
- Service interfaces: IVoiceService (SpeakAsync/ListenAsync), ITeamsNotificationService (SendNotificationAsync/AskQuestionAsync)
- Primary implementation: SemanticKernelVoiceService uses Azure OpenAI TTS + Whisper via Semantic Kernel
- WindowsVoiceService.cs is an empty placeholder — not implemented
- Configuration: Environment variables preferred for MCP clients, user secrets for dev; all config via Program.cs conditional registration
- Tools conditionally registered based on environment variables (TEAMS_WEBHOOK_URL for Teams, Azure OpenAI vars for voice)

### Key Patterns
- Voice confirmation loop: AskUser/AskForApproval both have nested confirmation logic with MAX_RETRIES=4
- Silence-based recording: RecordAudioAsync uses silence threshold (300) and duration (2.5s) to detect end of speech
- Audio pipeline: Microphone → WAV (16kHz mono) → MP3 via NAudio.Lame → Azure Whisper → text
- Teams uses Incoming Webhook with adaptive cards; one-way only (no response capture)
- Error messages returned as strings (e.g., "ERROR: Could not understand response after 4 attempts")

### Dependencies
- ModelContextProtocol 0.4.1-preview.1, Microsoft.SemanticKernel 1.28.0, NAudio 2.2.1, NAudio.Lame 2.1.0
- Target: net10.0, packaged as tool with PackageType=McpServer
- Logging to stderr (stdout reserved for MCP protocol)
