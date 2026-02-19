using Moq;
using VoiceMCP.Services;
using VoiceMCP.Tools;
using Xunit;

namespace VoiceMCP.Tests
{
    public class TeamsToolsTests
    {
        private readonly Mock<ITeamsNotificationService> _mockService;
        private readonly TeamsTools _tools;

        public TeamsToolsTests()
        {
            _mockService = new Mock<ITeamsNotificationService>();
            _tools = new TeamsTools(_mockService.Object);
        }

        #region NotifyViaTeams

        [Fact]
        public async Task NotifyViaTeams_DelegatesToService()
        {
            _mockService
                .Setup(s => s.SendNotificationAsync("Build done", null))
                .Returns(Task.CompletedTask);

            await _tools.NotifyViaTeams("Build done");

            _mockService.Verify(
                s => s.SendNotificationAsync("Build done", null),
                Times.Once);
        }

        [Fact]
        public async Task NotifyViaTeams_PassesTitleToService()
        {
            _mockService
                .Setup(s => s.SendNotificationAsync("Deploy done", "Deployment"))
                .Returns(Task.CompletedTask);

            await _tools.NotifyViaTeams("Deploy done", "Deployment");

            _mockService.Verify(
                s => s.SendNotificationAsync("Deploy done", "Deployment"),
                Times.Once);
        }

        [Fact]
        public async Task NotifyViaTeams_Success_ReturnsSuccessMessage()
        {
            _mockService
                .Setup(s => s.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .Returns(Task.CompletedTask);

            var result = await _tools.NotifyViaTeams("Test");

            Assert.Contains("successfully", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task NotifyViaTeams_ServiceThrows_ReturnsErrorMessage()
        {
            _mockService
                .Setup(s => s.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .ThrowsAsync(new HttpRequestException("Connection refused"));

            var result = await _tools.NotifyViaTeams("Test");

            Assert.Contains("Failed", result);
            Assert.Contains("Connection refused", result);
        }

        [Fact]
        public async Task NotifyViaTeams_ServiceThrows_DoesNotPropagateException()
        {
            _mockService
                .Setup(s => s.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .ThrowsAsync(new InvalidOperationException("Boom"));

            var exception = await Record.ExceptionAsync(() =>
                _tools.NotifyViaTeams("Test"));

            Assert.Null(exception);
        }

        #endregion

        #region AskUserViaTeams

        [Fact]
        public async Task AskUserViaTeams_DelegatesToService()
        {
            _mockService
                .Setup(s => s.AskQuestionAsync("Ready to deploy?"))
                .ReturnsAsync("Question posted to Teams");

            await _tools.AskUserViaTeams("Ready to deploy?");

            _mockService.Verify(
                s => s.AskQuestionAsync("Ready to deploy?"),
                Times.Once);
        }

        [Fact]
        public async Task AskUserViaTeams_ReturnsServiceResponse()
        {
            _mockService
                .Setup(s => s.AskQuestionAsync(It.IsAny<string>()))
                .ReturnsAsync("Question posted to Teams channel (MyProject)");

            var result = await _tools.AskUserViaTeams("Any blockers?");

            Assert.Equal("Question posted to Teams channel (MyProject)", result);
        }

        [Fact]
        public async Task AskUserViaTeams_ServiceThrows_ReturnsErrorMessage()
        {
            _mockService
                .Setup(s => s.AskQuestionAsync(It.IsAny<string>()))
                .ThrowsAsync(new HttpRequestException("Timeout"));

            var result = await _tools.AskUserViaTeams("Status?");

            Assert.Contains("Failed", result);
            Assert.Contains("Timeout", result);
        }

        [Fact]
        public async Task AskUserViaTeams_ServiceThrows_DoesNotPropagateException()
        {
            _mockService
                .Setup(s => s.AskQuestionAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Unexpected"));

            var exception = await Record.ExceptionAsync(() =>
                _tools.AskUserViaTeams("Test?"));

            Assert.Null(exception);
        }

        #endregion

        #region Constructor

        [Fact]
        public void Constructor_WithValidService_DoesNotThrow()
        {
            var service = new Mock<ITeamsNotificationService>();

            var exception = Record.Exception(() => new TeamsTools(service.Object));

            Assert.Null(exception);
        }

        #endregion
    }
}
