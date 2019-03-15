using System;
using System.IO;
using NAudio.Wave;

namespace SpeechCognitiveServices
{
    public class AudioWriter
    {
        private WaveFileWriter _waveFileWriter;
        private WaveInEvent _waveIn;
        private readonly string _outputFolder;
        public readonly int WavFileCount;
        public string OutputFilePath;


        public AudioWriter(string outputFolder)
        {
            _outputFolder = outputFolder;
            WavFileCount = Directory.GetFiles(outputFolder, "*.wav", SearchOption.TopDirectoryOnly).Length + 1;
            OutputFilePath = Path.Combine(_outputFolder, $"{WavFileCount}.wav");
            Directory.CreateDirectory(_outputFolder);
        }

        public void StartRecording()
        {
             _waveIn = new WaveInEvent {WaveFormat = new WaveFormat(16000, 16, 1)};

            _waveIn.DataAvailable += (s, a) =>
            {
                _waveFileWriter.Write(a.Buffer, 0, a.BytesRecorded);
            };

            _waveIn.RecordingStopped += (s, a) =>
            {
                _waveFileWriter?.Dispose();
                _waveFileWriter = null;
                _waveIn.Dispose();
            };
            _waveFileWriter = new WaveFileWriter(Path.Combine(_outputFolder, $"{WavFileCount+1}.wav"), _waveIn.WaveFormat);
            _waveIn.StartRecording();
        }

        public void StopRecording()
        {
            _waveIn.StopRecording();
        }
    }
}