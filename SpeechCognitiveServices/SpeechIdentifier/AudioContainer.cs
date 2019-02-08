namespace SpeechCognitiveServices.SpeechIdentifier
{
    /// <summary>
    /// Audio container which supports two types of containers: RAW and WAV
    /// </summary>
    public class AudioContainer
    {
        protected bool Equals(AudioContainer other)
        {
            return ContainerType == other.ContainerType;
        }

        public override int GetHashCode()
        {
            return (int) ContainerType;
        }

        /// <summary>
        /// Initializes a new instance of the AudioContainer class.
        /// </summary>
        /// <param name="type">Audio container type</param>
        public AudioContainer(AudioContainerType type)
        {
            ContainerType = type;

            switch (type)
            {
                case AudioContainerType.Wav:
                    MaxHeaderSize = 5000;
                    break;
                case AudioContainerType.Raw:
                    MaxHeaderSize = 0;
                    break;
            }
        }

        /// <summary>
        /// Gets or sets audio container type
        /// </summary>
        public AudioContainerType ContainerType
        {
            get;
        }

        internal int MaxHeaderSize { get; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((AudioContainer) obj);
        }
    }
}