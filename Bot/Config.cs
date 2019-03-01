using Microsoft.Bot.Configuration;

namespace MeetingMinutesBot
{
    public class Config
    {
        public string UiPathTenancyName { get;  }
        public string UiPathUserName { get; }
        public string UiPathPassword { get; }

        public Config(string uiPathTenancyName, string uiPathUserName, string uiPathPassword)
        {
            UiPathTenancyName = uiPathTenancyName;
            UiPathUserName = uiPathUserName;
            UiPathPassword = uiPathPassword;
        }
    }
}