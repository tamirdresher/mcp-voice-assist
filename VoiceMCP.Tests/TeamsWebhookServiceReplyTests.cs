using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
using VoiceMCP.Services;
using Xunit;

namespace VoiceMCP.Tests
{
    /// <summary>
    /// Tests for the updated TeamsWebhookService with blocking AskQuestionAsync.
    /// Tests the two-way communication flow as defined in Keaton's architecture.
    /// </summary>
    public class TeamsWebhookServiceReplyTests
    {
        private const string TestWebhookUrl = "https://outlook.office.com/webhook/test-guid";
        private const string TestProjectName = "TestProject";
        private const string TestQuestion = "Should I proceed with deployment?";

        private static (TeamsWebhookService service, Mock<IGatewayClient> mockGateway, Mock<HttpMessageHandler> handler) CreateServiceWithGateway(
            HttpStatusCode webhookResponseCode = HttpStatusCode.OK)
        {
            // Mock HttpClient for webhook posts
            var handler = new Mock<HttpMessageHandler>();
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = webhookResponseCode,
                    Content = new StringContent("1")
                });

            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler.Object));

            // Mock IGatewayClient
            var mockGateway = new Mock<IGatewayClient>();
            mockGateway
                .Setup(g => g.RegisterQuestionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var service = new TeamsWebhookService(mockFactory.Object, TestWebhookUrl, TestProjectName, mockGateway.Object);

            return (service, mockGateway, handler);
        }

        [Fact]
        public async Task AskQuestionAsync_GeneratesCorrectCorrelationId()
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

            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler.Object));
            
            var mockGateway = new Mock<IGatewayClient>();
            mockGateway.Setup(g => g.RegisterQuestionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var service = new TeamsWebhookService(mockFactory.Object, TestWebhookUrl, TestProjectName, mockGateway.Object);
            var instanceId = service.InstanceId;

            // Start task but don't await - we'll timeout
            var task = service.AskQuestionAsync(TestQuestion, 1);

            // Give it time to post the card
            await Task.Delay(200);

            Assert.NotNull(capturedBody);
            
            // Verify correlation ID format: q-{instanceId}-{sequenceNumber}
            Assert.Contains($"q-{instanceId}-", capturedBody);
            Assert.Contains("💬 Reply in the thread below", capturedBody);

            try { await task; } catch { /* Expected timeout */ }
        }

        [Fact]
        public async Task AskQuestionAsync_RegistersQuestionWithGateway()
        {
            var (service, mockGateway, _) = CreateServiceWithGateway();

            string? registeredQuestionId = null;
            string? registeredInstanceId = null;

            mockGateway
                .Setup(g => g.RegisterQuestionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((qId, instId) =>
                {
                    registeredQuestionId = qId;
                    registeredInstanceId = instId;
                })
                .Returns(Task.CompletedTask);

            var task = service.AskQuestionAsync(TestQuestion, 1);
            await Task.Delay(200);

            Assert.NotNull(registeredQuestionId);
            Assert.Equal(service.InstanceId, registeredInstanceId);
            Assert.StartsWith($"q-{service.InstanceId}-", registeredQuestionId);

            try { await task; } catch { /* Expected timeout */ }
        }

        [Fact]
        public async Task AskQuestionAsync_PostsCardWithReplyTemplate()
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

            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler.Object));
            
            var mockGateway = new Mock<IGatewayClient>();
            mockGateway.Setup(g => g.RegisterQuestionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var service = new TeamsWebhookService(mockFactory.Object, TestWebhookUrl, TestProjectName, mockGateway.Object);

            var task = service.AskQuestionAsync(TestQuestion, 1);
            await Task.Delay(200);

            Assert.NotNull(capturedBody);
            Assert.Contains("Question", capturedBody);
            Assert.Contains(TestQuestion, capturedBody);
            Assert.Contains(TestProjectName, capturedBody);
            Assert.Contains("AdaptiveCard", capturedBody);

            try { await task; } catch { /* Expected timeout */ }
        }

        [Fact]
        public async Task AskQuestionAsync_BlocksUntilReplyReceived()
        {
            var (service, mockGateway, _) = CreateServiceWithGateway();

            string capturedQuestionId = "";
            mockGateway
                .Setup(g => g.RegisterQuestionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((qId, _) => capturedQuestionId = qId)
                .Returns(Task.CompletedTask);

            var askTask = service.AskQuestionAsync(TestQuestion, 10);
            
            // Give time for card to post
            await Task.Delay(200);
            
            // Simulate reply from Gateway
            string testAnswer = "yes, proceed";
            service.OnReplyReceived(capturedQuestionId, testAnswer);

            // Should unblock and return answer
            var result = await askTask;
            Assert.Equal(testAnswer, result);
        }

        [Fact]
        public async Task AskQuestionAsync_TimesOutAfterSpecifiedDuration()
        {
            var (service, _, _) = CreateServiceWithGateway();

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            var result = await service.AskQuestionAsync(TestQuestion, 2);
            
            stopwatch.Stop();

            // Should timeout and return timeout message
            Assert.Contains("Timeout", result);
            Assert.True(stopwatch.ElapsedMilliseconds >= 1800);
            Assert.True(stopwatch.ElapsedMilliseconds < 3000);
        }

        [Fact]
        public async Task OnReplyReceived_ResolvesCorrectPendingQuestion()
        {
            var (service, mockGateway, _) = CreateServiceWithGateway();

            string questionId1 = "";
            string questionId2 = "";

            mockGateway
                .Setup(g => g.RegisterQuestionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((qId, _) =>
                {
                    if (string.IsNullOrEmpty(questionId1))
                        questionId1 = qId;
                    else
                        questionId2 = qId;
                })
                .Returns(Task.CompletedTask);

            // Start two questions
            var task1 = service.AskQuestionAsync("Question 1", 10);
            await Task.Delay(100);
            var task2 = service.AskQuestionAsync("Question 2", 10);
            await Task.Delay(100);

            // Answer second question first
            service.OnReplyReceived(questionId2, "answer 2");
            var result2 = await task2;
            Assert.Equal("answer 2", result2);
            Assert.False(task1.IsCompleted);

            // Answer first question
            service.OnReplyReceived(questionId1, "answer 1");
            var result1 = await task1;
            Assert.Equal("answer 1", result1);
        }

        [Fact]
        public void OnReplyReceived_IgnoresUnknownQuestionId()
        {
            var (service, _, _) = CreateServiceWithGateway();

            // Should not throw
            service.OnReplyReceived("q-unknown-999", "some answer");
        }

        [Fact]
        public async Task OnReplyReceived_IgnoresReplyAfterTimeout()
        {
            var (service, mockGateway, _) = CreateServiceWithGateway();

            string capturedQuestionId = "";
            mockGateway
                .Setup(g => g.RegisterQuestionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((qId, _) => capturedQuestionId = qId)
                .Returns(Task.CompletedTask);

            var result = await service.AskQuestionAsync(TestQuestion, 1);
            
            await Task.Delay(200);

            // Reply arrives after timeout - should be ignored (no exception)
            service.OnReplyReceived(capturedQuestionId, "late answer");
            
            Assert.Contains("Timeout", result);
        }

        [Fact]
        public async Task MultipleConcurrentQuestions_HandleIndependently()
        {
            var (service, mockGateway, _) = CreateServiceWithGateway();

            var questionIds = new ConcurrentBag<string>();
            mockGateway
                .Setup(g => g.RegisterQuestionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((qId, _) => questionIds.Add(qId))
                .Returns(Task.CompletedTask);

            // Start 5 concurrent questions
            var tasks = new List<Task<string?>>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(service.AskQuestionAsync($"Question {i}", 10));
                await Task.Delay(50);
            }

            // Answer them in reverse order
            var qIdList = questionIds.ToList();
            for (int i = qIdList.Count - 1; i >= 0; i--)
            {
                service.OnReplyReceived(qIdList[i], $"answer {i}");
            }

            // All should complete successfully
            var results = await Task.WhenAll(tasks);
            Assert.Equal(5, results.Length);
            Assert.All(results, r => Assert.StartsWith("answer", r!));
        }

        [Fact]
        public async Task CorrelationId_IncrementsSequenceNumber()
        {
            var capturedBodies = new List<string>();
            var handler = new Mock<HttpMessageHandler>();
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
                {
                    var body = await req.Content!.ReadAsStringAsync();
                    capturedBodies.Add(body);
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler.Object));
            
            var mockGateway = new Mock<IGatewayClient>();
            mockGateway.Setup(g => g.RegisterQuestionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var service = new TeamsWebhookService(mockFactory.Object, TestWebhookUrl, TestProjectName, mockGateway.Object);
            var instanceId = service.InstanceId;

            // Post 3 questions
            var tasks = new List<Task<string?>>();
            for (int i = 0; i < 3; i++)
            {
                tasks.Add(service.AskQuestionAsync($"Question {i}", 1));
                await Task.Delay(100);
            }

            // Verify sequence numbers increment
            Assert.Contains($"q-{instanceId}-001", capturedBodies[0]);
            Assert.Contains($"q-{instanceId}-002", capturedBodies[1]);
            Assert.Contains($"q-{instanceId}-003", capturedBodies[2]);

            try { await Task.WhenAll(tasks); } catch { /* Expected timeouts */ }
        }

        [Fact]
        public async Task AskQuestionAsync_ReturnsFailureMessageOnWebhookPostFailure()
        {
            var (service, _, _) = CreateServiceWithGateway(HttpStatusCode.InternalServerError);

            // Should return failure message if webhook post fails, not throw
            var result = await service.AskQuestionAsync(TestQuestion, 5);
            Assert.Contains("Failed", result);
        }
    }
}

