using System.Runtime.Serialization;

namespace MeetingMinutesBot.UiPathDomain
{
    public enum UiPathEventType
    {

        [EnumMember(Value = "Amazon")]
        Amazon,
        [EnumMember(Value = "HelpDesk")]
        HelpDesk,
        [EnumMember(Value = "Email")]
        Email,
        [EnumMember(Value = "")]
        None
    }
}