using Newtonsoft.Json;

namespace UiPathWorkflow
{
    public class UiPathAuthenticateResponse
    {
        [JsonProperty("result")] public string Token { get; set; }
    }
}