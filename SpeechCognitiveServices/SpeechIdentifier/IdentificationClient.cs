using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ProjectOxford.SpeakerRecognition;
using Microsoft.ProjectOxford.SpeakerRecognition.Contract.Identification;

namespace SpeechCognitiveServices.SpeechIdentifier
{
    /// <summary>
    ///     Identification client
    ///     Performs the identification against SpeakerRecognition service
    /// </summary>
    internal class IdentificationClient
    {
        private const int TimeSpanBetweenPollingRetries = 2;
        private readonly Action<RecognitionResult> _resultCallback;
        private readonly Guid[] _speakerIds;

        /// <summary>
        ///     Initializes a new instance of the IdentificationClient class.
        /// </summary>
        /// <param name="speakerIds"> Speaker IDs for identification</param>
        /// <param name="callback">Value callback action consisted of identification result, request ID and second sequence number</param>
        public IdentificationClient(Guid[] speakerIds, Action<RecognitionResult> callback)
        {
            _speakerIds = speakerIds;
            _resultCallback = callback;
        }

        /// <summary>
        ///     Identify a stream of audio
        /// </summary>
        /// <param name="stream">Audio buffer to be recognized</param>
        /// <param name="serviceClient">Client used in identifying the streamed audio wave</param>
        /// <param name="clientId">Client ID</param>
        /// <param name="requestId">Request ID</param>
        public async Task IdentifyStreamAsync(Stream stream, SpeakerIdentificationServiceClient serviceClient,
            Guid clientId, int requestId)
        {
            try
            {
                OperationLocation processPollingLocation = await serviceClient.IdentifyAsync(stream, _speakerIds, true).ConfigureAwait(false);
                var numberOfPollingRetries = 3;
                while (numberOfPollingRetries > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(TimeSpanBetweenPollingRetries));
                    IdentificationOperation identificationResponse = await serviceClient.CheckIdentificationStatusAsync(processPollingLocation);
                    
                    if (identificationResponse.Status == Status.Succeeded)
                    {
                        var result = new RecognitionResult(identificationResponse.ProcessingResult, clientId,
                            requestId);
                        _resultCallback(result);
                        break;
                    }

                    if (identificationResponse.Status == Status.Failed)
                    {
                        var failureResult = new RecognitionResult(false, identificationResponse.Message, requestId);

                        _resultCallback(failureResult);
                        return;
                    }

                    numberOfPollingRetries--;
                }


                if (numberOfPollingRetries <= 0)
                {
                    var failureResult = new RecognitionResult(false, "Request timeout.", requestId);
                    _resultCallback(failureResult);
                }
            }
            catch (Exception ex)
            {
                var result = new RecognitionResult(false, ex.Message, requestId);
                _resultCallback(result);
            }
        }
    }
}