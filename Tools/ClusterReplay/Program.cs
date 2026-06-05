using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using QScalp.View.ClustersSpace;
using QScalp.View.ClustersSpace.Analytics;
using QScalp.View.ClustersSpace.Analytics.Detectors;

namespace QScalp.Tools.ClusterReplay
{
  static class Program
  {
    sealed class ExportDoc
    {
      [JsonProperty("settings")] public ExportSettings Settings { get; set; }
      [JsonProperty("clusters")] public List<ExportCluster> Clusters { get; set; }
    }

    sealed class ExportSettings
    {
      [JsonProperty("price_ratio")] public int PriceRatio { get; set; }
      [JsonProperty("price_step_int")] public int PriceStepInt { get; set; }
      [JsonProperty("cluster_size")] public int ClusterSize { get; set; }
    }

    internal sealed class ExportCluster
    {
      [JsonProperty("index")] public int Index { get; set; }
      [JsonProperty("start_time")] public string StartTime { get; set; }
      [JsonProperty("open_price_int")] public int OpenPriceInt { get; set; }
      [JsonProperty("close_price_int")] public int ClosePriceInt { get; set; }
      [JsonProperty("bid_volume")] public int BidVolume { get; set; }
      [JsonProperty("ask_volume")] public int AskVolume { get; set; }
      [JsonProperty("total_volume")] public int TotalVolume { get; set; }
      [JsonProperty("price_levels")] public List<ExportLevel> PriceLevels { get; set; }
    }

    internal sealed class ExportLevel
    {
      [JsonProperty("price_int")] public int PriceInt { get; set; }
      [JsonProperty("bid")] public int Bid { get; set; }
      [JsonProperty("ask")] public int Ask { get; set; }
      [JsonProperty("inside_spread")] public int InsideSpread { get; set; }
    }

    internal sealed class Hit
    {
      public int BarIndex;
      public DateTime Time;
      public string Source;
      public SignalKind Kind;
      public double Strength;
      public string Message;
    }

    static int Main(string[] args)
    {
      bool falseBreakSweep = args.Length > 0 && args[0] == "--falsebreak-sweep";
      bool tcDiag = args.Length > 0 && args[0] == "--tc-diag";
      bool barDump = args.Length > 1 && args[0] == "--bar";
      bool flushBar = args.Length > 1 && args[0] == "--flush-bar";
      int pathArg = falseBreakSweep ? 1 : (tcDiag ? 1 : (barDump || flushBar ? 2 : 0));

      string path = args.Length > pathArg
        ? args[pathArg]
        : @"D:\VsProjects\QScalpWPF_Api_Feed\Datasets\clusters.NVDA..20260603.020017.json";

      if (!File.Exists(path))
      {
        Console.Error.WriteLine("File not found: " + path);
        return 1;
      }

      var doc = JsonConvert.DeserializeObject<ExportDoc>(File.ReadAllText(path));
      if (doc?.Clusters == null || doc.Clusters.Count == 0)
      {
        Console.Error.WriteLine("No clusters in file.");
        return 1;
      }

      ReplayConfig.PriceRatio = doc.Settings?.PriceRatio > 0 ? doc.Settings.PriceRatio : 100;
      ReplayConfig.PriceStep = doc.Settings?.PriceStepInt > 0 ? doc.Settings.PriceStepInt : 1;
      int clusterSize = doc.Settings?.ClusterSize > 0 ? doc.Settings.ClusterSize : 30;

      var clusters = new List<Cluster>(doc.Clusters.Count);
      foreach (var item in doc.Clusters)
        clusters.Add(BuildCluster(item));

      if (falseBreakSweep)
      {
        FalseBreakDiag.RunSweep(clusters, ReplayConfig.PriceStep);
        return 0;
      }

      if (tcDiag)
      {
        TrendContinuationDiag.Run(clusters, ReplayConfig.PriceStep);
        return 0;
      }

      if (barDump)
      {
        foreach (var t in args[1].Split(','))
          TrendContinuationDiag.DumpBar(clusters, ReplayConfig.PriceStep, t.Trim());
        return 0;
      }

      if (flushBar)
      {
        foreach (var t in args[1].Split(','))
          FlushDiag.DumpBar(clusters, ReplayConfig.PriceStep, t.Trim());
        return 0;
      }

      var bus = BuildViewSignalBus();
      var hits = new List<Hit>();
      var legacyTf = new List<string>();

      bus.AddSink(new CollectSink(hits));

      int historyCap = Math.Max(128, clusters.Count);
      var analyzerHistory = new List<Cluster>();

      for (int i = 0; i < clusters.Count; i++)
      {
        var c = clusters[i];
        bus.OnClusterClosed(c, ReplayConfig.PriceStep);

        analyzerHistory.Add(c);
        while (analyzerHistory.Count > historyCap)
          analyzerHistory.RemoveAt(0);

        if (analyzerHistory.Count >= 3)
        {
          foreach (string msg in ClusterAnalyzer.AnalyzeTimeframes(analyzerHistory, clusterSize))
          {
            if (!string.IsNullOrEmpty(msg))
              legacyTf.Add($"[{c.DateTime:yyyy-MM-dd HH:mm:ss}] {msg}");
          }
        }
      }

      Console.WriteLine("=== Cluster replay ===");
      Console.WriteLine("File: " + path);
      Console.WriteLine("Bars: " + clusters.Count + ", cluster_size=" + clusterSize + "s, price_step=" + ReplayConfig.PriceStep);
      Console.WriteLine();

      PrintGroup("SignalBus (View/Clusters/Analytics/Detectors)", hits);
      PrintLegacy(legacyTf);

      return 0;
    }

    static Cluster BuildCluster(ExportCluster item)
    {
      var dt = DateTime.Parse(item.StartTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
      var c = new Cluster(dt);

      var levels = item.PriceLevels ?? new List<ExportLevel>();
      var byPrice = levels.ToDictionary(x => x.PriceInt);

      var visit = BuildVisitOrder(item.OpenPriceInt, item.ClosePriceInt, byPrice.Keys);
      foreach (int price in visit)
      {
        if (!byPrice.TryGetValue(price, out var lv))
          continue;
        c.AddLevel(price, lv.Bid, lv.Ask, lv.InsideSpread);
      }

      return c;
    }

    static List<int> BuildVisitOrder(int open, int close, IEnumerable<int> prices)
    {
      var sorted = prices.OrderByDescending(p => p).ToList();
      var order = new List<int>();
      if (!sorted.Contains(open))
        sorted.Add(open);
      if (!sorted.Contains(close))
        sorted.Add(close);
      sorted = sorted.Distinct().OrderByDescending(p => p).ToList();

      int openIdx = sorted.IndexOf(open);
      int closeIdx = sorted.IndexOf(close);
      if (openIdx < 0) openIdx = 0;
      if (closeIdx < 0) closeIdx = sorted.Count - 1;

      if (openIdx <= closeIdx)
      {
        for (int i = openIdx; i <= closeIdx; i++)
          order.Add(sorted[i]);
      }
      else
      {
        for (int i = openIdx; i >= closeIdx; i--)
          order.Add(sorted[i]);
      }

      return order;
    }

    internal static SignalBus BuildViewSignalBus()
    {
      var bus = new SignalBus(128);

      bus.AddDetector(new AbsorptionDetector());
      bus.AddDetector(new ClimaxDetector());
      bus.AddDetector(new BalanceAfterClimaxDetector());
      bus.AddDetector(new VReversalDetector());
      bus.AddDetector(new HvnRejectDetector());
      bus.AddDetector(new DistributionDetector());
      bus.AddDetector(new AccumulationDetector());
      bus.AddDetector(new BreakoutDetector());
      bus.AddDetector(new DoubleTopDetector());
      bus.AddDetector(new DoubleBottomDetector());
      bus.AddDetector(new FalseBreakReclaimDetector());
      bus.AddDetector(new FalseBreakReclaimDownDetector());
      bus.AddDetector(new TrendContinuationDetector());
      bus.AddDetector(new FlushDetector());
      // OrphanClose отключён в UserSettings по умолчанию — как в ClustersElement (не в bus).

      return bus;
    }

    static void PrintGroup(string title, List<Hit> hits)
    {
      Console.WriteLine("--- " + title + " ---");
      if (hits.Count == 0)
      {
        Console.WriteLine("(нет срабатываний)");
        Console.WriteLine();
        return;
      }

      foreach (var g in hits.GroupBy(h => h.Source).OrderBy(g => g.Key))
      {
        Console.WriteLine("[" + g.Key + "] " + g.Count() + " hit(s):");
        foreach (var h in g.OrderBy(x => x.Time))
        {
          Console.WriteLine("  " + h.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            + "  " + h.Kind + "  strength=" + h.Strength.ToString("F2", CultureInfo.InvariantCulture));
          if (!string.IsNullOrEmpty(h.Message))
            Console.WriteLine("    " + h.Message);
        }
        Console.WriteLine();
      }
    }

    static void PrintLegacy(List<string> messages)
    {
      Console.WriteLine("--- ClusterAnalyzer (legacy, multi-TF) ---");
      if (messages.Count == 0)
      {
        Console.WriteLine("(нет срабатываний)");
        Console.WriteLine();
        return;
      }

      // dedupe consecutive identical messages
      string prev = null;
      foreach (string msg in messages)
      {
        if (msg == prev) continue;
        prev = msg;
        Console.WriteLine(msg);
      }
      Console.WriteLine();
    }

    internal sealed class CollectSink : ISignalSink
    {
      readonly List<Hit> _hits;
      public CollectSink(List<Hit> hits) => _hits = hits;

      public void Emit(Signal s)
      {
        if (s == null || s.Kind == SignalKind.None) return;
        _hits.Add(new Hit
        {
          Time = s.Time,
          Source = s.Source,
          Kind = s.Kind,
          Strength = s.Strength,
          Message = s.Message
        });
      }
    }
  }
}
