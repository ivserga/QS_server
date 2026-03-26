using Newtonsoft.Json;
using QScalp.Shared.Models;

namespace QScalp.Shared.Protocol
{
    public class TradeUpdatePayload
    {
        [JsonProperty("secKey")]
        public string SecKey { get; set; }

        [JsonProperty("trade")]
        public Trade Trade { get; set; }
    }
}
