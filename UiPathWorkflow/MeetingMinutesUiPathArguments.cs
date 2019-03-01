using System.Collections.Generic;

namespace UiPathWorkflow
{
    public class MeetingMinutesUiPathArguments : UiPathArguments
    {
        public List<string> DownloadUrls { get; } = new List<string>();
        public string SentimentFilePath { get; set; }
        public string TranscribedFilePath { get; set; }
        public string KeyPhrasesFilePath { get; set; }
        public string MinutesFilePath { get; set; }

        public string EmailToSend { get; set; }
        public string EmailBody { get; set; }
        public string EmailSubject { get; set; }
    }
}