using Newtonsoft.Json;

namespace QScalp.Server.Connector.WebSocket
{
    class WsQuote
    {
        [JsonProperty("sym")]
        public string Symbol { get; set; }

        [JsonProperty("bp")]
        public double BidPrice { get; set; }

        [JsonProperty("bs")]
        public int BidSize { get; set; }

        [JsonProperty("ap")]
        public double AskPrice { get; set; }

        [JsonProperty("as")]
        public int AskSize { get; set; }

        [JsonProperty("bx")]
        public int BidExchange { get; set; }

        [JsonProperty("ax")]
        public int AskExchange { get; set; }

        [JsonProperty("t")]
        public long Timestamp { get; set; }

        [JsonProperty("q")]
        public int SequenceNumber { get; set; }

        [JsonProperty("z")]
        public int Tape { get; set; }
    }

    class WsTrade
    {
        [JsonProperty("sym")]
        public string Symbol { get; set; }

        [JsonProperty("p")]
        public double Price { get; set; }

        [JsonProperty("s")]
        public int Size { get; set; }

        [JsonProperty("x")]
        public int Exchange { get; set; }

        [JsonProperty("i")]
        public string TradeId { get; set; }

        [JsonProperty("t")]
        public long Timestamp { get; set; }

        [JsonProperty("q")]
        public int SequenceNumber { get; set; }

        [JsonProperty("z")]
        public int Tape { get; set; }

        [JsonProperty("c")]
        public int[] Conditions { get; set; }

        [JsonProperty("trfi")]
        public int? TrfId { get; set; }

        [JsonProperty("trft")]
        public long? TrfTimestamp { get; set; }
    }
}
