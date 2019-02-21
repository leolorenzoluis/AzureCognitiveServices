using System.Collections.Generic;

namespace MeetingMinutesBot.UiPathDomain
{
    public class UiPathAmazonJob : IUiPathJob
    {
        public UiPathAmazonJob(int jobId, string serviceUrl, List<string> products)
        {
            JobId = jobId;
            ServiceUrl = serviceUrl;
            Products = products;
        }

        public List<string> Products { get; }
        public int JobId { get; }
        public string ServiceUrl { get;  }
    }
}