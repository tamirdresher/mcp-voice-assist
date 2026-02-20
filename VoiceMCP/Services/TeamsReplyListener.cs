using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text;
using System.Text.Json;

namespace VoiceMCP.Services
{
    /// <summary>
    /// Lightweight HTTP listener for receiving Teams reply callbacks from Gateway.
    /// Auto-allocates port starting at 8090 (up to 8190).
    /// </summary>
    public class TeamsReplyListener : IHostedService
    {
        private readonly ITeamsNotificationService _teamsService;
        private readonly string _secret;
        private HttpListener? _listener;
        private Task? _listenerTask;
        private CancellationTokenSource? _cts;
        
        public int Port { get; private set; }
        public string CallbackUrl => $"http://localhost:{Port}/voice-mcp/reply";

        public TeamsReplyListener(ITeamsNotificationService teamsService, string secret)
        {
            _teamsService = teamsService;
            _secret = secret;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Allocate port
            Port = AllocatePort(8090, 8190);
            if (Port == 0)
            {
                Console.Error.WriteLine("TeamsReplyListener: No available ports in range 8090-8190. Two-way Teams communication disabled.");
                return Task.CompletedTask;
            }

            // Start listener
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/voice-mcp/");
            _listener.Start();

            _cts = new CancellationTokenSource();
            _listenerTask = ListenAsync(_cts.Token);

            Console.Error.WriteLine($"TeamsReplyListener started on port {Port}.");
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_listener == null) return;

            _cts?.Cancel();
            _listener.Stop();

            if (_listenerTask != null)
            {
                await _listenerTask;
            }

            Console.Error.WriteLine("TeamsReplyListener stopped.");
        }

        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
                }
                catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"TeamsReplyListener error: {ex.Message}");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                if (request.HttpMethod == "POST" && request.Url?.AbsolutePath == "/voice-mcp/reply")
                {
                    using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                    var body = await reader.ReadToEndAsync();
                    var reply = JsonSerializer.Deserialize<TeamsReply>(body, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (reply != null && !string.IsNullOrEmpty(reply.QuestionId) && !string.IsNullOrEmpty(reply.Answer))
                    {
                        // Validate secret
                        if (reply.Secret != _secret)
                        {
                            Console.Error.WriteLine($"TeamsReplyListener: Invalid secret for question {reply.QuestionId}");
                            response.StatusCode = 401;
                            var unauthorizedText = Encoding.UTF8.GetBytes("{\"error\":\"Invalid secret\"}");
                            await response.OutputStream.WriteAsync(unauthorizedText, 0, unauthorizedText.Length);
                            response.Close();
                            return;
                        }

                        _teamsService.OnReplyReceived(reply.QuestionId, reply.Answer);

                        response.StatusCode = 200;
                        var responseText = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");
                        await response.OutputStream.WriteAsync(responseText, 0, responseText.Length);
                    }
                    else
                    {
                        response.StatusCode = 400;
                        var responseText = Encoding.UTF8.GetBytes("{\"error\":\"Invalid payload\"}");
                        await response.OutputStream.WriteAsync(responseText, 0, responseText.Length);
                    }
                }
                else
                {
                    response.StatusCode = 404;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"TeamsReplyListener request handler error: {ex.Message}");
                response.StatusCode = 500;
            }
            finally
            {
                response.Close();
            }
        }

        private static int AllocatePort(int basePort, int maxPort)
        {
            for (int port = basePort; port <= maxPort; port++)
            {
                if (IsPortAvailable(port))
                {
                    return port;
                }
            }
            return 0;
        }

        private static bool IsPortAvailable(int port)
        {
            try
            {
                using var listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{port}/");
                listener.Start();
                listener.Stop();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private class TeamsReply
        {
            public string QuestionId { get; set; } = string.Empty;
            public string Answer { get; set; } = string.Empty;
            public string? UserId { get; set; }
            public string? UserName { get; set; }
            public string Secret { get; set; } = string.Empty;
        }
    }
}
