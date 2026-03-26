using Newtonsoft.Json;
using QScalp.Shared.Models;

namespace QScalp.Shared.Protocol
{
    public class StockUpdatePayload
    {
        [JsonProperty("quotes")]
        public Quote[] Quotes { get; set; }

        [JsonProperty("spread")]
        public Spread Spread { get; set; }
    }
}
