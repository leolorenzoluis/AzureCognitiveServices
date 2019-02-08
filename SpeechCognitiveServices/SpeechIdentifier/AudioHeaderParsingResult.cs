namespace SpeechCognitiveServices.SpeechIdentifier
{
    internal class AudioHeaderParsingResult
    {
        /// <summary>
        /// Gets or sets the start position of data chunck
        /// </summary>
        public int DataChunckStart
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets number of bytes per second
        /// </summary>
        public int NumberofBytesPerSecond
        {
            get; set;
        }
    }
}