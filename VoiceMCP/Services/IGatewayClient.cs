namespace VoiceMCP.Services
{
    /// <summary>
    /// Client for communicating with the VoiceMCP Gateway for two-way Teams communication.
    /// </summary>
    public interface IGatewayClient
    {
        /// <summary>
        /// Registers this VoiceMCP instance with the Gateway.
        /// </summary>
        /// <param name="instanceId">Unique identifier for this instance.</param>
        /// <param name="callbackUrl">HTTP endpoint where this instance receives replies.</param>
        /// <param name="projectName">Name of the project/worktree for context.</param>
        /// <param name="secret">Shared secret for validating callbacks.</param>
        Task RegisterInstanceAsync(string instanceId, string callbackUrl, string projectName, string secret);

        /// <summary>
        /// Registers a pending question with the Gateway.
        /// </summary>
        /// <param name="questionId">Unique question identifier (format: q-{instanceId}-{seq}).</param>
        /// <param name="instanceId">Instance that owns this question.</param>
        Task RegisterQuestionAsync(string questionId, string instanceId);

        /// <summary>
        /// Sends a heartbeat to the Gateway to maintain session registration.
        /// </summary>
        /// <param name="instanceId">Instance identifier.</param>
        /// <param name="secret">Shared secret for authentication.</param>
        Task HeartbeatAsync(string instanceId, string secret);

        /// <summary>
        /// Unregisters this VoiceMCP instance from the Gateway.
        /// </summary>
        /// <param name="instanceId">Instance identifier to unregister.</param>
        Task UnregisterInstanceAsync(string instanceId);
    }
}
