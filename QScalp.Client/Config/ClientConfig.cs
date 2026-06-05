// ==========================================================================
//  ClientConfig.cs — Корневой конфиг клиента (URL сервера + список тикеров).
// ==========================================================================

using System;
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;

namespace QScalp.Client.Config
{
    public sealed class ClientConfig
    {
        [JsonProperty("serverUrl")]
        public string ServerUrl { get; set; } = "ws://localhost:9500/";

        [JsonProperty("tickers")]
        public List<TickerConfig> Tickers { get; set; } = new List<TickerConfig>();

        // ********************************************************************

        static readonly string DefaultPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "client.json");

        public static ClientConfig Load(string path = null)
        {
            path = path ?? DefaultPath;
            if (!File.Exists(path)) return new ClientConfig();

            try
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<ClientConfig>(json) ?? new ClientConfig();
            }
            catch
            {
                return new ClientConfig();
            }
        }

        public void Save(string path = null)
        {
            path = path ?? DefaultPath;
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }
}
