using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SpeechCognitiveServices.SpeechIdentifier
{
    /// <summary>
    /// An audio processor that handles streaming by means of the sliding window,
    /// It processes input stream, and prepares the corresponding waves according to the sliding window parameters.
    /// </summary>
    internal class AudioProcessor
    {
        private readonly int _maxWaveHeaderSize;

        private readonly Queue<byte[]> _secondsQueue;
        private readonly ConcurrentQueue<byte[]> _wavesQueue;

        private byte[] _lastSecond;
        private int _lastSecondIndex;

        private bool _headerFound;
        private readonly byte[] _headerBuffer;
        private int _headerBufferIndex;

        private readonly int _stepSize;
        private readonly int _windowSize;
        private int _secondsBuffered;

        private int _numberOfBytesPerSecond;

        private readonly AudioFormatHandler _audioFormatHandler;

        /// <summary>
        /// Initializes a new instance of the AudioProcessor class given a specified window size, step size and an audio format handler
        /// </summary>
        /// <param name="windowSize">The number of seconds to be included in each request</param>
        /// <param name="stepSize">The number of seconds between every request</param>
        /// <param name="audioFormatHandler">A helper handler to process the input stream, and verify its type</param>
        public AudioProcessor(int windowSize, int stepSize, AudioFormatHandler audioFormatHandler)
        {
            if (windowSize <= 0)
            {
                throw new ArgumentException("Window size must be a positive integer", nameof(windowSize));
            }

            if (stepSize <= 0)
            {
                throw new ArgumentException("Step size must be a positive integer", nameof(stepSize));
            }

            _windowSize = windowSize;
            _stepSize = stepSize;
            _audioFormatHandler = audioFormatHandler ?? throw new ArgumentNullException(nameof(audioFormatHandler));

            _secondsQueue = new Queue<byte[]>();
            _wavesQueue = new ConcurrentQueue<byte[]>();

            _lastSecond = null;
            _lastSecondIndex = 0;

            _maxWaveHeaderSize = audioFormatHandler.InputAudioFormat.Container.MaxHeaderSize;

            _headerFound = false;
            _headerBuffer = new byte[_maxWaveHeaderSize];
            _headerBufferIndex = 0;
        }

        /// <summary>
        /// Gets a boolean to indicate whether processing is complete or not.
        /// </summary>
        public bool IsCompleted { get; private set; }

        /// <summary>
        /// Stores input bytes into buffer for processing.
        /// </summary>
        /// <param name="bytesToSend">The byte array to send</param>
        public async Task AppendAsync(byte[] bytesToSend)
        {
            if (bytesToSend == null)
            {
                throw new ArgumentNullException(nameof(bytesToSend));
            }

            await AppendAsync(bytesToSend, 0, bytesToSend.Length).ConfigureAwait(false);
        }

        /// <summary>
        /// Stores input bytes into buffer for processing.
        /// </summary>
        /// <param name="buffer">The byte array to send from</param>
        /// <param name="offset">The index at which to start index</param>
        /// <param name="numberOfBytes">The number of bytes to be sent</param>
        public async Task AppendAsync(byte[] buffer, int offset, int numberOfBytes)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0)
            {
                throw new ArgumentException("Offset to start sending from must be a non-negative integer", nameof(offset));
            }

            if (numberOfBytes < 0)
            {
                throw new ArgumentException("Number of bytes to send must be a non-negative integer", nameof(numberOfBytes));
            }

            if (offset + numberOfBytes > buffer.Length)
            {
                throw new ArgumentException("There aren't enough bytes to send");
            }

            if (!_headerFound)
            {
                await ProcessHeader(buffer, offset, numberOfBytes).ConfigureAwait(false);
            }
            else
            {
                await AppendToQueue(buffer, offset, numberOfBytes).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Gets the next window to be sent.
        /// </summary>
        /// <returns>A byte array containing the wave to be sent</returns>
        public Task<byte[]> GetNextRequestAsync()
        {
            return !_wavesQueue.TryDequeue(out var audio) ? Task.FromResult<byte[]>(null) : Task.FromResult(audio);
        }

        /// <summary>
        /// Signals the processor to stop processing data
        /// </summary>
        public async Task CompleteAsync()
        {
            IsCompleted = true;
            if (_secondsBuffered > 0)
            {
                await PrepareRequestAsync().ConfigureAwait(false);
            }
        }

        private async Task AppendToQueue(byte[] bytesToSend, int offset, int numberOfBytes)
        {
            do
            {
                int numberOfBytesToSend = Math.Min(numberOfBytes, _numberOfBytesPerSecond - _lastSecondIndex);
                Array.Copy(bytesToSend, offset, _lastSecond, _lastSecondIndex, numberOfBytesToSend);

                offset += numberOfBytesToSend;
                numberOfBytes -= numberOfBytesToSend;
                _lastSecondIndex += numberOfBytesToSend;

                if (_lastSecondIndex == _numberOfBytesPerSecond)
                {
                    var second = (byte[])_lastSecond.Clone();

                    _secondsQueue.Enqueue(second);
                    if (_secondsQueue.Count > _windowSize)
                    {
                        _secondsQueue.Dequeue();
                    }

                    _secondsBuffered = (_secondsBuffered + 1) % _stepSize;
                    if (_secondsBuffered == 0)
                    {
                        await PrepareRequestAsync().ConfigureAwait(false);
                    }

                    _lastSecondIndex = 0;
                }
            }
            while (numberOfBytes > 0);
        }

        private async Task ProcessHeader(byte[] bytesToSend, int offset, int numberOfBytes)
        {
            int numberOfBytesToSend = Math.Min(numberOfBytes, _maxWaveHeaderSize - _headerBufferIndex);
            Array.Copy(bytesToSend, offset, _headerBuffer, _headerBufferIndex, numberOfBytesToSend);

            offset += numberOfBytesToSend;
            numberOfBytes -= numberOfBytesToSend;
            _headerBufferIndex += numberOfBytesToSend;

            if (_headerBufferIndex == _maxWaveHeaderSize)
            {
                var result = _audioFormatHandler.ParseHeader(_headerBuffer);

                _numberOfBytesPerSecond = result.NumberofBytesPerSecond;
                if (_numberOfBytesPerSecond <= 0)
                {
                    throw new InvalidDataException("The input audio's number of bytes per second must be a positive integer");
                }

                _lastSecond = new byte[_numberOfBytesPerSecond];

                _headerFound = true;
                int headerSize = result.DataChunckStart;

                await AppendAsync(_headerBuffer, headerSize, _maxWaveHeaderSize - headerSize).ConfigureAwait(false);
                await AppendAsync(bytesToSend, offset, numberOfBytes).ConfigureAwait(false);
            }
        }

        private Task PrepareRequestAsync()
        {
            var audioWave = GenerateWaveFile();
            _wavesQueue.Enqueue(audioWave);
            return Task.FromResult(0);
        }

        private byte[] GenerateWaveFile()
        {
            const int bitDepth = 16;
            const int sampleRate = 16000;
            int totalSampleCount = sampleRate * _secondsQueue.Count;

            using (var stream = new MemoryStream())
            {
                stream.Position = 0;
                stream.Write(Encoding.ASCII.GetBytes("RIFF"), 0, 4);
                stream.Write(BitConverter.GetBytes(((bitDepth / 8) * totalSampleCount) + 36), 0, 4);
                stream.Write(Encoding.ASCII.GetBytes("WAVE"), 0, 4);
                stream.Write(Encoding.ASCII.GetBytes("fmt "), 0, 4);
                stream.Write(BitConverter.GetBytes(16), 0, 4);
                stream.Write(BitConverter.GetBytes((ushort)1), 0, 2);
                stream.Write(BitConverter.GetBytes(1), 0, 2);
                stream.Write(BitConverter.GetBytes(sampleRate), 0, 4);
                stream.Write(BitConverter.GetBytes(sampleRate * (bitDepth / 8)), 0, 4);
                stream.Write(BitConverter.GetBytes((ushort)(bitDepth / 8)), 0, 2);
                stream.Write(BitConverter.GetBytes(bitDepth), 0, 2);
                stream.Write(Encoding.ASCII.GetBytes("data"), 0, 4);
                stream.Write(BitConverter.GetBytes((bitDepth / 8) * totalSampleCount), 0, 4);

                foreach (var wave in _secondsQueue)
                {
                    stream.Write(wave, 0, wave.Length);
                }

                return stream.ToArray();
            }
        }
    }
}