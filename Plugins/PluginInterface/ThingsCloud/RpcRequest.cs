using Newtonsoft.Json;

namespace PluginInterface.ThingsCloud
{
    public class RpcRequest
    {
        [JsonProperty(PropertyName = "device")]
        public string DeviceName { get; set; }
        [JsonProperty(PropertyName = "command")]
        public RpcData RequestData { get; set; }
    }
    public class RpcData
    {
        [JsonProperty(PropertyName = "id")]
        public string RequestId { get; set; }
        [JsonProperty(PropertyName = "method")]
        public string Method { get; set; }
        [JsonProperty(PropertyName = "params")]
        public Dictionary<string, object> Params { get; set; }
    }
}
