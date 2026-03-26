using Newtonsoft.Json;
using QScalp.Shared.Models;

namespace QScalp.Shared.Protocol
{
    public class TickerConfigPayload
    {
        [JsonProperty("ticker")]
        public string Ticker { get; set; }

        [JsonProperty("secKey")]
        public string SecKey { get; set; }

        [JsonProperty("priceRatio")]
        public int PriceRatio { get; set; }

        [JsonProperty("priceStep")]
        public int PriceStep { get; set; }
    }

    public class SnapshotPayload
    {
        [JsonProperty("config")]
        public TickerConfigPayload Config { get; set; }

        [JsonProperty("quotes")]
        public Quote[] Quotes { get; set; }

        [JsonProperty("spread")]
        public Spread Spread { get; set; }

        [JsonProperty("trades")]
        public TradeUpdatePayload[] RecentTrades { get; set; }
    }
}
