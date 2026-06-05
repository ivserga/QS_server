using System;
using System.IO;
using Newtonsoft.Json;

namespace QScalp.Server.Config
{
    public class ServerConfig
    {
        /// <summary>Massive — WebSocket massive.com; Striker — локальный HTTP API терминала Striker.</summary>
        public string DataSource { get; set; } = "Massive";

        public string ApiBaseUrl { get; set; } = "https://api.massive.com";
        public string WsBaseUrl { get; set; } = "wss://socket.massive.com/stocks";
        public string ApiKey { get; set; } = "";
        public string ListenUrl { get; set; } = "http://localhost:9500/";
        public int MaxClients { get; set; } = 10;
        public bool DebugMode { get; set; } = false;

        // ============ Snapshot policy ===========================================
        // По умолчанию все три флага выключены — сервер НЕ выполняет тяжёлую
        // REST-загрузку истории из massive.com и не шлёт большой снапшот по
        // подписке. Клиент получает только real-time stream после Subscribe.

        /// <summary>
        /// Если true — сервер при первой подписке на тикер делает REST-запрос
        /// LoadSnapshotAsync к API (затратно по трафику; история сделок за день).
        /// </summary>
        public bool LoadSnapshotFromApi { get; set; } = false;

        /// <summary>
        /// Если true — после Subscribe сервер автоматически отправляет клиенту
        /// сообщение Snapshot (содержимое буфера). Отключено по умолчанию.
        /// </summary>
        public bool AutoSnapshotOnSubscribe { get; set; } = false;

        /// <summary>
        /// Если true — обрабатывать клиентскую команду RequestSnapshot:
        /// при необходимости выполнить REST-загрузку (если LoadSnapshotFromApi
        /// тоже true) и отправить клиенту накопленный снапшот.
        /// </summary>
        public bool LoadSnapshotOnDemand { get; set; } = true;

        // ============ Striker (локальный HTTP API FTG.exe) =====================
        /// <summary>Базовый URL, например http://127.0.0.1:16239</summary>
        public string StrikerHttpBaseUrl { get; set; } = "http://127.0.0.1:16239";

        /// <summary>Период опроса GETLASTQUOTE (мс), не меньше 50.</summary>
        public int StrikerQuotePollMs { get; set; } = 500;

        /// <summary>Дополнительно опрашивать GETTIMESALES и слать TradeUpdate.</summary>
        public bool StrikerEnableTimeAndSales { get; set; } = true;

        // ============ Striker WS API (локальный WebSocket) =====================
        /// <summary>WebSocket URL Striker WS API, например ws://127.0.0.1:9700</summary>
        public string StrikerWsBaseUrl { get; set; } = "ws://127.0.0.1:9700";

        /// <summary>Пароль входа в Medved Trader / Striker (требуется командой connect).</summary>
        public string StrikerWsPassword { get; set; } = "";

        /// <summary>
        /// streamerID, который должен обслуживать данные (например BARCHART, IQFEED, AMTD).
        /// Если пусто — будет выбран первый стример с isSet=true из enumDataStreamers.
        /// </summary>
        public string StrikerWsStreamerId { get; set; } = "";

        // ============ Воспроизведение записи (QScalp.Recorder → JSONL) =========
        /// <summary>Каталог сессии (папка с TICKER.jsonl) или один .jsonl для режима Replay.</summary>
        public string ReplaySessionPath { get; set; } = "";

        /// <summary>Множитель скорости воспроизведения (1 = реальное время по меткам ts).</summary>
        public double ReplaySpeed { get; set; } = 1.0;

        /// <summary>Повторять запись с начала после окончания файла.</summary>
        public bool ReplayLoop { get; set; } = false;

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
