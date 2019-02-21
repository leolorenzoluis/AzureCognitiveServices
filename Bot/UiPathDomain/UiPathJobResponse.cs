namespace MeetingMinutesBot.UiPathDomain
{
    public class UiPathJobResponse
    {
        public int JobId { get; set; }
        public string Message { get; set; }
        public UiPathEventType Type { get; set; }
    }
}