using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using QScalp.View.ClustersSpace;
using QScalp.View.ClustersSpace.Analytics;
using QScalp.View.ClustersSpace.Analytics.Detectors;

namespace QScalp.Tools.ClusterReplay
{
  static class FalseBreakDiag
  {
    sealed class FbHit
    {
      public DateTime Time;
      public double Strength;
      public string Message;
    }

    public static void RunSweep(IList<Cluster> clusters, int priceStep)
    {
      Console.WriteLine("=== FalseBreakReclaim sweep (NVDA replay) ===");
      Console.WriteLine("Bars: " + clusters.Count);
      Console.WriteLine();

      RunUpSweep(clusters, priceStep);
      RunDownSweep(clusters, priceStep);
    }

    static void RunUpSweep(IList<Cluster> clusters, int priceStep)
    {
      Console.WriteLine("========== FalseBreakReclaimUp (spring long) ==========");
      RunPresetLoop(
        clusters,
        priceStep,
        "default (UserSettings)",
        d => { },
        "mild",
        d =>
        {
          d.MinNearTouches = 1;
          d.FalseBreakVolumeRatio = 0.85;
          d.ReclaimVolumeRatio = 0.80;
          d.ReclaimMinBodyRatio = 0.30;
          d.ReclaimPosComMin = 0.50;
          d.ReclaimMaxTop3Share = 0.60;
          d.MinStrength = 0.42;
          d.MaxBarsAfterTrap = 6;
        },
        "relaxed",
        d =>
        {
          d.MinNearTouches = 1;
          d.NearLevelTicks = 6;
          d.FalseBreakMinTicks = 1;
          d.FalseBreakVolumeRatio = 0.70;
          d.ReclaimVolumeRatio = 0.70;
          d.ReclaimMinBodyRatio = 0.25;
          d.ReclaimPosComMin = 0.45;
          d.ReclaimMaxTop3Share = 0.65;
          d.MinStrength = 0.35;
          d.MaxBarsAfterTrap = 8;
          d.ReclaimBreakTicks = 0;
          d.CooldownBars = 2;
        },
        "very relaxed",
        d =>
        {
          d.MinNearTouches = 1;
          d.NearLevelTicks = 8;
          d.FalseBreakMinTicks = 1;
          d.FalseBreakVolumeRatio = 0.50;
          d.ReclaimVolumeRatio = 0.55;
          d.ReclaimMinBodyRatio = 0.20;
          d.ReclaimPosComMin = 0.40;
          d.ReclaimMaxTop3Share = 0.75;
          d.MinStrength = 0.28;
          d.MaxBarsAfterTrap = 10;
          d.ReclaimBreakTicks = 0;
          d.CooldownBars = 1;
          d.LookbackBars = 16;
        });

      PrintWatchUp(clusters, priceStep);
      Console.WriteLine();
    }

    static void RunDownSweep(IList<Cluster> clusters, int priceStep)
    {
      Console.WriteLine("========== FalseBreakReclaimDown (upthrust short) ==========");
      RunPresetLoopDown(
        clusters,
        priceStep,
        "default (UserSettings)",
        d => { },
        "mild",
        d =>
        {
          d.MinNearTouches = 1;
          d.FalseBreakVolumeRatio = 0.85;
          d.ReclaimVolumeRatio = 0.80;
          d.ReclaimMinBodyRatio = 0.30;
          d.ReclaimPosComMax = 0.50;
          d.ReclaimMaxTop3Share = 0.60;
          d.MinStrength = 0.42;
          d.MaxBarsAfterTrap = 6;
        },
        "relaxed",
        d =>
        {
          d.MinNearTouches = 1;
          d.NearLevelTicks = 6;
          d.FalseBreakMinTicks = 1;
          d.FalseBreakVolumeRatio = 0.70;
          d.ReclaimVolumeRatio = 0.70;
          d.ReclaimMinBodyRatio = 0.25;
          d.ReclaimPosComMax = 0.55;
          d.ReclaimMaxTop3Share = 0.65;
          d.MinStrength = 0.35;
          d.MaxBarsAfterTrap = 8;
          d.ReclaimBreakTicks = 0;
          d.CooldownBars = 2;
        },
        "very relaxed",
        d =>
        {
          d.MinNearTouches = 1;
          d.NearLevelTicks = 8;
          d.FalseBreakMinTicks = 1;
          d.FalseBreakVolumeRatio = 0.50;
          d.ReclaimVolumeRatio = 0.55;
          d.ReclaimMinBodyRatio = 0.20;
          d.ReclaimPosComMax = 0.60;
          d.ReclaimMaxTop3Share = 0.75;
          d.MinStrength = 0.28;
          d.MaxBarsAfterTrap = 10;
          d.ReclaimBreakTicks = 0;
          d.CooldownBars = 1;
          d.LookbackBars = 16;
        });

      PrintWatchDown(clusters, priceStep);
      Console.WriteLine();
    }

    static void RunPresetLoop(
      IList<Cluster> clusters,
      int priceStep,
      string n0, Action<FalseBreakReclaimDetector> a0,
      string n1, Action<FalseBreakReclaimDetector> a1,
      string n2, Action<FalseBreakReclaimDetector> a2,
      string n3, Action<FalseBreakReclaimDetector> a3)
    {
      PrintPreset(n0, ReplayUp(clusters, priceStep, a0));
      PrintPreset(n1, ReplayUp(clusters, priceStep, a1));
      PrintPreset(n2, ReplayUp(clusters, priceStep, a2));
      PrintPreset(n3, ReplayUp(clusters, priceStep, a3));
    }

    static void RunPresetLoopDown(
      IList<Cluster> clusters,
      int priceStep,
      string n0, Action<FalseBreakReclaimDownDetector> a0,
      string n1, Action<FalseBreakReclaimDownDetector> a1,
      string n2, Action<FalseBreakReclaimDownDetector> a2,
      string n3, Action<FalseBreakReclaimDownDetector> a3)
    {
      PrintPreset(n0, ReplayDown(clusters, priceStep, a0));
      PrintPreset(n1, ReplayDown(clusters, priceStep, a1));
      PrintPreset(n2, ReplayDown(clusters, priceStep, a2));
      PrintPreset(n3, ReplayDown(clusters, priceStep, a3));
    }

    static void PrintPreset(string name, List<FbHit> hits)
    {
      Console.WriteLine("--- " + name + " ---");
      if(hits.Count == 0)
      {
        Console.WriteLine("(нет срабатываний)");
        Console.WriteLine();
        return;
      }

      foreach(var h in hits.OrderBy(x => x.Time))
      {
        Console.WriteLine("  " + h.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
          + "  strength=" + h.Strength.ToString("F2", CultureInfo.InvariantCulture));
        Console.WriteLine("    " + h.Message);
      }
      Console.WriteLine();
    }

    static List<FbHit> ReplayUp(IList<Cluster> clusters, int priceStep, Action<FalseBreakReclaimDetector> apply)
    {
      var det = new FalseBreakReclaimDetector();
      apply(det);
      return ReplayCore(clusters, priceStep, det.Evaluate);
    }

    static List<FbHit> ReplayDown(IList<Cluster> clusters, int priceStep, Action<FalseBreakReclaimDownDetector> apply)
    {
      var det = new FalseBreakReclaimDownDetector();
      apply(det);
      return ReplayCore(clusters, priceStep, det.Evaluate);
    }

    static List<FbHit> ReplayCore(IList<Cluster> clusters, int priceStep, Func<ClusterHistory, Signal> eval)
    {
      var history = new ClusterHistory(128);
      var hits = new List<FbHit>();

      for(int i = 0; i < clusters.Count; i++)
      {
        var stats = ClusterStats.Compute(clusters[i], priceStep);
        if(stats == null) continue;

        history.Add(stats);
        var sig = eval(history);
        if(sig == null) continue;

        hits.Add(new FbHit { Time = sig.Time, Strength = sig.Strength, Message = sig.Message });
      }

      return hits;
    }

    static void PrintWatchUp(IList<Cluster> clusters, int priceStep)
    {
      PrintWatch(clusters, priceStep, "Up relaxed", history =>
      {
        var det = new FalseBreakReclaimDetector();
        det.MinNearTouches = 1;
        det.NearLevelTicks = 6;
        det.FalseBreakMinTicks = 1;
        det.FalseBreakVolumeRatio = 0.70;
        det.ReclaimVolumeRatio = 0.70;
        det.ReclaimMinBodyRatio = 0.25;
        det.ReclaimPosComMin = 0.45;
        det.ReclaimMaxTop3Share = 0.65;
        det.MinStrength = 0.35;
        det.MaxBarsAfterTrap = 8;
        det.ReclaimBreakTicks = 0;
        det.CooldownBars = 2;
        return det.Evaluate(history);
      });
    }

    static void PrintWatchDown(IList<Cluster> clusters, int priceStep)
    {
      PrintWatch(clusters, priceStep, "Down relaxed", history =>
      {
        var det = new FalseBreakReclaimDownDetector();
        det.MinNearTouches = 1;
        det.NearLevelTicks = 6;
        det.FalseBreakMinTicks = 1;
        det.FalseBreakVolumeRatio = 0.70;
        det.ReclaimVolumeRatio = 0.70;
        det.ReclaimMinBodyRatio = 0.25;
        det.ReclaimPosComMax = 0.55;
        det.ReclaimMaxTop3Share = 0.65;
        det.MinStrength = 0.35;
        det.MaxBarsAfterTrap = 8;
        det.ReclaimBreakTicks = 0;
        det.CooldownBars = 2;
        return det.Evaluate(history);
      });
    }

    static void PrintWatch(IList<Cluster> clusters, int priceStep, string label, Func<ClusterHistory, Signal> eval)
    {
      string[] watch = { "17:15:00", "17:15:30", "17:20:00", "17:23:00", "17:25:00", "17:32:00", "17:35:30", "17:57:30" };
      var watchSet = new HashSet<string>(watch);

      Console.WriteLine("--- watch-бары [" + label + "] ---");
      for(int i = 0; i < clusters.Count; i++)
      {
        string hhmm = clusters[i].DateTime.ToString("HH:mm:ss");
        if(!watchSet.Contains(hhmm)) continue;

        var history = new ClusterHistory(128);
        for(int j = 0; j <= i; j++)
        {
          var st = ClusterStats.Compute(clusters[j], priceStep);
          if(st != null) history.Add(st);
        }

        var sig = eval(history);
        if(sig != null)
          Console.WriteLine("  " + clusters[i].DateTime.ToString("yyyy-MM-dd HH:mm:ss") + "  FIRE  " + sig.Message);
        else
          Console.WriteLine("  " + clusters[i].DateTime.ToString("yyyy-MM-dd HH:mm:ss") + "  —");
      }
    }
  }
}
