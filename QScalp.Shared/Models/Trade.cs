using System;
using Newtonsoft.Json;

namespace QScalp.Shared.Models
{
    public struct Trade
    {
        [JsonProperty("ip")]
        public int IntPrice;

        [JsonProperty("rp")]
        public double RawPrice;

        [JsonProperty("q")]
        public int Quantity;

        [JsonProperty("op")]
        public TradeOp Op;

        [JsonProperty("dt")]
        public DateTime DateTime;
    }
}
