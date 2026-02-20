using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VoiceMCP.Gateway.Services;

public class HeartbeatCleanupService : BackgroundService
{
    private readonly ILogger<HeartbeatCleanupService> _logger;
    private readonly ConcurrentDictionary<string, InstanceRegistration> _instances;
    private readonly ConcurrentDictionary<string, string> _questionToInstance;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(90);
    
    public HeartbeatCleanupService(
        ILogger<HeartbeatCleanupService> logger,
        ConcurrentDictionary<string, InstanceRegistration> instances,
        ConcurrentDictionary<string, string> questionToInstance)
    {
        _logger = logger;
        _instances = instances;
        _questionToInstance = questionToInstance;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Heartbeat] Cleanup service started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
                
                var now = DateTime.UtcNow;
                var staleInstances = _instances
                    .Where(kvp => now - kvp.Value.LastHeartbeat > _heartbeatTimeout)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var instanceId in staleInstances)
                {
                    if (_instances.TryRemove(instanceId, out var instance))
                    {
                        var removedQuestions = _questionToInstance
                            .Where(kvp => kvp.Value == instanceId)
                            .Select(kvp => kvp.Key)
                            .ToList();
                        
                        foreach (var qid in removedQuestions)
                        {
                            _questionToInstance.TryRemove(qid, out _);
                        }
                        
                        _logger.LogInformation(
                            $"[Heartbeat] Evicted stale instance: {instanceId} ({instance.ProjectName}) - " +
                            $"{removedQuestions.Count} pending questions removed");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Heartbeat] Error during cleanup cycle");
            }
        }
        
        _logger.LogInformation("[Heartbeat] Cleanup service stopped");
    }
}
