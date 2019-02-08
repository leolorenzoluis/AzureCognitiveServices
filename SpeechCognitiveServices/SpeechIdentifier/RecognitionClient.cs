using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ProjectOxford.SpeakerRecognition;

namespace SpeechCognitiveServices.SpeechIdentifier
{
    public class RecognitionClient : IDisposable
    {
        private const int DefaultDelayBetweenRequests = 250;

        private readonly AudioProcessor _audioProcessor;
        private readonly IdentificationClient _idClient;
        private int _requestId;
        private readonly Task _requestingTask;

        private readonly CancellationTokenSource _requestingTaskCancelletionTokenSource;
        private readonly SpeakerIdentificationServiceClient _serviceClient;
        private readonly List<Task> _httpRequests;

        /// <summary>
        ///     Initializes a new instance of the RecognitionClient class.
        /// </summary>
        /// <param name="clientId">ID associated with all requests related to this client</param>
        /// <param name="speakerIds">Speaker IDs for identification</param>
        /// <param name="stepSize">Step size in seconds</param>
        /// <param name="windowSize">Number of seconds sent per request</param>
        /// <param name="audioFormat">Audio format</param>
        /// <param name="resultCallback">Value callback action consisted of identification result, client ID and request ID</param>
        /// <param name="serviceClient">Client used in identifying the streamed audio file</param>
        /// <param name="httpRequests"></param>
        internal RecognitionClient(Guid clientId, Guid[] speakerIds, int stepSize, int windowSize,
            AudioFormat audioFormat, Action<RecognitionResult> resultCallback,
            SpeakerIdentificationServiceClient serviceClient, List<Task> httpRequests)
        {
            ClientId = clientId;
            SpeakerIds = speakerIds;
            StepSize = stepSize;
            WindowSize = windowSize;
            _requestId = 0;
            AudioFormat = audioFormat;
            var audioFormatHandler = new AudioFormatHandler(audioFormat);
            _serviceClient = serviceClient;
            _httpRequests = httpRequests;

            _audioProcessor = new AudioProcessor(WindowSize, StepSize, audioFormatHandler);
            _idClient = new IdentificationClient(SpeakerIds, resultCallback);

            _requestingTaskCancelletionTokenSource = new CancellationTokenSource();
            _requestingTask = Task.Run(async () =>
            {
                await SendingRequestsTask(_requestingTaskCancelletionTokenSource.Token).ConfigureAwait(false);
            });
        }

        /// <summary>
        ///     Gets or sets ID associated with all requests related to this client
        /// </summary>
        public Guid ClientId { get; set; }

        /// <summary>
        ///     Gets or sets speaker IDs for identification
        /// </summary>
        public Guid[] SpeakerIds { get; set; }

        /// <summary>
        ///     Gets or sets step size in seconds
        /// </summary>
        public int StepSize { get; set; }

        /// <summary>
        ///     Gets or sets number of seconds sent per request
        /// </summary>
        public int WindowSize { get; set; }

        /// <summary>
        ///     Gets or sets recognition audio format
        /// </summary>
        public AudioFormat AudioFormat { get; set; }

        /// <summary>
        ///     Disposes the client
        /// </summary>
        public void Dispose()
        {
            if (!_audioProcessor.IsCompleted) _requestingTaskCancelletionTokenSource.Cancel();

            _requestingTask.Wait();
        }

        /// <summary>
        ///     Streams audio to recognition service
        /// </summary>
        /// <param name="audioBytes">Audio bytes to be sent for recognition</param>
        /// <param name="offset">The position in the audio from where the stream should begin</param>
        /// <param name="length">The length of audio that should be streamed starting from the offset position</param>
        public async Task StreamAudioAsync(byte[] audioBytes, int offset, int length)
        {
            await _audioProcessor.AppendAsync(audioBytes, offset, length).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sends a signal to the recognition client that the audio stream ended
        /// </summary>
        public async Task EndStreamAudioAsync()
        {
            await _audioProcessor.CompleteAsync().ConfigureAwait(false);
        }

        private async Task SendingRequestsTask(CancellationToken token)
        {
            Console.WriteLine("Sending requests task!");
            while (!token.IsCancellationRequested)
            {
                var audio = await _audioProcessor.GetNextRequestAsync().ConfigureAwait(false);
                if (audio != null)
                {
                    var reqId = GetCurrentRequestId();

                    _httpRequests.Add(Task.Run(async () =>
                    {
                        using (var stream = new MemoryStream(audio))
                        {
                            await _idClient.IdentifyStreamAsync(stream, _serviceClient, ClientId, reqId)
                                .ConfigureAwait(false);
                        }
                    }, token));
                }
                else
                {
                    if (_audioProcessor.IsCompleted) break;
                }

                await Task.Delay(DefaultDelayBetweenRequests, token).ConfigureAwait(false);
            }
        }

        private int GetCurrentRequestId()
        {
            return Interlocked.Increment(ref _requestId);
        }
    }
}