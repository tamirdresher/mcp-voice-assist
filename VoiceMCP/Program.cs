using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using VoiceMCP.Services;
using VoiceMCP.Tools;

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


var mcpBuilder = builder.Services
    .AddMcpServer()
    .WithStdioServerTransport();

// Conditionally register Teams services and tools
var teamsWebhookUrl = Environment.GetEnvironmentVariable("TEAMS_WEBHOOK_URL");
if (!string.IsNullOrEmpty(teamsWebhookUrl))
{
    var projectName = Path.GetFileName(Path.GetFullPath("."));
    
    // Register Teams service
    builder.Services.AddSingleton<ITeamsNotificationService>(sp =>
        new TeamsWebhookService(
            new HttpClient(),
            teamsWebhookUrl,
            projectName));
    
    mcpBuilder.WithTools<TeamsTools>();
    Console.Error.WriteLine($"Teams integration enabled for project '{projectName}'.");
}
else
{
    Console.Error.WriteLine("Teams webhook URL not configured. Teams tools will not be available.");
}

// Conditionally register Voice services and tools
var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
var azureApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
var azureTtsDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_TTS_DEPLOYMENT");
var azureWhisperDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_WHISPER_DEPLOYMENT");

var configuration = builder.Configuration;
if (string.IsNullOrEmpty(azureEndpoint))
{
    var configEndpoint = configuration["AzureOpenAI:Endpoint"];
    var configApiKey = configuration["AzureOpenAI:ApiKey"];

    if (!string.IsNullOrWhiteSpace(configEndpoint) && !string.IsNullOrWhiteSpace(configApiKey))
    {
        azureEndpoint = configEndpoint;
        azureApiKey = configApiKey;
        azureTtsDeployment = configuration["AzureOpenAI:TtsDeploymentName"];
        azureWhisperDeployment = configuration["AzureOpenAI:WhisperDeploymentName"];
        Console.Error.WriteLine("Loaded Azure OpenAI credentials from user secrets/appsettings.json");
    }
}
else
{
    Console.Error.WriteLine("Loaded Azure OpenAI credentials from environment variables");
}

if (!string.IsNullOrEmpty(azureEndpoint) && !string.IsNullOrEmpty(azureApiKey)
    && !string.IsNullOrEmpty(azureTtsDeployment) && !string.IsNullOrEmpty(azureWhisperDeployment))
{
    builder.Services.AddSingleton<IVoiceService, SemanticKernelVoiceService>();
    builder.Services.AddKernel()
        .AddAzureOpenAITextToAudio(
             deploymentName: azureTtsDeployment,
             endpoint: azureEndpoint,
             apiKey: azureApiKey)
        .AddAzureOpenAIAudioToText(
            deploymentName: azureWhisperDeployment,
            endpoint: azureEndpoint,
            apiKey: azureApiKey);
    mcpBuilder.WithTools<AskUserTool>();
    Console.Error.WriteLine("Voice tools enabled.");
}
else
{
    Console.Error.WriteLine("Azure OpenAI credentials not configured. Voice tools will not be available.");
}


await builder.Build().RunAsync();