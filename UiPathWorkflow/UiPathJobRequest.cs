using Newtonsoft.Json;

namespace UiPathWorkflow
{
    public class UiPathJobRequest
    {
        [JsonProperty("startInfo")] public UiPathStartInfo StartInfo { get; set; }
    }
}