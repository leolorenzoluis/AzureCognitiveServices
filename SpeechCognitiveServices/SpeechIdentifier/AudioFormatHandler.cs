using System;
using System.IO;
using System.Text;

namespace SpeechCognitiveServices.SpeechIdentifier
{
    /// <summary>
    /// Handles audio formats, and parses header
    /// </summary>
    internal class AudioFormatHandler
    {
        private AudioHeaderParsingResult _parsingResult;

        private readonly Encoding _encoding = new ASCIIEncoding();

        private readonly AudioFormat _defaultAudioFormat;

        /// <summary>
        /// Initializes a new instance of the AudioFormatHandler class.
        /// </summary>
        /// <param name="audioFormat">Audio format</param>
        public AudioFormatHandler(AudioFormat audioFormat)
        {
            InputAudioFormat = audioFormat;
            _defaultAudioFormat = new AudioFormat(AudioEncoding.Pcm, 1, 16000, 16, new AudioContainer(AudioContainerType.Wav));
        }

        /// <summary>
        /// Gets input audio codec and container format
        /// </summary>
        public AudioFormat InputAudioFormat
        {
            get;
        }

        /// <summary>
        /// Parses an audio file header and returns parsing results (start of data-chunk and number of bytes per second)
        /// </summary>
        /// <param name="header">Audio file header</param>
        /// <returns>Parsing results (start of data-chunk and number of bytes per second)</returns>
        public virtual AudioHeaderParsingResult ParseHeader(byte[] header)
        {
            if (header.Length < InputAudioFormat.Container.MaxHeaderSize)
            {
                throw new ArgumentException($"Input size is incorrect. Expected {InputAudioFormat.Container.MaxHeaderSize} vs Actual: {header.Length}");
            }

            _parsingResult = new AudioHeaderParsingResult();
            if (InputAudioFormat.Container.MaxHeaderSize == 0)
            {
                _parsingResult.NumberofBytesPerSecond = CalculateBytesPerSecond(_defaultAudioFormat);
                _parsingResult.DataChunckStart = 0;
                return _parsingResult;
            }

            ProcessHeader(header);

            _parsingResult.NumberofBytesPerSecond = CalculateBytesPerSecond(InputAudioFormat);
            return _parsingResult;
        }

        private void ProcessHeader(byte[] header)
        {
            var parsedFormat = ParseContainerHeader(header);

            if (!InputAudioFormat.Equals(parsedFormat))
            {
                throw new ArgumentException($"Actual format does not match claimed format. Actual format:  {parsedFormat} vs Claimed format: {InputAudioFormat}");
            }
        }

        private AudioFormat ParseContainerHeader(byte[] header)
        {
            AudioFormat parsedFormat = null;

            using (Stream stream = new MemoryStream(header))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (InputAudioFormat.Container.ContainerType.Equals(AudioContainerType.Wav))
                {
                    string label = GetChunkLabel(reader, stream, 0);
                    if (string.CompareOrdinal(label, "RIFF") != 0)
                    {
                        throw new InvalidDataException("Unable to find RIFF signature in header");
                    }

                    label = GetChunkLabel(reader, stream, 8);
                    if (string.CompareOrdinal(label, "WAVE") != 0)
                    {
                        throw new InvalidDataException("Unable to find WAVE signature in header");
                    }

                    bool isParsed = false;
                    while (!isParsed)
                    {
                        // Safe to cast to int because the header size can't be > 5k
                        label = GetChunkLabel(reader, stream, (int)stream.Position);
                        int chunkSize = reader.ReadInt32();

                        switch (label)
                        {
                            case "fmt ":
                                long currentStreamPosition = stream.Position;
                                AudioEncoding encoding = AudioEncoding.None;
                                if (reader.ReadInt16() == 1)
                                {
                                    encoding = AudioEncoding.Pcm;
                                }

                                int channelsNumber = reader.ReadInt16();

                                int sampleRate = reader.ReadInt32();

                                // Skipping the unneeded format specs
                                stream.Position += 6;

                                int bitsPerSample = reader.ReadInt16();

                                parsedFormat = new AudioFormat(encoding, channelsNumber, sampleRate, bitsPerSample, new AudioContainer(AudioContainerType.Wav));

                                stream.Position = currentStreamPosition + chunkSize;
                                break;
                            case "data":
                                isParsed = true;
                                _parsingResult.DataChunckStart = (int)stream.Position;
                                if (parsedFormat == null)
                                {
                                    throw new InvalidDataException("Unable to find the fmt chunk in header");
                                }

                                break;
                            default:
                                stream.Position += chunkSize;
                                break;
                        }
                    }
                }
                else
                {
                    throw new InvalidDataException($"Unsupported container format: {InputAudioFormat.Container.ContainerType.ToString()}");
                }
            }

            return parsedFormat;
        }

        private string GetChunkLabel(BinaryReader reader, Stream stream, int position)
        {
            stream.Position = position;
            var labelBytes = reader.ReadBytes(4);
            return _encoding.GetString(labelBytes, 0, labelBytes.Length);
        }

        private int CalculateBytesPerSecond(AudioFormat format)
        {
            var count = (format.BitsPerSample * format.SampleRate * format.ChannelsNumber) / 8;
            return count;
        }
    }
}