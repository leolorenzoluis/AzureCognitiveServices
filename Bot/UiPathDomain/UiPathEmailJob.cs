namespace MeetingMinutesBot.UiPathDomain
{
    public class UiPathEmailJob : IUiPathJob
    {
        public UiPathEmailJob(int jobId, string serviceUrl, string emailSubject, string emailBody, string emailToSend)
        {
            JobId = jobId;
            ServiceUrl = serviceUrl;
            EmailSubject = emailSubject;
            EmailBody = emailBody;
            EmailToSend = emailToSend;
        }

        public int JobId { get; }
        public string ServiceUrl { get; }
        public string EmailToSend { get; }
        public string EmailBody { get; }
        public string EmailSubject { get; }
    }
}