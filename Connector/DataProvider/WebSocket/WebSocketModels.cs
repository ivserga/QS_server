// ==========================================================================
//    WebSocketModels.cs - DTO для WebSocket сообщений
// ==========================================================================

using Newtonsoft.Json;

namespace QScalp.Connector.WebSocket
{
    // ************************************************************************
    // *                         Base Message                                 *
    // ************************************************************************

    /// <summary>
    /// Базовый класс для WebSocket сообщений
    /// </summary>
    class WsMessage
    {
        /// <summary>
        /// Тип события: "Q" = quote, "T" = trade, "status" = статус
        /// </summary>
        [JsonProperty("ev")]
        public string EventType { get; set; }
    }

    // ************************************************************************
    // *                         Quote Message                                *
    // ************************************************************************

    /// <summary>
    /// WebSocket Quote (NBBO) - WS /stocks/Q
    /// </summary>
    class WsQuote : WsMessage
    {
        /// <summary>
        /// Ticker symbol
        /// </summary>
        [JsonProperty("sym")]
        public string Symbol { get; set; }

        /// <summary>
        /// Bid exchange ID
        /// </summary>
        [JsonProperty("bx")]
        public int BidExchange { get; set; }

        /// <summary>
        /// Bid price
        /// </summary>
        [JsonProperty("bp")]
        public double BidPrice { get; set; }

        /// <summary>
        /// Bid size (in round lots, 1 = 100 shares)
        /// </summary>
        [JsonProperty("bs")]
        public int BidSize { get; set; }

        /// <summary>
        /// Ask exchange ID
        /// </summary>
        [JsonProperty("ax")]
        public int AskExchange { get; set; }

        /// <summary>
        /// Ask price
        /// </summary>
        [JsonProperty("ap")]
        public double AskPrice { get; set; }

        /// <summary>
        /// Ask size (in round lots, 1 = 100 shares)
        /// </summary>
        [JsonProperty("as")]
        public int AskSize { get; set; }

        /// <summary>
        /// Condition
        /// </summary>
        [JsonProperty("c")]
        public int Condition { get; set; }

        /// <summary>
        /// Indicators array
        /// </summary>
        [JsonProperty("i")]
        public int[] Indicators { get; set; }

        /// <summary>
        /// SIP timestamp in Unix MS
        /// </summary>
        [JsonProperty("t")]
        public long Timestamp { get; set; }

        /// <summary>
        /// Sequence number (unique per ticker per day)
        /// </summary>
        [JsonProperty("q")]
        public int SequenceNumber { get; set; }

        /// <summary>
        /// Tape (1 = NYSE, 2 = AMEX, 3 = Nasdaq)
        /// </summary>
        [JsonProperty("z")]
        public int Tape { get; set; }
    }

    // ************************************************************************
    // *                         Trade Message                                *
    // ************************************************************************

    /// <summary>
    /// WebSocket Trade - WS /stocks/T
    /// </summary>
    class WsTrade : WsMessage
    {
        /// <summary>
        /// Ticker symbol
        /// </summary>
        [JsonProperty("sym")]
        public string Symbol { get; set; }

        /// <summary>
        /// Exchange ID
        /// </summary>
        [JsonProperty("x")]
        public int Exchange { get; set; }

        /// <summary>
        /// Trade ID
        /// </summary>
        [JsonProperty("i")]
        public string TradeId { get; set; }

        /// <summary>
        /// Tape (1 = NYSE, 2 = AMEX, 3 = Nasdaq)
        /// </summary>
        [JsonProperty("z")]
        public int Tape { get; set; }

        /// <summary>
        /// Price
        /// </summary>
        [JsonProperty("p")]
        public double Price { get; set; }

        /// <summary>
        /// Trade size
        /// </summary>
        [JsonProperty("s")]
        public int Size { get; set; }

        /// <summary>
        /// Trade conditions
        /// </summary>
        [JsonProperty("c")]
        public int[] Conditions { get; set; }

        /// <summary>
        /// SIP timestamp in Unix MS
        /// </summary>
        [JsonProperty("t")]
        public long Timestamp { get; set; }

        /// <summary>
        /// Sequence number
        /// </summary>
        [JsonProperty("q")]
        public int SequenceNumber { get; set; }

        /// <summary>
        /// Trade Reporting Facility ID
        /// </summary>
        [JsonProperty("trfi")]
        public int? TrfId { get; set; }

        /// <summary>
        /// TRF Timestamp in Unix MS
        /// </summary>
        [JsonProperty("trft")]
        public long? TrfTimestamp { get; set; }
    }

    // ************************************************************************
    // *                        Status Messages                               *
    // ************************************************************************

    /// <summary>
    /// Статусное сообщение от сервера
    /// </summary>
    class WsStatus : WsMessage
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    /// <summary>
    /// Сообщение подписки для отправки на сервер
    /// </summary>
    class WsSubscribe
    {
        [JsonProperty("action")]
        public string Action { get; set; } = "subscribe";

        [JsonProperty("params")]
        public string Params { get; set; }
    }
}
