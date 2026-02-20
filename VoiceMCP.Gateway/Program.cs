using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VoiceMCP.Gateway.Services;

var port = int.Parse(Environment.GetEnvironmentVariable("GATEWAY_PORT") ?? "8080");
var headless = !string.Equals(Environment.GetEnvironmentVariable("GATEWAY_HEADLESS"), "false", StringComparison.OrdinalIgnoreCase);

Console.Error.WriteLine($"[Gateway] Starting on port {port} (headless: {headless})");

SingleInstanceMutex? mutex = null;
try
{
    mutex = new SingleInstanceMutex();
    Console.Error.WriteLine("[Gateway] Singleton mutex acquired");
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"[Gateway] FATAL: {ex.Message}");
    return 1;
}

var builder = WebApplication.CreateBuilder(args);

var instances = new ConcurrentDictionary<string, InstanceRegistration>();
var questionToInstance = new ConcurrentDictionary<string, string>();

builder.Services.AddSingleton(instances);
builder.Services.AddSingleton(questionToInstance);
builder.Services.AddSingleton(sp => 
{
    return new TeamsPlaywrightPoller(
        sp.GetRequiredService<ILogger<TeamsPlaywrightPoller>>(),
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<ConcurrentDictionary<string, InstanceRegistration>>(),
        sp.GetRequiredService<ConcurrentDictionary<string, string>>(),
        port,
        headless);
});
builder.Services.AddHttpClient();
builder.Services.AddHostedService<HeartbeatCleanupService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TeamsPlaywrightPoller>());

var app = builder.Build();
app.Urls.Add($"http://0.0.0.0:{port}");

var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

app.MapPost("/gateway/register", async (
    HttpContext context,
    ConcurrentDictionary<string, InstanceRegistration> instances) =>
{
    var registration = await JsonSerializer.DeserializeAsync<InstanceRegistration>(context.Request.Body, jsonOptions);
    if (registration == null || 
        string.IsNullOrEmpty(registration.InstanceId) || 
        string.IsNullOrEmpty(registration.CallbackUrl) ||
        string.IsNullOrEmpty(registration.Secret))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { error = "Invalid registration payload (missing instanceId, callbackUrl, or secret)" });
        return;
    }

    registration.LastHeartbeat = DateTime.UtcNow;
    instances[registration.InstanceId] = registration;
    
    Console.Error.WriteLine($"[Gateway] Instance registered: {registration.InstanceId} ({registration.ProjectName}) -> {registration.CallbackUrl}");
    
    context.Response.StatusCode = 200;
    await context.Response.WriteAsJsonAsync(new { ok = true, heartbeatIntervalSec = 30 });
});

app.MapPost("/gateway/heartbeat", async (
    HttpContext context,
    ConcurrentDictionary<string, InstanceRegistration> instances) =>
{
    var payload = await JsonSerializer.DeserializeAsync<HeartbeatPayload>(context.Request.Body, jsonOptions);
    if (payload == null || string.IsNullOrEmpty(payload.InstanceId) || string.IsNullOrEmpty(payload.Secret))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { error = "Invalid heartbeat payload" });
        return;
    }

    if (!instances.TryGetValue(payload.InstanceId, out var instance))
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsJsonAsync(new { error = "Instance not registered" });
        return;
    }

    if (instance.Secret != payload.Secret)
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { error = "Invalid secret" });
        return;
    }

    instance.LastHeartbeat = DateTime.UtcNow;
    context.Response.StatusCode = 200;
    await context.Response.WriteAsJsonAsync(new { ok = true });
});

app.MapDelete("/gateway/instances/{instanceId}", async (
    string instanceId,
    HttpContext context,
    ConcurrentDictionary<string, InstanceRegistration> instances,
    ConcurrentDictionary<string, string> questionToInstance) =>
{
    var secret = context.Request.Headers["X-Gateway-Secret"].FirstOrDefault();
    
    if (instances.TryGetValue(instanceId, out var instance))
    {
        if (instance.Secret != secret)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid secret" });
            return;
        }
        
        instances.TryRemove(instanceId, out _);
        var removedQuestions = questionToInstance.Where(kvp => kvp.Value == instanceId).Select(kvp => kvp.Key).ToList();
        foreach (var qid in removedQuestions)
        {
            questionToInstance.TryRemove(qid, out _);
        }
        
        Console.Error.WriteLine($"[Gateway] Instance unregistered: {instanceId} ({removedQuestions.Count} pending questions removed)");
        context.Response.StatusCode = 200;
        await context.Response.WriteAsJsonAsync(new { ok = true });
    }
    else
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsJsonAsync(new { error = "Instance not found" });
    }
});

app.MapPost("/gateway/questions", async (
    HttpContext context,
    ConcurrentDictionary<string, InstanceRegistration> instances,
    ConcurrentDictionary<string, string> questionToInstance) =>
{
    var registration = await JsonSerializer.DeserializeAsync<QuestionRegistration>(context.Request.Body, jsonOptions);
    if (registration == null || string.IsNullOrEmpty(registration.QuestionId) || string.IsNullOrEmpty(registration.InstanceId))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { error = "Invalid question registration payload" });
        return;
    }

    if (!instances.ContainsKey(registration.InstanceId))
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsJsonAsync(new { error = "Instance not registered" });
        return;
    }

    questionToInstance[registration.QuestionId] = registration.InstanceId;
    Console.Error.WriteLine($"[Gateway] Question registered: {registration.QuestionId} -> {registration.InstanceId}");
    
    var hasPendingQuestions = questionToInstance.Any();
    var estimatedPollIntervalMs = hasPendingQuestions ? 5000 : 30000;
    
    context.Response.StatusCode = 200;
    await context.Response.WriteAsJsonAsync(new { ok = true, estimatedPollIntervalMs });
});

app.MapGet("/gateway/health", async (
    HttpContext context,
    ConcurrentDictionary<string, InstanceRegistration> instances,
    ConcurrentDictionary<string, string> questionToInstance,
    IEnumerable<IHostedService> hostedServices) =>
{
    var poller = hostedServices.OfType<TeamsPlaywrightPoller>().FirstOrDefault();
    var playwrightStatus = poller?.PlaywrightStatus ?? "not_started";
    var uptimeSec = poller?.GetUptimeSeconds() ?? 0;
    
    context.Response.StatusCode = 200;
    await context.Response.WriteAsJsonAsync(new
    {
        status = "ok",
        activeInstances = instances.Count,
        pendingQuestions = questionToInstance.Count,
        playwrightStatus,
        uptimeSec
    });
});

Console.Error.WriteLine("[Gateway] Ready");

try
{
    await app.RunAsync();
}
finally
{
    mutex?.Dispose();
    Console.Error.WriteLine("[Gateway] Shutdown complete");
}

return 0;

public record InstanceRegistration
{
    public string InstanceId { get; set; } = "";
    public string CallbackUrl { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string Secret { get; set; } = "";
    public DateTime LastHeartbeat { get; set; }
}

public record QuestionRegistration
{
    public string QuestionId { get; set; } = "";
    public string InstanceId { get; set; } = "";
}

public record HeartbeatPayload
{
    public string InstanceId { get; set; } = "";
    public string Secret { get; set; } = "";
}
