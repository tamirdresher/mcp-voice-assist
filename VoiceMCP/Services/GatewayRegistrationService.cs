using Microsoft.Extensions.Hosting;

namespace VoiceMCP.Services
{
    /// <summary>
    /// Hosted service that registers/unregisters this VoiceMCP instance with the Gateway.
    /// </summary>
    public class GatewayRegistrationService : IHostedService
    {
        private readonly IGatewayClient _gatewayClient;
        private readonly TeamsReplyListener _replyListener;
        private readonly ITeamsNotificationService _teamsService;
        private readonly string _secret;
        private string? _instanceId;

        public GatewayRegistrationService(
            IGatewayClient gatewayClient, 
            TeamsReplyListener replyListener,
            ITeamsNotificationService teamsService,
            string secret)
        {
            _gatewayClient = gatewayClient;
            _replyListener = replyListener;
            _teamsService = teamsService;
            _secret = secret;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Wait for listener to allocate port
            await Task.Delay(100, cancellationToken);

            if (_replyListener.Port == 0)
            {
                Console.Error.WriteLine("GatewayRegistrationService: Reply listener failed to start, skipping registration.");
                return;
            }

            // Get instanceId from TeamsWebhookService
            if (_teamsService is TeamsWebhookService webhookService)
            {
                _instanceId = webhookService.InstanceId;
            }
            else
            {
                Console.Error.WriteLine("GatewayRegistrationService: TeamsService is not TeamsWebhookService.");
                return;
            }

            var projectName = Path.GetFileName(Path.GetFullPath("."));

            try
            {
                await _gatewayClient.RegisterInstanceAsync(_instanceId, _replyListener.CallbackUrl, projectName, _secret);
                Console.Error.WriteLine($"Registered instance {_instanceId} with Gateway at {_replyListener.CallbackUrl}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Gateway registration failed: {ex.Message}");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_instanceId))
            {
                try
                {
                    await _gatewayClient.UnregisterInstanceAsync(_instanceId);
                    Console.Error.WriteLine($"Unregistered instance {_instanceId} from Gateway.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Gateway unregistration failed: {ex.Message}");
                }
            }
        }
    }
}
