using Newtonsoft.Json;

namespace QScalp.Server.Connector.RestApi
{
    class QuotesResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("next_url")]
        public string NextUrl { get; set; }

        [JsonProperty("results")]
        public QuoteResult[] Results { get; set; }
    }

    class QuoteResult
    {
        [JsonProperty("ask_price")]
        public double AskPrice { get; set; }

        [JsonProperty("ask_size")]
        public double AskSize { get; set; }

        [JsonProperty("bid_price")]
        public double BidPrice { get; set; }

        [JsonProperty("bid_size")]
        public double BidSize { get; set; }

        [JsonProperty("sip_timestamp")]
        public long SipTimestamp { get; set; }

        [JsonProperty("sequence_number")]
        public int SequenceNumber { get; set; }

        [JsonProperty("tape")]
        public int Tape { get; set; }
    }

    // ************************************************************************

    class TradesResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("next_url")]
        public string NextUrl { get; set; }

        [JsonProperty("results")]
        public TradeResult[] Results { get; set; }
    }

    class TradeResult
    {
        [JsonProperty("price")]
        public double Price { get; set; }

        [JsonProperty("size")]
        public double Size { get; set; }

        [JsonProperty("sip_timestamp")]
        public long SipTimestamp { get; set; }

        [JsonProperty("sequence_number")]
        public int SequenceNumber { get; set; }

        [JsonProperty("tape")]
        public int Tape { get; set; }
    }
}
