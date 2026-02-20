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
        /// Posts a question adaptive card to the configured Teams channel and waits for a reply.
        /// If Gateway is configured, blocks until user replies or timeout is reached.
        /// If Gateway is unavailable, degrades to one-way notification.
        /// </summary>
        /// <param name="question">The question to post.</param>
        /// <param name="timeoutSeconds">Timeout in seconds to wait for reply (default: 120).</param>
        /// <returns>The user's answer, a timeout message, or a fallback one-way confirmation.</returns>
        Task<string?> AskQuestionAsync(string question, int timeoutSeconds = 120);

        /// <summary>
        /// Called by TeamsReplyListener when a reply is received from Gateway.
        /// </summary>
        /// <param name="questionId">The question identifier.</param>
        /// <param name="answer">The user's answer.</param>
        void OnReplyReceived(string questionId, string answer);
    }
}
