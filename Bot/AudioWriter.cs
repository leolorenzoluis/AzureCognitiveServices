using System;
using System.IO;
using NAudio.Wave;

namespace MeetingMinutesBot
{
    public class AudioWriter
    {
        private WaveFileWriter _waveFileWriter;
        private WaveInEvent _waveIn;
        private string _outputFilePath;

        public void StartRecording()
        {
            var outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),"MeetingMinutes");
            Directory.CreateDirectory(outputFolder);

            var wavFileCount = Directory.GetFiles(outputFolder, "*.wav", SearchOption.TopDirectoryOnly).Length;
            _outputFilePath = Path.Combine(outputFolder, $"{wavFileCount+1}.wav");
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
            _waveFileWriter = new WaveFileWriter(_outputFilePath, _waveIn.WaveFormat);
            _waveIn.StartRecording();
        }

        public void StopRecording()
        {
            _waveIn.StopRecording();
        }
    }
}