using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AudioToText;
using Microsoft.SemanticKernel.TextToAudio;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using NAudio.Wave;
using NAudio.Lame;
using System.Text;

namespace VoiceMCP.Services
{
    /// <summary>
    /// Implements voice interaction using Azure OpenAI services through Semantic Kernel.
    /// Provides text-to-speech and speech-to-text capabilities using OpenAI TTS and Whisper.
    /// </summary>
    public class SemanticKernelVoiceService : IVoiceService, IDisposable
    {
        private readonly Kernel _kernel;
        private readonly ITextToAudioService _textToAudioService;
        private readonly IAudioToTextService _audioToTextService;
        private readonly string _voice;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SemanticKernelVoiceService"/> class.
        /// </summary>
        /// <param name="kernel">The Semantic Kernel instance with configured audio services.</param>
        /// <param name="voice">The voice to use for text-to-speech. Default is "alloy".</param>
        /// <exception cref="ArgumentNullException">Thrown when kernel is null.</exception>
        public SemanticKernelVoiceService(Kernel kernel, string voice = "alloy")
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            _voice = voice;

            // Get the audio services from the kernel
            _textToAudioService = kernel.GetRequiredService<ITextToAudioService>();
            _audioToTextService = kernel.GetRequiredService<IAudioToTextService>();
        }

        /// <inheritdoc/>
        public async Task SpeakAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            try
            {
                // Convert text to audio using OpenAI TTS
                var executionSettings = new OpenAITextToAudioExecutionSettings
                {
                    Voice = _voice,
                    ResponseFormat = "mp3"
                };
                var audioContent = await _textToAudioService.GetAudioContentAsync(text, executionSettings);

                if (audioContent?.Data == null || !audioContent.Data.HasValue || audioContent.Data.Value.Length == 0)
                {
                    Console.Error.WriteLine("No audio data received from TTS service");
                    return;
                }

                // Play the audio
                await PlayAudioAsync(audioContent.Data.Value.ToArray());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in Speak: {ex.Message}");
                throw;
            }
        }

        public async Task<string> ListenAsync()
        {
            try
            {
                Console.Error.WriteLine("=== Starting ListenAsync ===");
                
                // Record audio from microphone
                var audioData = await RecordAudioAsync();

                if (audioData == null || audioData.Length == 0)
                {
                    Console.Error.WriteLine("DIAGNOSTIC: No audio data recorded - possible causes:");
                    Console.Error.WriteLine("  - Microphone not accessible or wrong device selected");
                    Console.Error.WriteLine("  - Audio level too low to detect");
                    Console.Error.WriteLine("  - User didn't speak during recording window");
                    return string.Empty;
                }

                Console.Error.WriteLine($"Recorded {audioData.Length} bytes of audio data");
                
                // Log WAV header info for diagnostics
                LogAudioFileDetails(audioData);

                // Convert to MP3 format for better compatibility with Azure OpenAI Whisper
                var mp3Data = ConvertWavToMp3(audioData);
                Console.Error.WriteLine($"Converted to MP3: {mp3Data.Length} bytes");
                
                if (mp3Data.Length == 0)
                {
                    Console.Error.WriteLine("ERROR: MP3 conversion resulted in empty file");
                    return string.Empty;
                }

                // Convert audio to text using OpenAI Whisper with MP3 format
                var audioContent = new AudioContent(mp3Data, mimeType: "audio/mp3");
                var executionSettings = new OpenAIAudioToTextExecutionSettings
                {
                    Language = "en",
                    Filename = "audio.mp3", // Explicitly set filename with extension
                    Temperature = 0.0f // Use 0 for most accurate transcription
                };
                
                Console.Error.WriteLine("Sending audio to Whisper API...");
                var startTime = DateTime.Now;
                var textContent = await _audioToTextService.GetTextContentAsync(audioContent, executionSettings);
                var elapsed = DateTime.Now - startTime;
                
                Console.Error.WriteLine($"Whisper API response received in {elapsed.TotalSeconds:F2}s");
                Console.Error.WriteLine($"Transcription result: '{textContent?.Text ?? "(empty)"}'");
                
                if (string.IsNullOrWhiteSpace(textContent?.Text))
                {
                    Console.Error.WriteLine("DIAGNOSTIC: Empty transcription - possible causes:");
                    Console.Error.WriteLine("  - Audio too quiet or contains only silence");
                    Console.Error.WriteLine("  - Background noise without clear speech");
                    Console.Error.WriteLine("  - Audio format issue (check MP3 conversion)");
                    Console.Error.WriteLine("  - Speech not in expected language (en)");
                }

                return textContent?.Text ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR in ListenAsync: {ex.Message}");
                Console.Error.WriteLine($"Exception type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    Console.Error.WriteLine($"Inner exception type: {ex.InnerException.GetType().Name}");
                }
                Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
                return string.Empty; // Return empty instead of throwing to allow retry
            }
        }

        private async Task<byte[]> RecordAudioAsync()
        {
            var tcs = new TaskCompletionSource<byte[]>();
            var recordedBytes = new List<byte>();
            var silenceThreshold = 300; // Lowered from 500 for better detection
            var silenceDuration = TimeSpan.FromSeconds(2.5); // Increased slightly for better speech capture
            var silenceStart = DateTime.MaxValue;
            var hasSpoken = false;
            var preRecordingDelay = TimeSpan.FromMilliseconds(500); // Allow user to start speaking

            Console.Error.WriteLine($"Recording settings: threshold={silenceThreshold}, silenceDuration={silenceDuration.TotalSeconds}s");

            using var waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1) // 16kHz, mono for Whisper
            };

            waveIn.DataAvailable += (sender, e) =>
            {
                // Add recorded data
                recordedBytes.AddRange(e.Buffer.Take(e.BytesRecorded));

                // Calculate audio level for silence detection
                var level = CalculateAudioLevel(e.Buffer, e.BytesRecorded);

                if (level > silenceThreshold)
                {
                    hasSpoken = true;
                    silenceStart = DateTime.MaxValue;
                }
                else if (hasSpoken && silenceStart == DateTime.MaxValue)
                {
                    silenceStart = DateTime.Now;
                }
                else if (hasSpoken && (DateTime.Now - silenceStart) >= silenceDuration)
                {
                    waveIn.StopRecording();
                }
            };

            waveIn.RecordingStopped += (sender, e) =>
            {
                if (e.Exception != null)
                {
                    tcs.TrySetException(e.Exception);
                }
                else
                {
                    tcs.TrySetResult(recordedBytes.ToArray());
                }
            };

            Console.Error.WriteLine("Listening... (speak now)");
            
            // Small delay to allow user to start speaking
            await Task.Delay(preRecordingDelay);
            
            waveIn.StartRecording();

            // Increased maximum recording time to 45 seconds for longer responses
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(45));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                waveIn.StopRecording();
                await tcs.Task; // Wait for the stop to complete
            }

            var audioBytes = await tcs.Task;

            Console.Error.WriteLine($"Recording complete: {audioBytes.Length} bytes captured");
            
            // Check if we actually recorded any audio
            if (audioBytes.Length == 0)
            {
                Console.Error.WriteLine("WARNING: No audio data captured - microphone may not be working");
                return Array.Empty<byte>();
            }

            // Convert to WAV format with proper headers
            var wavData = ConvertToWav(audioBytes, waveIn.WaveFormat);
            
            // Calculate approximate duration
            var durationSeconds = audioBytes.Length / (double)(waveIn.WaveFormat.SampleRate * waveIn.WaveFormat.Channels * (waveIn.WaveFormat.BitsPerSample / 8));
            Console.Error.WriteLine($"Audio duration: approximately {durationSeconds:F2} seconds");
            
            return wavData;
        }

        private float CalculateAudioLevel(byte[] buffer, int bytesRecorded)
        {
            float sum = 0;
            for (int i = 0; i < bytesRecorded; i += 2)
            {
                if (i + 1 < bytesRecorded)
                {
                    short sample = (short)((buffer[i + 1] << 8) | buffer[i]);
                    // Use the absolute value, but handle the edge case of short.MinValue
                    sum += sample == short.MinValue ? short.MaxValue : Math.Abs(sample);
                }
            }
            return bytesRecorded > 0 ? sum / (bytesRecorded / 2) : 0;
        }

        private byte[] ConvertToWav(byte[] audioData, WaveFormat format)
        {
            using var memoryStream = new MemoryStream();
            using (var writer = new WaveFileWriter(memoryStream, format))
            {
                writer.Write(audioData, 0, audioData.Length);
            }
            return memoryStream.ToArray();
        }

        private byte[] ConvertWavToMp3(byte[] wavData)
        {
            try
            {
                Console.Error.WriteLine("Converting WAV to MP3...");
                using var wavStream = new MemoryStream(wavData);
                using var reader = new WaveFileReader(wavStream);
                using var mp3Stream = new MemoryStream();
                
                Console.Error.WriteLine($"WAV format: {reader.WaveFormat.SampleRate}Hz, {reader.WaveFormat.Channels} channel(s), {reader.WaveFormat.BitsPerSample}-bit");
                
                // Use LAMEPreset.STANDARD for good quality and compatibility
                using (var writer = new LameMP3FileWriter(mp3Stream, reader.WaveFormat, LAMEPreset.STANDARD))
                {
                    reader.CopyTo(writer);
                }
                
                var mp3Data = mp3Stream.ToArray();
                Console.Error.WriteLine($"MP3 conversion successful: {mp3Data.Length} bytes");
                return mp3Data;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR converting WAV to MP3: {ex.Message}");
                Console.Error.WriteLine($"Exception type: {ex.GetType().Name}");
                Console.Error.WriteLine("Falling back to WAV format");
                // Fall back to WAV if conversion fails
                return wavData;
            }
        }

        private void LogAudioFileDetails(byte[] audioData)
        {
            try
            {
                if (audioData.Length < 44)
                {
                    Console.Error.WriteLine($"WARNING: Audio data too small ({audioData.Length} bytes) to be valid WAV");
                    return;
                }

                // Read WAV header
                var riff = Encoding.ASCII.GetString(audioData, 0, 4);
                var fileSize = BitConverter.ToInt32(audioData, 4);
                var wave = Encoding.ASCII.GetString(audioData, 8, 4);
                var fmt = Encoding.ASCII.GetString(audioData, 12, 4);
                
                if (riff == "RIFF" && wave == "WAVE")
                {
                    var audioFormat = BitConverter.ToInt16(audioData, 20);
                    var channels = BitConverter.ToInt16(audioData, 22);
                    var sampleRate = BitConverter.ToInt32(audioData, 24);
                    var bitsPerSample = BitConverter.ToInt16(audioData, 34);
                    
                    Console.Error.WriteLine($"WAV Details:");
                    Console.Error.WriteLine($"  Format: {audioFormat} (1=PCM)");
                    Console.Error.WriteLine($"  Channels: {channels}");
                    Console.Error.WriteLine($"  Sample Rate: {sampleRate} Hz");
                    Console.Error.WriteLine($"  Bits per Sample: {bitsPerSample}");
                    Console.Error.WriteLine($"  Total Size: {audioData.Length} bytes");
                }
                else
                {
                    Console.Error.WriteLine($"WARNING: Invalid WAV header. RIFF={riff}, WAVE={wave}");
                    Console.Error.WriteLine($"First 16 bytes (hex): {BitConverter.ToString(audioData.Take(16).ToArray())}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error logging audio details: {ex.Message}");
            }
        }


        private async Task PlayAudioAsync(byte[] audioData)
        {
            using var memoryStream = new MemoryStream(audioData);
            using var reader = new Mp3FileReader(memoryStream);
            using var waveOut = new WaveOutEvent();

            var tcs = new TaskCompletionSource<bool>();

            waveOut.PlaybackStopped += (sender, e) =>
            {
                if (e.Exception != null)
                {
                    tcs.TrySetException(e.Exception);
                }
                else
                {
                    tcs.TrySetResult(true);
                }
            };

            waveOut.Init(reader);
            waveOut.Play();

            await tcs.Task;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}