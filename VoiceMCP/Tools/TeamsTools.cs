using ModelContextProtocol.Server;
using System.ComponentModel;
using VoiceMCP.Services;

namespace VoiceMCP.Tools
{
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

        [McpServerTool, Description(@"Post a question to a Microsoft Teams channel and get the user's reply. 
This tool posts an adaptive card to the Teams channel. The user replies in the thread.
After calling this tool, use the Playwright browser to poll for the thread reply:
1. Navigate to the Teams wizard channel in the browser
2. Find the message group containing the returned question ID
3. Look for thread replies inside that group using: locator('[aria-label*=""reply""]').locator('p')
4. Return the thread reply text as the user's answer")]
        public async Task<string> AskUserViaTeams(
            [Description("The question to post to Teams")] string question)
        {
            try
            {
                var result = await _teamsService.AskQuestionAsync(question);
                return result ?? "No response.";
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"AskUserViaTeams error: {ex.Message}");
                return $"Failed to post question to Teams: {ex.Message}";
            }
        }
    }
}
