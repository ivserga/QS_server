using Newtonsoft.Json;

namespace QScalp.Shared.Protocol
{
    public enum ClientCommandType
    {
        Subscribe,
        Unsubscribe,
        RequestSnapshot,
        FillTradeTicket
    }

    /// <summary>
    /// Команда от клиента серверу.
    /// </summary>
    public class ClientCommand
    {
        [JsonProperty("type")]
        public ClientCommandType Type { get; set; }

        [JsonProperty("ticker")]
        public string Ticker { get; set; }

        [JsonProperty("secKey")]
        public string SecKey { get; set; }

        /// <summary>Buy/Sell для заполнения Trade Ticket.</summary>
        [JsonProperty("side")]
        public string Side { get; set; }

        /// <summary>Количество акций для Trade Ticket.</summary>
        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        /// <summary>Сила сигнала, по которому был создан тикет (для лога).</summary>
        [JsonProperty("signalStrength")]
        public double SignalStrength { get; set; }

        /// <summary>Описание сигнала, по которому был создан тикет (для лога).</summary>
        [JsonProperty("signalMessage")]
        public string SignalMessage { get; set; }
    }
}
