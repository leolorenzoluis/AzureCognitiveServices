using Microsoft.Bot.Builder;

namespace MeetingMinutesBot
{
    public class JobState : BotState
    {
        private const string StorageKey = "MeetingMinutesBot.JobState";
        public JobState(IStorage storage) : base(storage, StorageKey)
        {
        }

        protected override string GetStorageKey(ITurnContext turnContext) => StorageKey;
    }
}