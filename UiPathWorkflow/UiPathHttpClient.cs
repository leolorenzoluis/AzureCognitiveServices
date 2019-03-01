using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static System.Console;

namespace UiPathWorkflow
{
    public class UiPathHttpClient
    {
        public string UserName { get; }
        public string Password { get; }
        public string TenancyName { get; }

        public UiPathHttpClient(string userName, string password, string tenancyName)
        {
            UserName = userName;
            Password = password;
            TenancyName = tenancyName;
        }

        public async Task SendUiPathJob(UiPathArguments uiPathArguments,
            string uiJobReleaseKey, CancellationToken cancellationToken = default(CancellationToken))
        {
            WriteLine("===== Initializing UIPath Robot =====");
            var uiPathRequestBody = new UiPathAuthenticateBody
            {
                UserName = UserName,
                Password = Password,
                TenancyName = TenancyName
            };
            using (var client = new HttpClient())
            {
                var response = await client.PostAsync(
                    "https://platform.uipath.com/api/account/authenticate",
                    new StringContent(JsonConvert.SerializeObject(uiPathRequestBody), Encoding.UTF8,
                        "application/json"));
                var uiPathResponse =
                    JsonConvert.DeserializeObject<UiPathAuthenticateResponse>(
                        await response.Content.ReadAsStringAsync());

                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", uiPathResponse.Token);
                // I can make another request to get the release key and robot id. For demo purposes I wont.
                var uiPathStartJobRequest = new UiPathJobRequest
                {
                    StartInfo = new UiPathStartInfo()
                    {
                        InputArguments = JsonConvert.SerializeObject(uiPathArguments),
                        JobsCount = 0,
                        ReleaseKey = uiJobReleaseKey,
                        RobotIds = new List<int> {110294},
                        Strategy = "Specific"
                    }
                };
                var jobResponse = await client.PostAsync(
                    "https://platform.uipath.com/odata/Jobs/UiPath.Server.Configuration.OData.StartJobs",
                    new StringContent(JsonConvert.SerializeObject(uiPathStartJobRequest),
                        Encoding.UTF8, "application/json"));
                WriteLine($"Job Response: {jobResponse.StatusCode}");
                WriteLine(
                    $"Job Content: {JsonConvert.SerializeObject(jobResponse.Content)}");
            }
        }
    }
}
