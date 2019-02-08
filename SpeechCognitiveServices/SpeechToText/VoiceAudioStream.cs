using System.Threading;
using Microsoft.CognitiveServices.Speech.Audio;

namespace SpeechCognitiveServices.SpeechToText
{
    public class VoiceAudioStream : PullAudioInputStreamCallback
    {
        private readonly EchoStream _dataStream = new EchoStream();
        private ManualResetEvent _waitForEmptyDataStream;


        public override int Read(byte[] dataBuffer, uint size)
        {
            return !_dataStream.DataAvailable ? 0 : _dataStream.Read(dataBuffer, 0, dataBuffer.Length);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            _dataStream.Write(buffer, offset, count);
        }

        public override void Close()
        {
            if (_dataStream.DataAvailable)
            {
                _waitForEmptyDataStream = new ManualResetEvent(false);
                _waitForEmptyDataStream.WaitOne();
            }

            _waitForEmptyDataStream.Close();
            _dataStream.Dispose();
            base.Close();
        }
    }
}