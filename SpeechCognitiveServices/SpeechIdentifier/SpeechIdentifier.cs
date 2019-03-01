using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ProjectOxford.SpeakerRecognition;
using Microsoft.ProjectOxford.SpeakerRecognition.Contract;
using Microsoft.ProjectOxford.SpeakerRecognition.Contract.Identification;
using SpeechCognitiveServices.Properties;
using static System.Console;

namespace SpeechCognitiveServices.SpeechIdentifier
{
    public class SpeechIdentifier
    {
        private readonly string _wavFilePath;
        private readonly List<Task> _httpRequests;
        private readonly List<RecognitionResult> _recognitionResults = new List<RecognitionResult>();

        // Delay between passing audio chunks to the client in milliseconds
        private const int RequestsDelay = 1000;


        private static readonly CancellationTokenSource StreamAudioCancellationTokenSource =
            new CancellationTokenSource();

        private readonly SpeakerIdentificationServiceClient _serviceClient;

        private Profile[] _allProfiles;
        private const int WindowSize = 2;
        private const int StepSize = 1;

        public SpeechIdentifier(string wavFilePath, List<Task> httpRequests)
        {
            _wavFilePath = wavFilePath;
            _httpRequests = httpRequests;
            _serviceClient = new SpeakerIdentificationServiceClient(Settings.Default.SpeakerRecognitionSubscriptionKey);
        }

        public IEnumerable<RecognitionResult> RecognitionResults => _recognitionResults;


        public async Task IdentifySpeakers()
        {
            try
            {
                WriteLine("Getting all profiles");
                _allProfiles = await _serviceClient.GetProfilesAsync().ConfigureAwait(false);

                var testProfileIds = _allProfiles.Where(t => t.EnrollmentStatus == EnrollmentStatus.Enrolled).Select(
                        x =>
                        {
                            WriteLine(
                                "Speaker Profile Id: " + x.ProfileId + " has been selected for streaming.");
                            return x.ProfileId;
                        })
                    .ToArray();
                WriteLine("Streaming audio...");
                await StreamAudio(WindowSize, StepSize, testProfileIds.Where(x =>
                {
                    // Uncomment if you only want to specify select speakers
                    //var id = x.ToString();
                    //if (id.Contains("72041dcf-1ec5-4140-bfda-17e07a33898d") ||
                    //    id.Contains("db6999") ||
                    //    id.Contains("ce34b08c-3bf7-472b-a896-ff674ab576c9"))
                    //    return true;
                    //return false;
                    // ReSharper disable once ConvertToLambdaExpression
                    return true;
                }).ToArray(), StreamAudioCancellationTokenSource.Token);
            }
            catch (GetProfileException ex)
            {
                WriteLine("Error Retrieving Profiles: {0}", ex.Message);
            }
            catch (Exception ex)
            {
                WriteLine("Error: {0}", ex.Message);
            }
        }

        private void WriteResults(RecognitionResult recognitionResult)
        {
            _recognitionResults.Add(recognitionResult);
            if (!recognitionResult.Succeeded)
            {
                WriteLine("Request " + recognitionResult.RequestId + " error message: " +
                                  recognitionResult.FailureMsg);
                return;
            }

            var identificationResult = recognitionResult.Value;
            WriteLine(identificationResult.IdentifiedProfileId == Guid.Empty
                ? "Unknown"
                : identificationResult.IdentifiedProfileId.ToString());
            WriteLine(identificationResult.Confidence.ToString());
            WriteLine(recognitionResult.RequestId.ToString());
            var result = identificationResult.IdentifiedProfileId == Guid.Empty
                ? "Unknown"
                : identificationResult.IdentifiedProfileId.ToString();
            WriteLine("Request " + recognitionResult.RequestId + ": Profile id: " + result);
        }


        private async Task StreamAudio(int windowSize, int stepSize, Guid[] testProfileIds, CancellationToken token)
        {
            // Unique id of the recognition client. Returned in the callback to relate results with clients in case of having several clients using the same callback
            var recognitionClientId = Guid.NewGuid();

            // Audio format of the recognition audio
            // Supported containers: WAV and RAW (no header)
            // Supported format: Encoding = PCM, Channels = Mono (1), Rate = 16k, Bits per sample = 16
            var audioFormat =
                new AudioFormat(AudioEncoding.Pcm, 1, 16000, 16, new AudioContainer(AudioContainerType.Wav));
            using (Stream audioStream = File.OpenRead(_wavFilePath))
            {
                // Client factory is used to create a recognition client
                // Recognition client can be used for one audio only. In case of having several audios, a separate client should be created for each one
                // ReSharper disable once IdentifierTypo
                var clientfactory = new ClientFactory();
                using (var recognitionClient = clientfactory.CreateRecognitionClient(recognitionClientId,
                    testProfileIds, stepSize, windowSize, audioFormat, WriteResults, _serviceClient, _httpRequests))
                {
                    WriteLine("Sending request");
                    const int chunkSize = 32000;
                    var buffer = new byte[chunkSize];
                    int bytesRead;

                    while ((bytesRead = audioStream.Read(buffer, 0, buffer.Length)) > 0 &&
                           !token.IsCancellationRequested)
                    {
                        // You can send any number of bytes not limited to 1 second
                        // If the remaining bytes of the last request are smaller than 1 second, it gets ignored
                        await recognitionClient.StreamAudioAsync(buffer, 0, bytesRead).ConfigureAwait(false);

                        // Simulates live streaming
                        // It's recommended to use a one second delay to guarantee receiving responses in the correct order
                        await Task.Delay(RequestsDelay, token).ConfigureAwait(false);
                    }

                    await recognitionClient.EndStreamAudioAsync().ConfigureAwait(false);
                }
            }
        }
    }
}