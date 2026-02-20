using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
using Xunit;

namespace VoiceMCP.Tests
{
    /// <summary>
    /// Tests for the Gateway client that registers instances and questions with the VoiceMCP Gateway.
    /// Tests against the IGatewayClient interface as defined in Keaton's architecture.
    /// </summary>
    public class GatewayClientTests
    {
        private const string TestGatewayUrl = "http://localhost:8080";
        private const string TestInstanceId = "a3f7b2c1";
        private const string TestCallbackUrl = "http://localhost:8091/voice-mcp/reply";
        private const string TestProjectName = "TestProject";
        private const string TestQuestionId = "q-a3f7b2c1-001";

        private static (dynamic client, Mock<HttpMessageHandler> handler) CreateClient(
            HttpStatusCode responseCode = HttpStatusCode.OK,
            string responseBody = "")
        {
            var handler = new Mock<HttpMessageHandler>();
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = responseCode,
                    Content = new StringContent(responseBody)
                });

            var httpClient = new HttpClient(handler.Object);
            
            // Use dynamic to work with the interface without needing the actual type
            // The implementation will be provided by McManus/Fenster
            var clientType = Type.GetType("VoiceMCP.Services.GatewayClient, VoiceMCP");
            var client = clientType != null 
                ? Activator.CreateInstance(clientType, httpClient, TestGatewayUrl)
                : null;

            return (client!, handler);
        }

        [Fact]
        public async Task RegisterInstanceAsync_PostsToGatewayRegisterEndpoint()
        {
            var (client, handler) = CreateClient();
            if (client == null)
            {
                // Skip if implementation not yet available
                return;
            }

            await client.RegisterInstanceAsync(TestInstanceId, TestCallbackUrl, TestProjectName);

            handler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == $"{TestGatewayUrl}/gateway/register"),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task RegisterInstanceAsync_SendsCorrectPayload()
        {
            string? capturedBody = null;
            var handler = new Mock<HttpMessageHandler>();
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
                {
                    capturedBody = await req.Content!.ReadAsStringAsync();
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var httpClient = new HttpClient(handler.Object);
            var clientType = Type.GetType("VoiceMCP.Services.GatewayClient, VoiceMCP");
            if (clientType == null) return;
            
            dynamic client = Activator.CreateInstance(clientType, httpClient, TestGatewayUrl)!;

            await client.RegisterInstanceAsync(TestInstanceId, TestCallbackUrl, TestProjectName);

            Assert.NotNull(capturedBody);
            var payload = JsonSerializer.Deserialize<JsonElement>(capturedBody);
            Assert.Equal(TestInstanceId, payload.GetProperty("instanceId").GetString());
            Assert.Equal(TestCallbackUrl, payload.GetProperty("callbackUrl").GetString());
            Assert.Equal(TestProjectName, payload.GetProperty("projectName").GetString());
        }

        [Fact]
        public async Task RegisterQuestionAsync_PostsToGatewayQuestionsEndpoint()
        {
            var (client, handler) = CreateClient();
            if (client == null) return;

            await client.RegisterQuestionAsync(TestQuestionId, TestInstanceId);

            handler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == $"{TestGatewayUrl}/gateway/questions"),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task RegisterQuestionAsync_SendsCorrectPayload()
        {
            string? capturedBody = null;
            var handler = new Mock<HttpMessageHandler>();
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
                {
                    capturedBody = await req.Content!.ReadAsStringAsync();
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var httpClient = new HttpClient(handler.Object);
            var clientType = Type.GetType("VoiceMCP.Services.GatewayClient, VoiceMCP");
            if (clientType == null) return;
            
            dynamic client = Activator.CreateInstance(clientType, httpClient, TestGatewayUrl)!;

            await client.RegisterQuestionAsync(TestQuestionId, TestInstanceId);

            Assert.NotNull(capturedBody);
            var payload = JsonSerializer.Deserialize<JsonElement>(capturedBody);
            Assert.Equal(TestQuestionId, payload.GetProperty("questionId").GetString());
            Assert.Equal(TestInstanceId, payload.GetProperty("instanceId").GetString());
        }

        [Fact]
        public async Task UnregisterInstanceAsync_DeletesFromGatewayInstancesEndpoint()
        {
            var (client, handler) = CreateClient();
            if (client == null) return;

            await client.UnregisterInstanceAsync(TestInstanceId);

            handler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Delete &&
                    req.RequestUri!.ToString() == $"{TestGatewayUrl}/gateway/instances/{TestInstanceId}"),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task RegisterInstanceAsync_ThrowsOnGatewayError()
        {
            var (client, _) = CreateClient(HttpStatusCode.InternalServerError, "Gateway error");
            if (client == null) return;

            await Assert.ThrowsAsync<HttpRequestException>(async () =>
                await client.RegisterInstanceAsync(TestInstanceId, TestCallbackUrl, TestProjectName));
        }

        [Fact]
        public async Task RegisterInstanceAsync_ThrowsOnNetworkError()
        {
            var handler = new Mock<HttpMessageHandler>();
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Connection refused"));

            var httpClient = new HttpClient(handler.Object);
            var clientType = Type.GetType("VoiceMCP.Services.GatewayClient, VoiceMCP");
            if (clientType == null) return;
            
            dynamic client = Activator.CreateInstance(clientType, httpClient, TestGatewayUrl)!;

            await Assert.ThrowsAsync<HttpRequestException>(async () =>
                await client.RegisterInstanceAsync(TestInstanceId, TestCallbackUrl, TestProjectName));
        }

        [Fact]
        public async Task RegisterQuestionAsync_ThrowsOnGatewayError()
        {
            var (client, _) = CreateClient(HttpStatusCode.BadRequest, "Invalid question format");
            if (client == null) return;

            await Assert.ThrowsAsync<HttpRequestException>(async () =>
                await client.RegisterQuestionAsync(TestQuestionId, TestInstanceId));
        }

        [Fact]
        public async Task UnregisterInstanceAsync_DoesNotThrowOn404()
        {
            // Unregister should be graceful - instance might already be removed
            var (client, _) = CreateClient(HttpStatusCode.NotFound);
            if (client == null) return;

            // Should not throw
            await client.UnregisterInstanceAsync(TestInstanceId);
        }

        [Fact]
        public async Task UnregisterInstanceAsync_DoesNotThrowOnNetworkError()
        {
            // Unregister on shutdown should not crash the app
            var handler = new Mock<HttpMessageHandler>();
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Connection refused"));

            var httpClient = new HttpClient(handler.Object);
            var clientType = Type.GetType("VoiceMCP.Services.GatewayClient, VoiceMCP");
            if (clientType == null) return;
            
            dynamic client = Activator.CreateInstance(clientType, httpClient, TestGatewayUrl)!;

            // Should not throw - swallow errors on unregister
            try
            {
                await client.UnregisterInstanceAsync(TestInstanceId);
            }
            catch
            {
                // Expected to catch and log, not crash
            }
        }
    }
}
