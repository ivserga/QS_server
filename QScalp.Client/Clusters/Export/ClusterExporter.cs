// ==========================================================================
//  ClusterExporter.cs — JSON-экспорт кластеров (совместим с WPF qscalp.cluster_export.v1).
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using Newtonsoft.Json;

using QScalp.Client.Config;

namespace QScalp.Client.Clusters.Export
{
    internal static class ClusterExporter
    {
        static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static int ExportDocument(string fileName, IList<Cluster> clusters, TickerConfig config,
            DateTime from, DateTime to)
        {
            var doc = new ClusterExportDocument(config, from, to);

            if (clusters != null)
                for (int i = 0; i < clusters.Count; i++)
                    doc.Clusters.Add(new ClusterExportItem(i + 1, clusters[i], config));

            var json = JsonConvert.SerializeObject(doc, Formatting.Indented);
            File.WriteAllText(fileName, json);

            return doc.Clusters.Count;
        }

        public static string SerializeSessionHeader(TickerConfig config)
        {
            var header = new ClusterExportSessionHeader(config);
            return JsonConvert.SerializeObject(header, SerializerSettings);
        }

        public static string SerializeClusterLine(int index, Cluster cluster, TickerConfig config)
        {
            var item = new ClusterExportClusterLine(index, cluster, config);
            return JsonConvert.SerializeObject(item, SerializerSettings);
        }
    }

    // ==========================================================================

    sealed class ClusterExportSessionHeader
    {
        public ClusterExportSessionHeader(TickerConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        readonly TickerConfig Config;

        [JsonProperty("record")]
        public string Record => "session";

        [JsonProperty("schema")]
        public string Schema => "qscalp.cluster_export.v1";

        [JsonProperty("source")]
        public string Source => "QScalp.Client";

        [JsonProperty("created_at")]
        public string CreatedAt => ClusterExportDocument.FormatTime(DateTime.Now);

        [JsonProperty("instrument")]
        public ClusterExportInstrument Instrument => new ClusterExportInstrument(Config);

        [JsonProperty("settings")]
        public ClusterExportSettings Settings => new ClusterExportSettings(Config);

        [JsonProperty("volume_semantics")]
        public ClusterExportVolumeSemantics VolumeSemantics => new ClusterExportVolumeSemantics();
    }

    // ==========================================================================

    sealed class ClusterExportClusterLine
    {
        readonly ClusterExportItem _item;

        public ClusterExportClusterLine(int index, Cluster cluster, TickerConfig config)
        {
            _item = new ClusterExportItem(index, cluster, config);
        }

        [JsonProperty("record")]
        public string Record => "cluster";

        [JsonProperty("schema")]
        public string Schema => "qscalp.cluster_export.v1";

        [JsonProperty("source")]
        public string Source => "QScalp.Client";

        [JsonProperty("index")]
        public int Index => _item.Index;

        [JsonProperty("start_time")]
        public string StartTime => _item.StartTime;

        [JsonProperty("open_price_int")]
        public int OpenPrice => _item.OpenPrice;

        [JsonProperty("open_price")]
        public double RawOpenPrice => _item.RawOpenPrice;

        [JsonProperty("open_price_text")]
        public string OpenPriceText => _item.OpenPriceText;

        [JsonProperty("close_price_int")]
        public int ClosePrice => _item.ClosePrice;

        [JsonProperty("close_price")]
        public double RawClosePrice => _item.RawClosePrice;

        [JsonProperty("close_price_text")]
        public string ClosePriceText => _item.ClosePriceText;

        [JsonProperty("min_price_int")]
        public int MinPrice => _item.MinPrice;

        [JsonProperty("min_price")]
        public double RawMinPrice => _item.RawMinPrice;

        [JsonProperty("min_price_text")]
        public string MinPriceText => _item.MinPriceText;

        [JsonProperty("max_price_int")]
        public int MaxPrice => _item.MaxPrice;

        [JsonProperty("max_price")]
        public double RawMaxPrice => _item.RawMaxPrice;

        [JsonProperty("max_price_text")]
        public string MaxPriceText => _item.MaxPriceText;

        [JsonProperty("total_volume")]
        public int TotalVolume => _item.TotalVolume;

        [JsonProperty("ticks")]
        public int Ticks => _item.Ticks;

        [JsonProperty("delta")]
        public int Delta => _item.Delta;

        [JsonProperty("bid_volume")]
        public int BidVolume => _item.BidVolume;

        [JsonProperty("ask_volume")]
        public int AskVolume => _item.AskVolume;

        [JsonProperty("inside_spread_volume")]
        public int InsideSpreadVolume => _item.InsideSpreadVolume;

        [JsonProperty("price_levels")]
        public IList<ClusterPriceLevel> PriceLevels => _item.PriceLevels;
    }

    // ==========================================================================

    sealed class ClusterExportDocument
    {
        public ClusterExportDocument(TickerConfig config, DateTime from, DateTime to)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Period = new ClusterExportPeriod(from, to);
            Instrument = new ClusterExportInstrument(config);
            Settings = new ClusterExportSettings(config);
            VolumeSemantics = new ClusterExportVolumeSemantics();
            Clusters = new List<ClusterExportItem>();
        }

        [JsonProperty("schema")]
        public string Schema => "qscalp.cluster_export.v1";

        [JsonProperty("source")]
        public string Source => "QScalp.Client";

        [JsonProperty("created_at")]
        public string CreatedAt => FormatTime(DateTime.Now);

        [JsonProperty("instrument")]
        public ClusterExportInstrument Instrument { get; private set; }

        [JsonProperty("period")]
        public ClusterExportPeriod Period { get; private set; }

        [JsonProperty("settings")]
        public ClusterExportSettings Settings { get; private set; }

        [JsonProperty("volume_semantics")]
        public ClusterExportVolumeSemantics VolumeSemantics { get; private set; }

        [JsonProperty("clusters")]
        public List<ClusterExportItem> Clusters { get; private set; }

        internal static string FormatTime(DateTime time)
        {
            return time.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);
        }
    }

    // ==========================================================================

    sealed class ClusterExportInstrument
    {
        readonly TickerConfig _config;

        public ClusterExportInstrument(TickerConfig config)
        {
            _config = config;
        }

        [JsonProperty("sec_code")]
        public string SecCode => _config.Ticker;

        [JsonProperty("class_code")]
        public string ClassCode => _config.SecKey ?? _config.Ticker;
    }

    // ==========================================================================

    sealed class ClusterExportPeriod
    {
        public ClusterExportPeriod(DateTime from, DateTime to)
        {
            From = from;
            To = to;
        }

        [JsonIgnore]
        public DateTime From { get; private set; }

        [JsonIgnore]
        public DateTime To { get; private set; }

        [JsonProperty("from")]
        public string FromText => ClusterExportDocument.FormatTime(From);

        [JsonProperty("to")]
        public string ToText => ClusterExportDocument.FormatTime(To);
    }

    // ==========================================================================

    sealed class ClusterExportSettings
    {
        readonly TickerConfig _config;

        public ClusterExportSettings(TickerConfig config)
        {
            _config = config;
        }

        [JsonProperty("price_ratio")]
        public int PriceRatio => IntPrice.DefaultRatio;

        [JsonProperty("price_step_int")]
        public int PriceStep => _config.PriceStep;

        [JsonProperty("price_step")]
        public double RawPriceStep => IntPrice.GetRaw(_config.PriceStep);

        [JsonProperty("cluster_base")]
        public string ClusterBase => _config.ClusterBase.ToString();

        [JsonProperty("cluster_size")]
        public int ClusterSize => _config.ClusterSize;
    }

    // ==========================================================================

    sealed class ClusterExportVolumeSemantics
    {
        [JsonProperty("bid")]
        public string Bid => "seller initiated volume, printed at bid";

        [JsonProperty("ask")]
        public string Ask => "buyer initiated volume, printed at ask";

        [JsonProperty("inside_spread")]
        public string InsideSpread => "neutral volume printed inside spread or with unknown side";

        [JsonProperty("delta")]
        public string Delta => "ask - bid; inside_spread is not included";
    }

    // ==========================================================================

    sealed class ClusterExportItem
    {
        readonly Cluster _cluster;
        readonly TickerConfig _config;
        readonly IList<ClusterPriceLevel> _levels;
        readonly int _bidVolume;
        readonly int _askVolume;
        readonly int _insideSpreadVolume;

        public ClusterExportItem(int index, Cluster cluster, TickerConfig config)
        {
            _cluster = cluster;
            _config = config;
            Index = index;

            _levels = cluster.GetPriceLevels(config.PriceStep);

            for (int i = 0; i < _levels.Count; i++)
            {
                _bidVolume += _levels[i].BidVolume;
                _askVolume += _levels[i].AskVolume;
                _insideSpreadVolume += _levels[i].InsideSpreadVolume;
            }
        }

        [JsonProperty("index")]
        public int Index { get; private set; }

        [JsonProperty("start_time")]
        public string StartTime => ClusterExportDocument.FormatTime(_cluster.DateTime);

        [JsonProperty("open_price_int")]
        public int OpenPrice => _cluster.OpenPrice;

        [JsonProperty("open_price")]
        public double RawOpenPrice => IntPrice.GetRaw(_cluster.OpenPrice);

        [JsonProperty("open_price_text")]
        public string OpenPriceText => IntPrice.GetString(_cluster.OpenPrice, _config.PriceStep);

        [JsonProperty("close_price_int")]
        public int ClosePrice => _cluster.ClosePrice;

        [JsonProperty("close_price")]
        public double RawClosePrice => IntPrice.GetRaw(_cluster.ClosePrice);

        [JsonProperty("close_price_text")]
        public string ClosePriceText => IntPrice.GetString(_cluster.ClosePrice, _config.PriceStep);

        [JsonProperty("min_price_int")]
        public int MinPrice => _cluster.MinPrice;

        [JsonProperty("min_price")]
        public double RawMinPrice => IntPrice.GetRaw(_cluster.MinPrice);

        [JsonProperty("min_price_text")]
        public string MinPriceText => IntPrice.GetString(_cluster.MinPrice, _config.PriceStep);

        [JsonProperty("max_price_int")]
        public int MaxPrice => _cluster.MaxPrice;

        [JsonProperty("max_price")]
        public double RawMaxPrice => IntPrice.GetRaw(_cluster.MaxPrice);

        [JsonProperty("max_price_text")]
        public string MaxPriceText => IntPrice.GetString(_cluster.MaxPrice, _config.PriceStep);

        [JsonProperty("total_volume")]
        public int TotalVolume => _cluster.Volume;

        [JsonProperty("ticks")]
        public int Ticks => _cluster.Ticks;

        [JsonProperty("delta")]
        public int Delta => _cluster.Delta;

        [JsonProperty("bid_volume")]
        public int BidVolume => _bidVolume;

        [JsonProperty("ask_volume")]
        public int AskVolume => _askVolume;

        [JsonProperty("inside_spread_volume")]
        public int InsideSpreadVolume => _insideSpreadVolume;

        [JsonProperty("price_levels")]
        public IList<ClusterPriceLevel> PriceLevels => _levels;
    }
}
