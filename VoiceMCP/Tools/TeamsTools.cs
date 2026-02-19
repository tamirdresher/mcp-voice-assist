using ModelContextProtocol.Server;
using System.ComponentModel;
using VoiceMCP.Services;

namespace VoiceMCP.Tools
{
    // No [McpServerToolType] — Fenster will register this class explicitly
    // only when the Teams webhook URL is configured.
    public class TeamsTools
    {
        private readonly ITeamsNotificationService _teamsService;

        public TeamsTools(ITeamsNotificationService teamsService)
        {
            _teamsService = teamsService;
        }

        [McpServerTool, Description("Send a notification message to a Microsoft Teams channel. Use this to inform the user about progress, completed work, or important updates.")]
        public async Task<string> NotifyViaTeams(
            [Description("The notification message to send")] string message,
            [Description("Optional title for the notification card")] string? title = null)
        {
            try
            {
                await _teamsService.SendNotificationAsync(message, title);
                return $"Notification sent to Teams successfully.";
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"NotifyViaTeams error: {ex.Message}");
                return $"Failed to send Teams notification: {ex.Message}";
            }
        }

        [McpServerTool, Description("Post a question to a Microsoft Teams channel for the user to see. Since Teams webhooks are one-way, this posts the question as a card. The user should respond through another channel.")]
        public async Task<string> AskUserViaTeams(
            [Description("The question to post to Teams")] string question)
        {
            try
            {
                return await _teamsService.AskQuestionAsync(question);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"AskUserViaTeams error: {ex.Message}");
                return $"Failed to post question to Teams: {ex.Message}";
            }
        }
    }
}
