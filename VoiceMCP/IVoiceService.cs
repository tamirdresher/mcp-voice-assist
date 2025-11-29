namespace VoiceMCP.Services
{
    /// <summary>
    /// Provides voice interaction capabilities for text-to-speech and speech-to-text operations.
    /// </summary>
    public interface IVoiceService
    {
        /// <summary>
        /// Converts the specified text to speech and plays it.
        /// </summary>
        /// <param name="text">The text to convert to speech.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SpeakAsync(string text);

        /// <summary>
        /// Records audio from the microphone and converts it to text.
        /// </summary>
        /// <returns>A task representing the asynchronous operation, containing the transcribed text.</returns>
        Task<string> ListenAsync();
    }
}