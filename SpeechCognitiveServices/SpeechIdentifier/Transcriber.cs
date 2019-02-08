using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;
using SpeechCognitiveServices.SpeechToText;

namespace SpeechCognitiveServices.SpeechIdentifier
{
    public class Transcriber
    {
        private const byte Channels = 1;
        private const byte BitsPerSample = 16;
        private const uint SamplesPerSecond = 16000;
        private const uint BytesPerSecond = SamplesPerSecond * BitsPerSample / 8;

        private readonly TaskCompletionSource<int>
            _stopBaseRecognitionTaskCompletionSource = new TaskCompletionSource<int>();
        public void TrimWavFile(WaveFileReader reader, VoiceAudioStream writer, long startPos, long endPos)
        {
            reader.Position = startPos;
            var buffer = new byte[1024];
            while (reader.Position < endPos)
            {
                var bytesRequired = (int)(endPos - reader.Position);
                if (bytesRequired <= 0) continue;

                var bytesToRead = Math.Min(bytesRequired, buffer.Length);
                var bytesRead = reader.Read(buffer, 0, bytesToRead);
                if (bytesRead > 0) writer.Write(buffer, 0, bytesRead);
            }
        }

        private readonly string _wavFilePath;
        private readonly StreamWriter _streamWriter;

        public Transcriber(string wavFilePath, StreamWriter streamWriter)
        {
            _wavFilePath = wavFilePath;
            _streamWriter = streamWriter;
        }

        public async Task TranscribeSpeechFromAudioStream(SpeechConfig config, string person, int startSecond = 0, int endSecond = 0)
        {
            var audioFormat = AudioStreamFormat.GetWaveFormatPCM(SamplesPerSecond, BitsPerSample, Channels);
            using (var waveFileReader = new WaveFileReader(_wavFilePath))
            {
                var pullAudioInputStreamCallback = new VoiceAudioStream();
                TrimWavFile(waveFileReader, pullAudioInputStreamCallback, BytesPerSecond * startSecond,
                    BytesPerSecond * endSecond);
                var speechToText = new SpeechToTextRecognizer(person, _streamWriter);
                using (var audioConfig = AudioConfig.FromStreamInput(pullAudioInputStreamCallback, audioFormat))
                {
                    using (var basicRecognizer = new SpeechRecognizer(config, audioConfig))
                    {
                        await speechToText.RunRecognizer(basicRecognizer, RecognizerType.Base,
                            _stopBaseRecognitionTaskCompletionSource).ConfigureAwait(false);
                    }
                }
            };
        }

        public async Task TranscribeSpeechFromWavFileInput(SpeechConfig config)
        {
            using (var audioConfig = AudioConfig.FromWavFileInput(_wavFilePath))
            {
                using (var basicRecognizer = new SpeechRecognizer(config, audioConfig))
                {
                    var speechToText = new SpeechToTextRecognizer(_streamWriter);
                    await speechToText.RunRecognizer(basicRecognizer, RecognizerType.Base,
                        _stopBaseRecognitionTaskCompletionSource).ConfigureAwait(false);
                }
            }
        }
    }
}