namespace VoiceMCP.Services
{
    public interface IVoiceService
    {
        Task SpeakAsync(string text);
        Task<string> ListenAsync();
    }
}