﻿namespace UiPathWorkflow
{
    public class UiPathEmailJob : UiPathArguments
    {
        public UiPathEmailJob(string jobId, string serviceUrl, string emailSubject, string emailBody, string emailToSend)
        {
            JobId = jobId;
            ServiceUrl = serviceUrl;
            EmailSubject = emailSubject;
            EmailBody = emailBody;
            EmailToSend = emailToSend;
        }

        public string EmailToSend { get; }
        public string EmailBody { get; }
        public string EmailSubject { get; }
    }
}