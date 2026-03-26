using System;
using System.IO;
using Newtonsoft.Json;

namespace QScalp.Server.Config
{
    public class ServerConfig
    {
        public string ApiBaseUrl { get; set; } = "https://api.massive.com";
        public string WsBaseUrl { get; set; } = "wss://socket.massive.com/stocks";
        public string ApiKey { get; set; } = "";
        public string ListenUrl { get; set; } = "http://localhost:9500/";
        public int MaxClients { get; set; } = 10;
        public bool DebugMode { get; set; } = false;

        // ********************************************************************

        private static readonly string ConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "server.json");

        public static ServerConfig Load()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    var json = File.ReadAllText(ConfigPath);
                    return JsonConvert.DeserializeObject<ServerConfig>(json) ?? new ServerConfig();
                }
                catch
                {
                    return new ServerConfig();
                }
            }
            return new ServerConfig();
        }

        public void Save()
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }
    }
}
