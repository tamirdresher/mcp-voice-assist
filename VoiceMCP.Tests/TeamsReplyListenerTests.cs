using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Moq;
using VoiceMCP.Services;
using Xunit;

namespace VoiceMCP.Tests
{
    /// <summary>
    /// Tests for the HTTP reply listener that receives callbacks from the Gateway.
    /// Tests against the TeamsReplyListener as defined in Keaton's architecture.
    /// </summary>
    public class TeamsReplyListenerTests
    {
        private const int TestPort = 8091;
        private const string TestQuestionId = "q-a3f7b2c1-001";
        private const string TestAnswer = "yes please";
        private const string TestUserId = "john.doe@company.com";
        private const string TestUserName = "John Doe";
        private const string TestSecret = "test-secret-123";

        private static bool IsPortAvailable(int port)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        [Fact]
        public async Task StartAsync_StartsListenerOnSpecifiedPort()
        {
            // Find an available port
            int availablePort = TestPort;
            while (!IsPortAvailable(availablePort) && availablePort < TestPort + 100)
            {
                availablePort++;
            }

            var mockService = new Mock<ITeamsNotificationService>();
            var listener = new TeamsReplyListener(mockService.Object, TestSecret);
            
            var cts = new CancellationTokenSource();
            await listener.StartAsync(cts.Token);

            // Verify port is now in use
            Assert.False(IsPortAvailable(listener.Port));

            await listener.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task HandleReply_ForwardsToTeamsService()
        {
            int availablePort = TestPort;
            while (!IsPortAvailable(availablePort) && availablePort < TestPort + 100)
            {
                availablePort++;
            }

            string? capturedQuestionId = null;
            string? capturedAnswer = null;

            // Create mock service that captures OnReplyReceived calls
            var mockService = new Mock<ITeamsNotificationService>();
            mockService
                .Setup(s => s.OnReplyReceived(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((qId, ans) =>
                {
                    capturedQuestionId = qId;
                    capturedAnswer = ans;
                });

            var listener = new TeamsReplyListener(mockService.Object, TestSecret);
            
            await listener.StartAsync(CancellationToken.None);

            // Send POST to listener
            using var httpClient = new HttpClient();
            var payload = new
            {
                questionId = TestQuestionId,
                answer = TestAnswer,
                userId = TestUserId,
                userName = TestUserName,
                secret = TestSecret
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"http://localhost:{listener.Port}/voice-mcp/reply", content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(TestQuestionId, capturedQuestionId);
            Assert.Equal(TestAnswer, capturedAnswer);

            await listener.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task HandleReply_RejectsMalformedPayload()
        {
            int availablePort = TestPort + 10;
            while (!IsPortAvailable(availablePort) && availablePort < TestPort + 110)
            {
                availablePort++;
            }

            var mockService = new Mock<ITeamsNotificationService>();
            var listener = new TeamsReplyListener(mockService.Object, TestSecret);
            
            await listener.StartAsync(CancellationToken.None);

            using var httpClient = new HttpClient();
            var invalidJson = "{ this is not valid json }";
            var content = new StringContent(invalidJson, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"http://localhost:{listener.Port}/voice-mcp/reply", content);

            // Should return 400 Bad Request or 500 for malformed JSON
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest || 
                       response.StatusCode == HttpStatusCode.InternalServerError);

            await listener.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task HandleReply_RequiresQuestionIdField()
        {
            int availablePort = TestPort + 20;
            while (!IsPortAvailable(availablePort) && availablePort < TestPort + 120)
            {
                availablePort++;
            }

            var mockService = new Mock<ITeamsNotificationService>();
            var listener = new TeamsReplyListener(mockService.Object, TestSecret);
            
            await listener.StartAsync(CancellationToken.None);

            using var httpClient = new HttpClient();
            var payload = new { answer = TestAnswer, secret = TestSecret }; // Missing questionId
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"http://localhost:{listener.Port}/voice-mcp/reply", content);

            // Should fail validation
            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);

            await listener.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task StopAsync_StopsListener()
        {
            int availablePort = TestPort + 30;
            while (!IsPortAvailable(availablePort) && availablePort < TestPort + 130)
            {
                availablePort++;
            }

            var mockService = new Mock<ITeamsNotificationService>();
            var listener = new TeamsReplyListener(mockService.Object, TestSecret);
            
            await listener.StartAsync(CancellationToken.None);
            Assert.False(IsPortAvailable(listener.Port));

            await listener.StopAsync(CancellationToken.None);

            // Port should be released (with small delay for cleanup)
            await Task.Delay(100);
            Assert.True(IsPortAvailable(listener.Port));
        }

        [Fact]
        public void PortAllocation_FindsAvailablePort()
        {
            // Test the port allocation logic specified in architecture
            int basePort = 8090;
            int maxAttempts = 100;
            int assignedPort = 0;

            for (int i = 0; i < maxAttempts; i++)
            {
                int candidatePort = basePort + i;
                if (IsPortAvailable(candidatePort))
                {
                    assignedPort = candidatePort;
                    break;
                }
            }

            Assert.NotEqual(0, assignedPort);
            Assert.True(assignedPort >= basePort && assignedPort < basePort + maxAttempts);
        }

        [Fact]
        public async Task HandleReply_AcceptsOptionalUserFields()
        {
            int availablePort = TestPort + 40;
            while (!IsPortAvailable(availablePort) && availablePort < TestPort + 140)
            {
                availablePort++;
            }

            var mockService = new Mock<ITeamsNotificationService>();
            var listener = new TeamsReplyListener(mockService.Object, TestSecret);
            
            await listener.StartAsync(CancellationToken.None);

            using var httpClient = new HttpClient();
            var payloadWithoutUser = new
            {
                questionId = TestQuestionId,
                answer = TestAnswer,
                secret = TestSecret
                // userId and userName omitted
            };
            var json = JsonSerializer.Serialize(payloadWithoutUser);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"http://localhost:{listener.Port}/voice-mcp/reply", content);

            // Should still accept - user fields are optional
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            await listener.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task HandleReply_ReturnsOkImmediately()
        {
            int availablePort = TestPort + 50;
            while (!IsPortAvailable(availablePort) && availablePort < TestPort + 150)
            {
                availablePort++;
            }

            var mockService = new Mock<ITeamsNotificationService>();
            var listener = new TeamsReplyListener(mockService.Object, TestSecret);
            
            await listener.StartAsync(CancellationToken.None);

            using var httpClient = new HttpClient();
            var payload = new
            {
                questionId = TestQuestionId,
                answer = TestAnswer,
                secret = TestSecret
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await httpClient.PostAsync($"http://localhost:{listener.Port}/voice-mcp/reply", content);
            stopwatch.Stop();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            // Should respond quickly (< 1 second)
            Assert.True(stopwatch.ElapsedMilliseconds < 1000);

            await listener.StopAsync(CancellationToken.None);
        }
    }
}
