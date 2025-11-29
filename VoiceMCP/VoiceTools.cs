using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using VoiceMCP.Services;

namespace VoiceMCP.Tools
{    

    [McpServerToolType]
    public class AskUserTool
    {
        const int MAX_RETRIES = 4;
        private readonly IVoiceService _voiceService;

        public AskUserTool(IVoiceService voiceService)
        {
            _voiceService = voiceService;
        }

        [McpServerTool, Description("Ask the user a question via voice and get a confirmed text response")]
        public async Task<string> AskUser(string question)
        {
            int retryCount = 0;

            // 1. Speak the question
            await _voiceService.SpeakAsync(question);

            while (retryCount < MAX_RETRIES)
            {
                // 2. Listen for response
                string recognizedText = await _voiceService.ListenAsync();

                if (string.IsNullOrWhiteSpace(recognizedText))
                {
                    retryCount++;
                    Console.Error.WriteLine($"No response detected, retry {retryCount}/{MAX_RETRIES}");

                    if (retryCount >= MAX_RETRIES)
                    {
                        await _voiceService.SpeakAsync("I'm sorry, I couldn't understand your response after multiple attempts. Please try again later.");
                        return "ERROR: Could not understand response after 4 attempts";
                    }

                    int attemptsRemaining = MAX_RETRIES - retryCount;
                    await _voiceService.SpeakAsync($"I didn't catch that. Please try again. {attemptsRemaining} attempt{(attemptsRemaining > 1 ? "s" : "")} remaining.");
                    continue;
                }

                Console.Error.WriteLine($"User response: '{recognizedText}'");

                // 3. Confirmation Loop
                int confirmRetryCount = 0;
                while (confirmRetryCount < MAX_RETRIES)
                {
                    await _voiceService.SpeakAsync($"I heard: {recognizedText}. Is this correct? Say yes or no.");
                    string confirmation = await _voiceService.ListenAsync();

                    Console.Error.WriteLine($"Confirmation response: '{confirmation}'");
                    Console.Error.WriteLine($"IsAffirmative result: {IsAffirmative(confirmation)}");

                    if (IsAffirmative(confirmation))
                    {
                        Console.Error.WriteLine("Response confirmed, returning answer");
                        return recognizedText;
                    }
                    else if (!string.IsNullOrWhiteSpace(confirmation))
                    {
                        Console.Error.WriteLine("Response not confirmed, asking again");
                        await _voiceService.SpeakAsync("Okay, let's try again. What was your answer?");
                        break; // Break confirmation loop to ask question again
                    }
                    else
                    {
                        confirmRetryCount++;
                        Console.Error.WriteLine($"Empty confirmation, retry {confirmRetryCount}/{MAX_RETRIES}");

                        if (confirmRetryCount >= MAX_RETRIES)
                        {
                            await _voiceService.SpeakAsync("I couldn't hear your confirmation. Let's try the question again.");
                            break; // Break to ask question again
                        }

                        int attemptsRemaining = MAX_RETRIES - confirmRetryCount;
                        await _voiceService.SpeakAsync($"I didn't hear you. Please say yes or no. {attemptsRemaining} attempt{(attemptsRemaining > 1 ? "s" : "")} remaining.");
                    }
                }

                retryCount++;
                if (retryCount >= MAX_RETRIES)
                {
                    await _voiceService.SpeakAsync("I'm sorry, I couldn't understand your response after multiple attempts. Please try again later.");
                    return "ERROR: Could not understand response after 4 attempts";
                }
            }

            return "ERROR: Could not understand response after 4 attempts";
        }

        [McpServerTool, Description("Ask the user for approval of a work summary via voice. Returns the user approval with feedback.")]
        public async Task<string> AskForApproval([Description("The summary of the work to be approved.")]string summary)
        {
            int retryCount = 0;

            // 1. Speak the summary
            await _voiceService.SpeakAsync("Here is the summary of the work:");
            await _voiceService.SpeakAsync(summary);

            while (retryCount < MAX_RETRIES)
            {
                await _voiceService.SpeakAsync("Do you approve this work? Please say Approved or Rejected.");

                // 2. Listen for response
                string recognizedText = await _voiceService.ListenAsync();

                if (string.IsNullOrWhiteSpace(recognizedText))
                {
                    retryCount++;
                    Console.Error.WriteLine($"No response detected, retry {retryCount}/{MAX_RETRIES}");

                    if (retryCount >= MAX_RETRIES)
                    {
                        await _voiceService.SpeakAsync("I'm sorry, I couldn't understand your response after multiple attempts. Please try again later.");
                        return "ERROR: Could not understand response after 4 attempts";
                    }

                    int attemptsRemaining = MAX_RETRIES - retryCount;
                    await _voiceService.SpeakAsync($"I didn't catch that. Please try again. {attemptsRemaining} attempt{(attemptsRemaining > 1 ? "s" : "")} remaining.");
                    continue;
                }

                Console.Error.WriteLine($"User response: '{recognizedText}'");

                // 3. Confirmation Loop
                int confirmRetryCount = 0;
                while (confirmRetryCount < MAX_RETRIES)
                {
                    await _voiceService.SpeakAsync($"I heard: {recognizedText}. Is this correct? Say yes or no.");
                    string confirmation = await _voiceService.ListenAsync();

                    Console.Error.WriteLine($"Confirmation response: '{confirmation}'");
                    Console.Error.WriteLine($"IsAffirmative result: {IsAffirmative(confirmation)}");

                    if (IsAffirmative(confirmation))
                    {
                        Console.Error.WriteLine("Response confirmed, returning answer");
                        return recognizedText;
                    }
                    else if (!string.IsNullOrWhiteSpace(confirmation))
                    {
                        Console.Error.WriteLine("Response not confirmed, asking again");
                        await _voiceService.SpeakAsync("Okay, let's try again. What was your answer?");
                        break; // Break confirmation loop to ask question again
                    }
                    else
                    {
                        confirmRetryCount++;
                        Console.Error.WriteLine($"Empty confirmation, retry {confirmRetryCount}/{MAX_RETRIES}");

                        if (confirmRetryCount >= MAX_RETRIES)
                        {
                            await _voiceService.SpeakAsync("I couldn't hear your confirmation. Let's try the question again.");
                            break; // Break to ask question again
                        }

                        int attemptsRemaining = MAX_RETRIES - confirmRetryCount;
                        await _voiceService.SpeakAsync($"I didn't hear you. Please say yes or no. {attemptsRemaining} attempt{(attemptsRemaining > 1 ? "s" : "")} remaining.");
                    }
                }

                retryCount++;
                if (retryCount >= MAX_RETRIES)
                {
                    await _voiceService.SpeakAsync("I'm sorry, I couldn't understand your response after multiple attempts. Please try again later.");
                    return "ERROR: Could not understand response after 4 attempts";
                }
            }

            return "ERROR: Could not understand response after 4 attempts";
        }


        private bool IsAffirmative(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.ToLowerInvariant().Trim();
            Console.Error.WriteLine($"Checking affirmative for: '{text}'");

            // More flexible matching - check if the text contains affirmative words
            bool result = text.Contains("yes") ||
                         text.Contains("correct") ||
                         text.Contains("yeah") ||
                         text.Contains("yep") ||
                         text.Contains("yup") ||
                         text.Contains("sure") ||
                         text.Contains("right") ||
                         text.Contains("affirmative") ||
                         text == "y";

            Console.Error.WriteLine($"Affirmative check result: {result}");
            return result;
        }

        [McpServerTool, Description("Echoes in reverse the message sent by the client.")]
        public static string ReverseEcho(string message) => new string(message.Reverse().ToArray());
    }

}