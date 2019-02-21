namespace MeetingMinutesBot.UiPathDomain
{
    public interface IUiPathJob
    {
        int JobId { get;  }
        string ServiceUrl { get; }
    }
}