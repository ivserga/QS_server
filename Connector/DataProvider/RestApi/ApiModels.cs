// ==========================================================================
//    ApiModels.cs - DTO для REST API ответов
// ==========================================================================

using Newtonsoft.Json;

namespace QScalp.Connector.RestApi
{
    // ************************************************************************
    // *                         Quotes Response                              *
    // ************************************************************************

    class QuotesResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("request_id")]
        public string RequestId { get; set; }

        [JsonProperty("next_url")]
        public string NextUrl { get; set; }

        [JsonProperty("results")]
        public QuoteResult[] Results { get; set; }
    }

    class QuoteResult
    {
        [JsonProperty("ask_exchange")]
        public int AskExchange { get; set; }

        [JsonProperty("ask_price")]
        public double AskPrice { get; set; }

        [JsonProperty("ask_size")]
        public double AskSize { get; set; }

        [JsonProperty("bid_exchange")]
        public int BidExchange { get; set; }

        [JsonProperty("bid_price")]
        public double BidPrice { get; set; }

        [JsonProperty("bid_size")]
        public double BidSize { get; set; }

        [JsonProperty("conditions")]
        public int[] Conditions { get; set; }

        [JsonProperty("indicators")]
        public int[] Indicators { get; set; }

        [JsonProperty("participant_timestamp")]
        public long ParticipantTimestamp { get; set; }

        [JsonProperty("sip_timestamp")]
        public long SipTimestamp { get; set; }

        [JsonProperty("trf_timestamp")]
        public long TrfTimestamp { get; set; }

        [JsonProperty("sequence_number")]
        public int SequenceNumber { get; set; }

        [JsonProperty("tape")]
        public int Tape { get; set; }
    }

    // ************************************************************************
    // *                         Trades Response                              *
    // ************************************************************************

    class TradesResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("request_id")]
        public string RequestId { get; set; }

        [JsonProperty("next_url")]
        public string NextUrl { get; set; }

        [JsonProperty("results")]
        public TradeResult[] Results { get; set; }
    }

    class TradeResult
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("price")]
        public double Price { get; set; }

        [JsonProperty("size")]
        public double Size { get; set; }

        [JsonProperty("exchange")]
        public int Exchange { get; set; }

        [JsonProperty("conditions")]
        public int[] Conditions { get; set; }

        [JsonProperty("correction")]
        public int? Correction { get; set; }

        [JsonProperty("participant_timestamp")]
        public long ParticipantTimestamp { get; set; }

        [JsonProperty("sip_timestamp")]
        public long SipTimestamp { get; set; }

        [JsonProperty("trf_timestamp")]
        public long? TrfTimestamp { get; set; }

        [JsonProperty("trf_id")]
        public int? TrfId { get; set; }

        [JsonProperty("sequence_number")]
        public int SequenceNumber { get; set; }

        [JsonProperty("tape")]
        public int Tape { get; set; }
    }
}
