using System;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace MeetingMinutesBot
{
    public class StateBotAccessors
    {
        public StateBotAccessors(ConversationState conversationState, UserState userState)
        {
            ConversationState = conversationState ?? throw new ArgumentNullException(nameof(conversationState));
            UserState = userState ?? throw new ArgumentNullException(nameof(userState));
        }

        public static string UserName { get; } = "User";

        public IStatePropertyAccessor<User> UserAccessor { get; set; }

        public ConversationState ConversationState { get; }

        public UserState UserState { get; }

        public ConversationReference AudioRecorderConversationReference { get; set; }
    }
}