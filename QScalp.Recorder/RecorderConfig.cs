using System;
using System.IO;
using Newtonsoft.Json;

namespace QScalp.Recorder
{
    public sealed class RecorderConfig
    {
        /// <summary>ws://127.0.0.1:9500/ (из http:// ListenUrl сервера).</summary>
        public string ServerUrl { get; set; } = "ws://127.0.0.1:9500/";

        public string[] Tickers { get; set; } = Array.Empty<string>();

        public string OutputDirectory { get; set; } = "Records";

        public bool RecordSnapshots { get; set; } = false;

        private static readonly string ConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "recorder.json");

        public static RecorderConfig Load()
        {
            if (!File.Exists(ConfigPath))
                return new RecorderConfig();

            try
            {
                return JsonConvert.DeserializeObject<RecorderConfig>(File.ReadAllText(ConfigPath))
                       ?? new RecorderConfig();
            }
            catch
            {
                return new RecorderConfig();
            }
        }

        public void Save()
        {
            File.WriteAllText(ConfigPath,
                JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public static string HttpListenUrlToWs(string listenUrl)
        {
            if (string.IsNullOrWhiteSpace(listenUrl))
                return "ws://127.0.0.1:9500/";

            var u = listenUrl.Trim();
            if (u.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
                u.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                return u;

            if (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                return "ws://" + u.Substring(7);
            if (u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return "wss://" + u.Substring(8);

            return "ws://" + u.TrimStart('/');
        }
    }
}
