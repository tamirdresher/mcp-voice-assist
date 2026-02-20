using Microsoft.Extensions.Hosting;

namespace VoiceMCP.Services
{
    /// <summary>
    /// Hosted service that sends periodic heartbeats to the Gateway to maintain session registration.
    /// </summary>
    public class HeartbeatService : BackgroundService
    {
        private readonly IGatewayClient _gatewayClient;
        private readonly ITeamsNotificationService _teamsService;
        private readonly string _secret;
        private string? _instanceId;
        private readonly int _intervalSeconds;

        public HeartbeatService(
            IGatewayClient gatewayClient,
            ITeamsNotificationService teamsService,
            string secret,
            int intervalSeconds = 30)
        {
            _gatewayClient = gatewayClient;
            _teamsService = teamsService;
            _secret = secret;
            _intervalSeconds = intervalSeconds;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Get instanceId from TeamsWebhookService
            if (_teamsService is TeamsWebhookService webhookService)
            {
                _instanceId = webhookService.InstanceId;
            }
            else
            {
                Console.Error.WriteLine("HeartbeatService: TeamsService is not TeamsWebhookService. Heartbeat disabled.");
                return;
            }

            // Wait a bit for registration to complete
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _gatewayClient.HeartbeatAsync(_instanceId, _secret);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Heartbeat failed: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
            }
        }
    }
}
