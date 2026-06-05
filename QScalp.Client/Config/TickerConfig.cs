// ==========================================================================
//  TickerConfig.cs — Конфиг анализа одного тикера.
// ==========================================================================

using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using QScalp.Shared.Models;

namespace QScalp.Client.Config
{
    public sealed class TickerConfig
    {
        [JsonProperty("ticker")]
        public string Ticker { get; set; }

        [JsonProperty("secKey")]
        public string SecKey { get; set; }

        /// <summary>
        /// Шаг цены в IntPrice-единицах сервера. QScalp.Server отдаёт IntPrice =
        /// raw * 1_000_000, поэтому для актива с шагом 0.01 это 10000.
        /// </summary>
        [JsonProperty("priceStep")]
        public int PriceStep { get; set; } = 10000;

        [JsonProperty("clusterBase")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ClusterBase ClusterBase { get; set; } = ClusterBase.Time;

        /// <summary>
        /// Размер кластера в единицах ClusterBase: для Time — секунды,
        /// для Volume/Range/Ticks/Delta — соответствующая величина.
        /// </summary>
        [JsonProperty("clusterSize")]
        public int ClusterSize { get; set; } = 60;

        [JsonProperty("historyCapacity")]
        public int HistoryCapacity { get; set; } = 64;

        /// <summary>
        /// Минимальный интервал между повторными сигналами одного типа
        /// (Source + Kind + Direction) в секундах. Защищает UI/CSV от
        /// "дребезга", когда условие детектора держится несколько кластеров
        /// подряд.
        /// </summary>
        [JsonProperty("minSignalIntervalSeconds")]
        public int MinSignalIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Минимальное число закрытых кластеров между повторными сигналами
        /// одного типа. Работает вместе с MinSignalIntervalSeconds.
        /// </summary>
        [JsonProperty("minBarsBetweenRepeatedSignals")]
        public int MinBarsBetweenRepeatedSignals { get; set; } = 3;

        /// <summary>
        /// Максимум сигналов, который SignalBus может пропустить наружу на
        /// одном закрытом кластере. Если несколько детекторов сработали
        /// одновременно, наружу уходят первые по порядку регистрации.
        /// </summary>
        [JsonProperty("maxSignalsPerCluster")]
        public int MaxSignalsPerCluster { get; set; } = 3;

        /// <summary>
        /// Минимальная сила сигнала для публикации. 0.0 — не фильтровать.
        /// </summary>
        [JsonProperty("minSignalStrength")]
        public double MinSignalStrength { get; set; } = 0.0;

        /// <summary>
        /// Если true — при подходящем сигнале клиент попросит сервер заполнить
        /// Trade Ticket в Striker. Ордер не отправляется автоматически.
        /// </summary>
        [JsonProperty("autoFillTradeTicket")]
        public bool AutoFillTradeTicket { get; set; } = false;

        /// <summary>Минимальная сила сигнала для заполнения Trade Ticket.</summary>
        [JsonProperty("tradeTicketMinSignalStrength")]
        public double TradeTicketMinSignalStrength { get; set; } = 0.75;

        /// <summary>Количество акций для Trade Ticket.</summary>
        [JsonProperty("tradeTicketQuantity")]
        public int TradeTicketQuantity { get; set; } = 100;

        [JsonProperty("logToCsv")]
        public bool LogToCsv { get; set; } = false;

        /// <summary>
        /// Если true — каждый закрытый кластер дописывается в JSONL-файл
        /// clusters\clusters_{ТИКЕР}.jsonl (формат совместим с WPF ClusterExporter).
        /// </summary>
        [JsonProperty("exportClustersToJson")]
        public bool ExportClustersToJson { get; set; } = false;

        [JsonProperty("detectors")]
        public List<DetectorConfig> Detectors { get; set; } = new List<DetectorConfig>();

        /// <summary>Legacy 3-bar absorption: доля объёма выше/ниже ref.</summary>
        [JsonProperty("legacyAbsorptionVolumeRatioThreshold")]
        public double LegacyAbsorptionVolumeRatioThreshold { get; set; } = 0.68;

        /// <summary>Legacy 3-bar absorption: объём c3 vs c2.</summary>
        [JsonProperty("legacyAbsorptionVolumeMultiplier")]
        public double LegacyAbsorptionVolumeMultiplier { get; set; } = 1.35;
    }
}
