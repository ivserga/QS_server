using System;
using Newtonsoft.Json;

namespace QScalp.Shared.Protocol
{
    public static class ProtocolHelper
    {
        public static ServerMessage CreateMessage(ServerMessageType type, string ticker, object payload)
        {
            return new ServerMessage
            {
                Type = type,
                Ticker = ticker,
                Payload = JsonConvert.SerializeObject(payload),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public static string Serialize(ServerMessage msg)
        {
            return JsonConvert.SerializeObject(msg);
        }

        public static ServerMessage DeserializeServerMessage(string json)
        {
            return JsonConvert.DeserializeObject<ServerMessage>(json);
        }

        public static string SerializeCommand(ClientCommand cmd)
        {
            return JsonConvert.SerializeObject(cmd);
        }

        public static ClientCommand DeserializeCommand(string json)
        {
            return JsonConvert.DeserializeObject<ClientCommand>(json);
        }

        public static T DeserializePayload<T>(string payloadJson)
        {
            return JsonConvert.DeserializeObject<T>(payloadJson);
        }
    }
}
