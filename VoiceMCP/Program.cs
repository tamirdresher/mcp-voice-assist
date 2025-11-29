using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using VoiceMCP.Services;

var builder = Host.CreateApplicationBuilder(args);

// Diagnostic: Print the current environment name
Console.Error.WriteLine($"Current environment: {builder.Environment.EnvironmentName}");

// Add user secrets to configuration in development
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);


builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

builder.Services.AddSingleton<IVoiceService, SemanticKernelVoiceService>();

// Hybrid credential loading: Environment variables (MCP clients) → User secrets (dev) → Fail
// 1. Try environment variables first (for MCP clients)
var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
var azureApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
var azureDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
var azureTtsDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_TTS_DEPLOYMENT");
var azureWhisperDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_WHISPER_DEPLOYMENT");

var configuration = builder.Configuration;
if (string.IsNullOrEmpty(azureEndpoint))
{
    var configEndpoint = configuration["AzureOpenAI:Endpoint"];
    var configApiKey = configuration["AzureOpenAI:ApiKey"];

    // Only use config values if they are non-empty
    if (!string.IsNullOrWhiteSpace(configEndpoint) && !string.IsNullOrWhiteSpace(configApiKey))
    {
        azureEndpoint = configEndpoint;
        azureApiKey = configApiKey;
        azureDeployment = configuration["AzureOpenAI:DeploymentName"];
        azureTtsDeployment = configuration["AzureOpenAI:TtsDeploymentName"];
        azureWhisperDeployment = configuration["AzureOpenAI:WhisperDeploymentName"];
        Console.Error.WriteLine("Loaded Azure OpenAI credentials from user secrets/appsettings.json");
    }
    else
    {
        Console.Error.WriteLine("No valid credentials found in environment variables or configuration");
    }
}
else
{
    Console.Error.WriteLine("Loaded Azure OpenAI credentials from environment variables");
}

builder.Services.AddKernel()
    .AddAzureOpenAITextToAudio(
         deploymentName: azureTtsDeployment ?? throw new InvalidOperationException("TTS deployment name is not configured"),
         endpoint: azureEndpoint ?? throw new InvalidOperationException("Azure OpenAI endpoint is not configured"),
         apiKey: azureApiKey ?? throw new InvalidOperationException("Azure OpenAI API key is not configured"))
    .AddAzureOpenAIAudioToText(
        deploymentName: azureWhisperDeployment ?? throw new InvalidOperationException("Whisper deployment name is not configured"),
        endpoint: azureEndpoint ?? throw new InvalidOperationException("Azure OpenAI endpoint is not configured"),
        apiKey: azureApiKey ?? throw new InvalidOperationException("Azure OpenAI API key is not configured"));


await builder.Build().RunAsync();