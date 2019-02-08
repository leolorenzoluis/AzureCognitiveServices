using System;

namespace SpeechCognitiveServices.SpeechIdentifier
{
    public class AudioFormat
    {
        /// <summary>
        /// Initializes a new instance of the AudioFormat class.
        /// </summary>
        /// <param name="encoding">Audio encoding</param>
        /// <param name="channelsNumber">Channels number</param>
        /// <param name="sampleRate">Sample rate</param>
        /// <param name="bitsPerSample">Bits per sample</param>
        /// <param name="audioContainer">Type of audio container, either RAW or WAV</param>
        public AudioFormat(AudioEncoding encoding, int channelsNumber, int sampleRate, int bitsPerSample, AudioContainer audioContainer)
        {
            ValidateAudioFormat(channelsNumber, sampleRate, bitsPerSample);

            Encoding = encoding;
            ChannelsNumber = channelsNumber;
            SampleRate = sampleRate;
            BitsPerSample = bitsPerSample;
            Container = audioContainer;
        }

        /// <summary>
        /// Gets or sets audio encoding
        /// </summary>
        public AudioEncoding Encoding
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets channels number
        /// </summary>
        public int ChannelsNumber
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets sample rate
        /// </summary>
        public int SampleRate
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets bits per sample
        /// </summary>
        public int BitsPerSample
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets audio container
        /// </summary>
        public AudioContainer Container
        {
            get;
            set;
        }

        /// <summary>
        /// Compares the input format of this audio
        /// </summary>
        /// <param name="obj">Input format to be compared</param>
        /// <returns>True if similar</returns>
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            return obj is AudioFormat format &&
                   Encoding.Equals(format.Encoding) &&
                   ChannelsNumber == format.ChannelsNumber &&
                   SampleRate == format.SampleRate &&
                   BitsPerSample == format.BitsPerSample &&
                   Container.Equals(format.Container);
        }

        /// <summary>
        /// Returns description of audio format
        /// </summary>
        /// <returns>A string representation for the object's fields</returns>
        public override string ToString()
        {
            return "Container: " + Container.ContainerType + ", Encoding: " + Encoding + ", Rate: " + SampleRate + ", Sample Format: " + BitsPerSample + ", Channels: " + ChannelsNumber;
        }

        private static void ValidateAudioFormat(int channelsNumber, int sampleRate, int bitsPerSample)
        {
            if (channelsNumber <= 0)
            {
                throw new ArgumentException("Channels number must be a positive number.");
            }

            if (sampleRate <= 0)
            {
                throw new ArgumentException("Sample rate must be a positive number.");
            }

            if (bitsPerSample <= 0)
            {
                throw new ArgumentException("Bits per sample must be a positive number.");
            }
        }
    }
}