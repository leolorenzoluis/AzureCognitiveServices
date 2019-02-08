using System;
using Microsoft.ProjectOxford.SpeakerRecognition.Contract.Identification;

namespace SpeechCognitiveServices.SpeechIdentifier
{
    /// <summary>
    /// Recognition result which includes the ID of the client initiated the request, the ID of the request and the identification result for the request
    /// </summary>
    public class RecognitionResult
    {
        /// <summary>
        /// Initializes a new instance of the RecognitionResult class incase of a successful recognition
        /// </summary>
        /// <param name="result">Operation result</param>
        /// <param name="clientId">Client ID</param>
        /// <param name="requestId">Request ID</param>
        public RecognitionResult(Identification result, Guid clientId, int requestId)
        {
            Value = result;
            ClientId = clientId;
            RequestId = requestId;

            Succeeded = true;
        }

        /// <summary>
        /// Initializes a new instance of the RecognitionResult class incase of a failed recognition
        /// </summary>
        /// <param name="status">Flag that Indicates whether the request has succeeded or not</param>
        /// <param name="failureMsg">Failure message in case of a failure</param>
        /// <param name="requestId">Request ID</param>
        public RecognitionResult(bool status, string failureMsg, int requestId)
        {
            Succeeded = status;
            FailureMsg = failureMsg;
            RequestId = requestId;
        }

        /// <summary>
        /// Operation result
        /// </summary>
        public Identification Value
        {
            get; set;
        }

        /// <summary>
        /// Client ID
        /// </summary>
        public Guid ClientId
        {
            get; set;
        }

        /// <summary>
        /// Request ID which gets incremented with each request
        /// </summary>
        public int RequestId
        {
            get; set;
        }

        /// <summary>
        /// Flag that Indicates whether the request has succeeded or not
        /// </summary>
        public bool Succeeded
        {
            get; set;
        }

        /// <summary>
        /// Gets and Sets failure message in case of a failure
        /// </summary>
        public string FailureMsg
        {
            get; set;
        }
    }
}