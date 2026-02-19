namespace VoiceMCP.Services
{
    /// <summary>
    /// Provides Teams channel notification capabilities via Incoming Webhook.
    /// </summary>
    public interface ITeamsNotificationService
    {
        /// <summary>
        /// Sends a notification adaptive card to the configured Teams channel.
        /// </summary>
        /// <param name="message">The message body to display.</param>
        /// <param name="title">Optional title for the card.</param>
        Task SendNotificationAsync(string message, string? title = null);

        /// <summary>
        /// Posts a question adaptive card to the configured Teams channel.
        /// Since Teams Incoming Webhooks are one-way, this posts the card and returns
        /// a confirmation message rather than waiting for a response.
        /// </summary>
        /// <param name="question">The question to post.</param>
        /// <returns>A message confirming the question was posted to Teams.</returns>
        Task<string> AskQuestionAsync(string question);
    }
}
