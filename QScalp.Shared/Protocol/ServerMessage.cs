using Newtonsoft.Json;

namespace QScalp.Shared.Protocol
{
    public enum ServerMessageType
    {
        StockUpdate,
        TradeUpdate,
        SpreadUpdate,
        Snapshot,
        TickerConfig,
        ServerMessage,
        ClientList
    }

    /// <summary>
    /// Сообщение от сервера к клиенту.
    /// Единый формат для всех типов данных.
    /// </summary>
    public class ServerMessage
    {
        [JsonProperty("type")]
        public ServerMessageType Type { get; set; }

        [JsonProperty("ticker")]
        public string Ticker { get; set; }

        [JsonProperty("payload")]
        public string Payload { get; set; }

        [JsonProperty("ts")]
        public long Timestamp { get; set; }
    }
}
