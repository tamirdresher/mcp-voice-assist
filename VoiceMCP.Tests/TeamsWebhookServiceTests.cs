using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
using VoiceMCP.Services;
using Xunit;

namespace VoiceMCP.Tests
{
    public class TeamsWebhookServiceTests
    {
        private const string TestWebhookUrl = "https://outlook.office.com/webhook/test-guid";
        private const string TestProjectName = "TestProject";

        private static (TeamsWebhookService service, Mock<HttpMessageHandler> handler) CreateService(
            HttpStatusCode responseCode = HttpStatusCode.OK,
            string responseBody = "1")
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
            var service = new TeamsWebhookService(httpClient, TestWebhookUrl, TestProjectName);
            return (service, handler);
        }

        #region SendNotificationAsync

        [Fact]
        public async Task SendNotificationAsync_PostsToWebhookUrl()
        {
            var (service, handler) = CreateService();

            await service.SendNotificationAsync("Hello Teams");

            handler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == TestWebhookUrl),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task SendNotificationAsync_SendsAdaptiveCardJson()
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
            var service = new TeamsWebhookService(httpClient, TestWebhookUrl, TestProjectName);

            await service.SendNotificationAsync("Build succeeded");

            Assert.NotNull(capturedBody);
            Assert.Contains("AdaptiveCard", capturedBody);
            Assert.Contains("application/vnd.microsoft.card.adaptive", capturedBody);
        }

        [Fact]
        public async Task SendNotificationAsync_IncludesProjectName()
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
            var service = new TeamsWebhookService(httpClient, TestWebhookUrl, TestProjectName);

            await service.SendNotificationAsync("Test message");

            Assert.NotNull(capturedBody);
            Assert.Contains(TestProjectName, capturedBody);
        }

        [Fact]
        public async Task SendNotificationAsync_WithTitle_IncludesTitleInCard()
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
            var service = new TeamsWebhookService(httpClient, TestWebhookUrl, TestProjectName);

            await service.SendNotificationAsync("Deploy complete", "Deployment Status");

            Assert.NotNull(capturedBody);
            Assert.Contains("Deployment Status", capturedBody);
        }

        [Fact]
        public async Task SendNotificationAsync_WithoutTitle_UsesDefaultTitle()
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
            var service = new TeamsWebhookService(httpClient, TestWebhookUrl, TestProjectName);

            await service.SendNotificationAsync("Test message");

            Assert.NotNull(capturedBody);
            // Default title is "Notification" per implementation
            Assert.Contains("Notification", capturedBody);
        }

        [Fact]
        public async Task SendNotificationAsync_IncludesMessage()
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
            var service = new TeamsWebhookService(httpClient, TestWebhookUrl, TestProjectName);

            await service.SendNotificationAsync("All tests passed — 247/247 green");

            Assert.NotNull(capturedBody);
            Assert.Contains("All tests passed", capturedBody);
        }

        [Fact]
        public async Task SendNotificationAsync_SetsContentTypeJson()
        {
            string? capturedContentType = null;
            var handler = new Mock<HttpMessageHandler>();
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
                {
                    capturedContentType = req.Content!.Headers.ContentType?.MediaType;
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var httpClient = new HttpClient(handler.Object);
            var service = new TeamsWebhookService(httpClient, TestWebhookUrl, TestProjectName);

            await service.SendNotificationAsync("Test");

            Assert.Equal("application/json", capturedContentType);
        }

        #endregion

        #region HTTP Error Handling

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        public async Task SendNotificationAsync_HttpError_DoesNotThrow(HttpStatusCode statusCode)
        {
            // The implementation catches exceptions and returns false via PostCardAsync.
            // SendNotificationAsync doesn't return bool, but it should not throw.
            var (service, _) = CreateService(statusCode, "error");

            // Should not throw — PostCardAsync catches and logs
            var exception = await Record.ExceptionAsync(() =>
                service.SendNotificationAsync("Test message"));

            Assert.Null(exception);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task AskQuestionAsync_HttpError_ReturnsFailureMessage(HttpStatusCode statusCode)
        {
            var (service, _) = CreateService(statusCode, "error");

            var result = await service.AskQuestionAsync("What's the status?");

            Assert.Contains("Failed", result);
        }

        #endregion

        #region AskQuestionAsync

        [Fact]
        public async Task AskQuestionAsync_Success_ReturnsConfirmationWithQuestion()
        {
            var (service, _) = CreateService();

            var result = await service.AskQuestionAsync("What's the ETA?");

            Assert.Contains("What's the ETA?", result);
            Assert.Contains(TestProjectName, result);
        }

        [Fact]
        public async Task AskQuestionAsync_Success_PostsToWebhook()
        {
            var (service, handler) = CreateService();

            await service.AskQuestionAsync("Need approval?");

            handler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == TestWebhookUrl),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task AskQuestionAsync_IncludesQuestionInCard()
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
            var service = new TeamsWebhookService(httpClient, TestWebhookUrl, TestProjectName);

            await service.AskQuestionAsync("Should I deploy to prod?");

            Assert.NotNull(capturedBody);
            Assert.Contains("Should I deploy to prod?", capturedBody);
        }

        #endregion

        #region Network Exceptions

        [Fact]
        public async Task SendNotificationAsync_NetworkException_DoesNotThrow()
        {
            var handler = new Mock<HttpMessageHandler>();
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network unreachable"));

            var httpClient = new HttpClient(handler.Object);
            var service = new TeamsWebhookService(httpClient, TestWebhookUrl, TestProjectName);

            // PostCardAsync catches all exceptions, so this should not throw
            var exception = await Record.ExceptionAsync(() =>
                service.SendNotificationAsync("Test"));

            Assert.Null(exception);
        }

        [Fact]
        public async Task AskQuestionAsync_NetworkException_ReturnsFailureMessage()
        {
            var handler = new Mock<HttpMessageHandler>();
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("DNS resolution failed"));

            var httpClient = new HttpClient(handler.Object);
            var service = new TeamsWebhookService(httpClient, TestWebhookUrl, TestProjectName);

            var result = await service.AskQuestionAsync("Test question");

            Assert.Contains("Failed", result);
        }

        #endregion

        #region Adaptive Card Structure

        [Fact]
        public async Task SendNotificationAsync_CardContainsAdaptiveCardSchema()
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
            var service = new TeamsWebhookService(httpClient, TestWebhookUrl, TestProjectName);

            await service.SendNotificationAsync("Schema test");

            Assert.NotNull(capturedBody);
            Assert.Contains("$schema", capturedBody);
            Assert.Contains("adaptivecards.io", capturedBody);
        }

        [Fact]
        public async Task SendNotificationAsync_CardContainsVersion()
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
            var service = new TeamsWebhookService(httpClient, TestWebhookUrl, TestProjectName);

            await service.SendNotificationAsync("Version test");

            Assert.NotNull(capturedBody);
            Assert.Contains("1.4", capturedBody);
        }

        #endregion
    }
}
