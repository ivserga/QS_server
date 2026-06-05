// ==========================================================================
//  ClusterExporter.cs - JSON export of cluster data for LLM analysis.
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using Newtonsoft.Json;

namespace QScalp.View.ClustersSpace
{
  static class ClusterExporter
  {
    // **********************************************************************

    public static int Export(string fileName, IList<Cluster> clusters, DateTime from, DateTime to)
    {
      ClusterExportDocument doc = new ClusterExportDocument(from, to);

      if(clusters != null)
        for(int i = 0; i < clusters.Count; i++)
          doc.Clusters.Add(new ClusterExportItem(i + 1, clusters[i]));

      string json = JsonConvert.SerializeObject(doc, Formatting.Indented);
      File.WriteAllText(fileName, json);

      return doc.Clusters.Count;
    }

    // **********************************************************************
    //  Streaming-friendly serialization (one record per call) for live
    //  LLM analysis. Compatible with qscalp.cluster_export.v1 schema.
    //  Compact JSON (Formatting.None + NullValueHandling.Ignore) — экономит
    //  токены при отправке в chat API.
    // **********************************************************************

    static readonly JsonSerializerSettings CompactSettings = new JsonSerializerSettings
    {
      Formatting = Formatting.None,
      NullValueHandling = NullValueHandling.Ignore
    };

    public static string SerializeSessionHeader()
    {
      return JsonConvert.SerializeObject(new ClusterExportSessionHeader(), CompactSettings);
    }

    public static string SerializeClusterLine(int index, Cluster cluster)
    {
      return JsonConvert.SerializeObject(new ClusterExportClusterLine(index, cluster), CompactSettings);
    }

    // **********************************************************************
  }

  // ==========================================================================

  sealed class ClusterExportSessionHeader
  {
    [JsonProperty("record")]
    public string Record { get { return "session"; } }

    [JsonProperty("schema")]
    public string Schema { get { return "qscalp.cluster_export.v1"; } }

    [JsonProperty("source")]
    public string Source { get { return "QScalp.WPF"; } }

    [JsonProperty("created_at")]
    public string CreatedAt { get { return ClusterExportDocument.FormatTime(DateTime.Now); } }

    [JsonProperty("instrument")]
    public ClusterExportInstrument Instrument { get { return new ClusterExportInstrument(); } }

    [JsonProperty("settings")]
    public ClusterExportSettings Settings { get { return new ClusterExportSettings(); } }

    [JsonProperty("volume_semantics")]
    public ClusterExportVolumeSemantics VolumeSemantics { get { return new ClusterExportVolumeSemantics(); } }
  }

  // ==========================================================================
  //  Обёртка над ClusterExportItem без дублирования 20 свойств (в отличие от
  //  клиентской версии). Маркер record=cluster и индекс добавляются на уровне
  //  обёртки, сами данные читает внутренний item.
  // ==========================================================================

  sealed class ClusterExportClusterLine
  {
    readonly ClusterExportItem _item;

    public ClusterExportClusterLine(int index, Cluster cluster)
    {
      _item = new ClusterExportItem(index, cluster);
    }

    [JsonProperty("record")]
    public string Record { get { return "cluster"; } }

    [JsonProperty("schema")]
    public string Schema { get { return "qscalp.cluster_export.v1"; } }

    [JsonProperty("source")]
    public string Source { get { return "QScalp.WPF"; } }

    [JsonProperty("index")]
    public int Index { get { return _item.Index; } }

    [JsonProperty("start_time")]
    public string StartTime { get { return _item.StartTime; } }

    [JsonProperty("open_price_int")]
    public int OpenPrice { get { return _item.OpenPrice; } }

    [JsonProperty("open_price")]
    public double RawOpenPrice { get { return _item.RawOpenPrice; } }

    [JsonProperty("open_price_text")]
    public string OpenPriceText { get { return _item.OpenPriceText; } }

    [JsonProperty("close_price_int")]
    public int ClosePrice { get { return _item.ClosePrice; } }

    [JsonProperty("close_price")]
    public double RawClosePrice { get { return _item.RawClosePrice; } }

    [JsonProperty("close_price_text")]
    public string ClosePriceText { get { return _item.ClosePriceText; } }

    [JsonProperty("min_price_int")]
    public int MinPrice { get { return _item.MinPrice; } }

    [JsonProperty("min_price")]
    public double RawMinPrice { get { return _item.RawMinPrice; } }

    [JsonProperty("min_price_text")]
    public string MinPriceText { get { return _item.MinPriceText; } }

    [JsonProperty("max_price_int")]
    public int MaxPrice { get { return _item.MaxPrice; } }

    [JsonProperty("max_price")]
    public double RawMaxPrice { get { return _item.RawMaxPrice; } }

    [JsonProperty("max_price_text")]
    public string MaxPriceText { get { return _item.MaxPriceText; } }

    [JsonProperty("total_volume")]
    public int TotalVolume { get { return _item.TotalVolume; } }

    [JsonProperty("ticks")]
    public int Ticks { get { return _item.Ticks; } }

    [JsonProperty("delta")]
    public int Delta { get { return _item.Delta; } }

    [JsonProperty("bid_volume")]
    public int BidVolume { get { return _item.BidVolume; } }

    [JsonProperty("ask_volume")]
    public int AskVolume { get { return _item.AskVolume; } }

    [JsonProperty("inside_spread_volume")]
    public int InsideSpreadVolume { get { return _item.InsideSpreadVolume; } }

    [JsonProperty("price_levels")]
    public IList<ClusterPriceLevel> PriceLevels { get { return _item.PriceLevels; } }
  }

  // ==========================================================================

  sealed class ClusterExportDocument
  {
    public ClusterExportDocument(DateTime from, DateTime to)
    {
      Period = new ClusterExportPeriod(from, to);
      Instrument = new ClusterExportInstrument();
      Settings = new ClusterExportSettings();
      VolumeSemantics = new ClusterExportVolumeSemantics();
      Clusters = new List<ClusterExportItem>();
    }

    [JsonProperty("schema")]
    public string Schema { get { return "qscalp.cluster_export.v1"; } }

    [JsonProperty("created_at")]
    public string CreatedAt { get { return FormatTime(DateTime.Now); } }

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
    [JsonProperty("sec_code")]
    public string SecCode { get { return cfg.u.SecCode; } }

    [JsonProperty("class_code")]
    public string ClassCode { get { return cfg.u.ClassCode; } }
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
    public string FromText { get { return ClusterExportDocument.FormatTime(From); } }

    [JsonProperty("to")]
    public string ToText { get { return ClusterExportDocument.FormatTime(To); } }
  }

  // ==========================================================================

  sealed class ClusterExportSettings
  {
    [JsonProperty("price_ratio")]
    public int PriceRatio { get { return cfg.u.PriceRatio; } }

    [JsonProperty("price_step_int")]
    public int PriceStep { get { return cfg.u.PriceStep; } }

    [JsonProperty("price_step")]
    public double RawPriceStep { get { return Price.GetRaw(cfg.u.PriceStep); } }

    [JsonProperty("cluster_base")]
    public string ClusterBase { get { return cfg.u.ClusterBase.ToString(); } }

    [JsonProperty("cluster_size")]
    public int ClusterSize { get { return cfg.u.ClusterSize; } }
  }

  // ==========================================================================

  sealed class ClusterExportVolumeSemantics
  {
    [JsonProperty("bid")]
    public string Bid { get { return "seller initiated volume, printed at bid"; } }

    [JsonProperty("ask")]
    public string Ask { get { return "buyer initiated volume, printed at ask"; } }

    [JsonProperty("inside_spread")]
    public string InsideSpread { get { return "neutral volume printed inside spread or with unknown side"; } }

    [JsonProperty("delta")]
    public string Delta { get { return "ask - bid; inside_spread is not included"; } }
  }

  // ==========================================================================

  sealed class ClusterExportItem
  {
    readonly Cluster cluster;
    readonly IList<ClusterPriceLevel> levels;
    readonly int bidVolume;
    readonly int askVolume;
    readonly int insideSpreadVolume;

    public ClusterExportItem(int index, Cluster cluster)
    {
      this.cluster = cluster;
      Index = index;

      levels = cluster.GetPriceLevels();

      for(int i = 0; i < levels.Count; i++)
      {
        bidVolume += levels[i].BidVolume;
        askVolume += levels[i].AskVolume;
        insideSpreadVolume += levels[i].InsideSpreadVolume;
      }
    }

    [JsonProperty("index")]
    public int Index { get; private set; }

    [JsonProperty("start_time")]
    public string StartTime { get { return ClusterExportDocument.FormatTime(cluster.DateTime); } }

    [JsonProperty("open_price_int")]
    public int OpenPrice { get { return cluster.OpenPrice; } }

    [JsonProperty("open_price")]
    public double RawOpenPrice { get { return Price.GetRaw(cluster.OpenPrice); } }

    [JsonProperty("open_price_text")]
    public string OpenPriceText { get { return Price.GetString(cluster.OpenPrice); } }

    [JsonProperty("close_price_int")]
    public int ClosePrice { get { return cluster.ClosePrice; } }

    [JsonProperty("close_price")]
    public double RawClosePrice { get { return Price.GetRaw(cluster.ClosePrice); } }

    [JsonProperty("close_price_text")]
    public string ClosePriceText { get { return Price.GetString(cluster.ClosePrice); } }

    [JsonProperty("min_price_int")]
    public int MinPrice { get { return cluster.MinPrice; } }

    [JsonProperty("min_price")]
    public double RawMinPrice { get { return Price.GetRaw(cluster.MinPrice); } }

    [JsonProperty("min_price_text")]
    public string MinPriceText { get { return Price.GetString(cluster.MinPrice); } }

    [JsonProperty("max_price_int")]
    public int MaxPrice { get { return cluster.MaxPrice; } }

    [JsonProperty("max_price")]
    public double RawMaxPrice { get { return Price.GetRaw(cluster.MaxPrice); } }

    [JsonProperty("max_price_text")]
    public string MaxPriceText { get { return Price.GetString(cluster.MaxPrice); } }

    [JsonProperty("total_volume")]
    public int TotalVolume { get { return cluster.Volume; } }

    [JsonProperty("ticks")]
    public int Ticks { get { return cluster.Ticks; } }

    [JsonProperty("delta")]
    public int Delta { get { return cluster.Delta; } }

    [JsonProperty("bid_volume")]
    public int BidVolume { get { return bidVolume; } }

    [JsonProperty("ask_volume")]
    public int AskVolume { get { return askVolume; } }

    [JsonProperty("inside_spread_volume")]
    public int InsideSpreadVolume { get { return insideSpreadVolume; } }

    [JsonProperty("price_levels")]
    public IList<ClusterPriceLevel> PriceLevels { get { return levels; } }
  }

  // ==========================================================================
}
