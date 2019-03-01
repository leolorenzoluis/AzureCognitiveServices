using System;
using Newtonsoft.Json;

namespace UiPathWorkflow
{
    public class UiPathAuthenticateBody
    {
        [JsonProperty("tenancyName")] public string TenancyName { get; set; }

        [JsonProperty("userNameOrEmailAddress")]
        public string UserName { get; set; }


        [JsonProperty("password")] public string Password { get; set; }
    }
}