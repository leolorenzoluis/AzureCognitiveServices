using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ProjectOxford.SpeakerRecognition;

namespace SpeechCognitiveServices.SpeechIdentifier
{
    public class ClientFactory
    {
        /// <summary>
        /// Creates new identification-streaming recognition client
        /// </summary>
        /// <param name="clientId">ID associated with all requests related to this client</param>
        /// <param name="speakerIds">Speaker ids for recognition</param>
        /// <param name="stepSize">Frequency of sending requests to the server in seconds. 
        ///     If set to 1, the client will send a request to the server for every second received from the user</param>
        /// <param name="windowSize">Number of seconds sent per request</param>
        /// <param name="audioFormat">Audio format</param>
        /// <param name="resultCallBack">Value callback action consisted of identification result, client ID and request ID</param>
        /// <param name="serviceClient">Client used in identifying the streamed audio file</param>
        /// <param name="httpRequests"></param>
        /// <returns>Identification-Streaming and recognition client</returns>
        public RecognitionClient CreateRecognitionClient(Guid clientId, Guid[] speakerIds, int stepSize, int windowSize,
            AudioFormat audioFormat, Action<RecognitionResult> resultCallBack,
            SpeakerIdentificationServiceClient serviceClient, List<Task> httpRequests)
        {
            if (speakerIds.Length < 1)
            {
                throw new ArgumentException("Speakers count can't be smaller than 1.");
            }

            var recognitionClient = new RecognitionClient(clientId, speakerIds, stepSize, windowSize, audioFormat, resultCallBack, serviceClient, httpRequests);
            return recognitionClient;
        }
    }
}