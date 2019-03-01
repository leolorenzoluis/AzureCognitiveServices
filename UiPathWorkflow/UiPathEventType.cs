using System.Runtime.Serialization;

namespace UiPathWorkflow
{
    public enum UiPathEventType
    {

        [EnumMember(Value = "Amazon")]
        Amazon,
        [EnumMember(Value = "HelpDesk")]
        HelpDesk,
        [EnumMember(Value = "Email")]
        Email,
        [EnumMember(Value = "MeetingMinutes")]
        MeetingMinutes,
        [EnumMember(Value = "")]
        None
    }
}