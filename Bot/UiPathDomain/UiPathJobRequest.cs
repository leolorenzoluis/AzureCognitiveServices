namespace MeetingMinutesBot.UiPathDomain
{
    public class UiPathJobRequest : IUiPathJob
    {
        public int JobId { get; }
        public string ServiceUrl { get; }

        public UiPathJobRequest(int jobId, string serviceUrl)
        {
            JobId = jobId;
            ServiceUrl = serviceUrl;
        }
    }
}