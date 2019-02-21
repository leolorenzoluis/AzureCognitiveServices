using System.Collections.Generic;
using Microsoft.Bot.Schema;

namespace MeetingMinutesBot
{
    public class JobStorage : Dictionary<long, Job>
    {
    }

    public class Job
    {
        public int Id { get; set; } = 0;
        public bool Completed { get; set; } = false;
        public ConversationReference ConversationReference { get; set; }
    }
}