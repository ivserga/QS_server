using Newtonsoft.Json;

namespace QScalp.Shared.Protocol
{
    public enum ClientCommandType
    {
        Subscribe,
        Unsubscribe,
        RequestSnapshot
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
    }
}
