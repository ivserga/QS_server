using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using QScalp.View.ClustersSpace;
using QScalp.View.ClustersSpace.Analytics;
using QScalp.View.ClustersSpace.Analytics.Detectors;

namespace QScalp.Tools.ClusterReplay
{
  static class TrendContinuationDiag
  {
    public static void DumpBar(IList<Cluster> clusters, int priceStep, string timeSuffix)
    {
      var det = new TrendContinuationDetector();
      var history = new ClusterHistory(128);
      ClusterStats target = null;

      for (int i = 0; i < clusters.Count; i++)
      {
        var stats = ClusterStats.Compute(clusters[i], priceStep);
        if (stats == null) continue;
        history.Add(stats);
        if (clusters[i].DateTime.ToString("HH:mm:ss") == timeSuffix ||
            clusters[i].DateTime.ToString("yyyy-MM-dd HH:mm:ss").EndsWith(timeSuffix))
          target = stats;
      }

      if (target == null)
      {
        Console.WriteLine("Bar not found: " + timeSuffix);
        return;
      }

      bool up = false;
      double slope = WindowMath.SlopePctClose(history, 0, det.Lookback);
      double closeExt = CloseExtremeShare(target, up);
      double counterTail = target.ShareBeyondCom(true);
      double avgVol = history.AverageVolumeBefore(det.Lookback);

      Console.WriteLine("--- Bar " + target.Source.DateTime.ToString("yyyy-MM-dd HH:mm:ss") + " ---");
      Console.WriteLine(string.Format("  OHLC int: O={0} C={1} H={2} L={3} vol={4} range={5}",
        target.OpenPrice, target.ClosePrice, target.MaxPrice, target.MinPrice, target.Volume, target.Range));
      Console.WriteLine(string.Format("  PosCom={0:F2} Top3={1:F2} Shape={2} Skew={3:F2} Com={4}",
        target.PosCom, target.Top3Share, target.Shape, target.Skewness, target.ComPrice / 100.0));
      Console.WriteLine(string.Format("  close@low={0:F2} counterTail(above COM)={1:F2} qBottom={2:F2} inside={3:P0}",
        closeExt, counterTail, target.ShareInQuarter(false), target.InsideSpreadShare));
      Console.WriteLine(string.Format("  slope8={0:F5} netMove8={1:F5} avgVol={2:F0} vol/avg={3:F2}",
        slope, NetMovePct(history, det.Lookback),
        avgVol, avgVol > 0 ? target.Volume / avgVol : 0));
      Console.WriteLine(string.Format("  bodyRatio={0:F2} density={1:F0} (vol/range)",
        target.Range > 0 ? Math.Abs(target.ClosePrice - target.OpenPrice) / (double)target.Range : 0,
        target.Range > 0 ? (double)target.Volume / target.Range : 0));
    }

    static double NetMovePct(ClusterHistory history, int lookback)
    {
      var a = history.Last(lookback - 1);
      var b = history.Last(0);
      if (a == null || b == null) return 0;
      double mean = (a.ClosePrice + b.ClosePrice) / 2.0;
      return mean > 0 ? (b.ClosePrice - a.ClosePrice) / mean : 0;
    }

    public static void Run(IList<Cluster> clusters, int priceStep)
    {
      var det = new TrendContinuationDetector();
      var history = new ClusterHistory(128);
      var failCounts = new Dictionary<string, int>();
      var nearMiss = new List<string>();

      void Bump(string reason)
      {
        if (!failCounts.ContainsKey(reason))
          failCounts[reason] = 0;
        failCounts[reason]++;
      }

      for (int i = 0; i < clusters.Count; i++)
      {
        var stats = ClusterStats.Compute(clusters[i], priceStep);
        if (stats == null)
          continue;

        history.Add(stats);
        if (history.Count < det.Lookback)
          continue;

        DiagnoseBar(det, history, false, Bump, nearMiss);
      }

      Console.WriteLine("=== TrendContinuation DOWN — why no signal (NVDA replay) ===");
      Console.WriteLine("Bars evaluated (with full lookback): " + (clusters.Count - det.Lookback + 1));
      Console.WriteLine();
      Console.WriteLine("First failing check per bar (histogram):");
      foreach (var kv in failCounts.OrderByDescending(x => x.Value))
        Console.WriteLine($"  {kv.Value,4}  {kv.Key}");

      Console.WriteLine();
      Console.WriteLine("Sample bars that passed ALL checks except strength (if any):");
      foreach (var line in nearMiss)
        Console.WriteLine("  " + line);
      Console.WriteLine("  Total PASS (down, all checks): " + nearMiss.Count(x => x.StartsWith("PASS")));

      Console.WriteLine();
      Console.WriteLine("Slope close on selloff window (17:20-17:40), threshold=" + det.MinTrendSlopePct);
      for (int i = 0; i < clusters.Count; i++)
      {
        var t = clusters[i].DateTime;
        if (t.Hour != 17 || t.Minute < 20 || t.Minute > 40)
          continue;

        var h = new ClusterHistory(128);
        for (int j = 0; j <= i; j++)
        {
          var st = ClusterStats.Compute(clusters[j], priceStep);
          if (st != null) h.Add(st);
        }
        if (h.Count < det.Lookback) continue;

        double sl = WindowMath.SlopePctClose(h, 0, det.Lookback);
        var last = h.Last(0);
        Console.WriteLine($"  {t:HH:mm:ss} close={last.ClosePrice / 100.0:F2} slope={sl:F5} aligned={AlignedBodyShare(h, det.Lookback, false):F2}");
      }

      Console.WriteLine();
      Console.WriteLine("Note: DeferToBreakout blocks when close <= prior window low - 2 ticks.");
      Console.WriteLine("      On cascading selloffs Breakout fires; TrendContinuation intentionally skips those bars.");
    }

    static void DiagnoseBar(
      TrendContinuationDetector det,
      ClusterHistory history,
      bool up,
      Action<string> bump,
      List<string> nearMiss)
    {
      var last = history.Last(0);
      if (last == null || last.Range <= 0 || last.Volume == 0)
      {
        bump("empty_bar");
        return;
      }

      double slopeClose = WindowMath.SlopePctClose(history, 0, det.Lookback);
      if (!HasTrendMomentum(det, history, up, slopeClose))
      {
        bump("trend_momentum_weak");
        return;
      }

      double aligned = AlignedBodyShare(history, det.Lookback, up);
      if (aligned < det.MinAlignedBodyShare)
      {
        bump($"aligned_body={aligned:F2}<{det.MinAlignedBodyShare:F2}");
        return;
      }

      if (!up && LooksLikeAccumulation(history, det.Lookback))
      {
        bump("looks_like_accumulation");
        return;
      }

      if (det.DeferToBreakoutTicks > 0 && IsFreshBreakout(det, history, last, up))
      {
        bump("defer_to_breakout");
        return;
      }

      double posThreshold = 1.0 - det.PosComFavorable;
      if (last.PosCom > posThreshold)
      {
        bump($"pos_com={last.PosCom:F2}>{posThreshold:F2}");
        return;
      }

      double closeExt = CloseExtremeShare(last, up);
      if (closeExt < det.MinCloseAtExtreme)
      {
        bump($"close_at_extreme={closeExt:F2}<{det.MinCloseAtExtreme:F2}");
        return;
      }

      double counterTail = last.ShareBeyondCom(true);
      if (counterTail > det.MaxCounterTailShare)
      {
        bump($"counter_tail={counterTail:F2}>{det.MaxCounterTailShare:F2}");
        return;
      }

      double qShare = last.ShareInQuarter(false);
      if (qShare < det.MinQuarterShare)
      {
        bump($"quarter_share={qShare:F2}<{det.MinQuarterShare:F2}");
        return;
      }

      if (last.Top3Share > det.MaxTop3Share)
      {
        bump($"top3={last.Top3Share:F2}>{det.MaxTop3Share:F2}");
        return;
      }

      double avgVol = history.AverageVolumeBefore(det.Lookback);
      if (avgVol > 0 && last.Volume < avgVol * det.MinVolumeRatio)
      {
        bump($"volume_ratio={(last.Volume / avgVol):F2}<{det.MinVolumeRatio:F2}");
        return;
      }

      if (last.Shape == ProfileShape.Thin)
      {
        bump("shape_thin");
        return;
      }

      double avgPosCom = AveragePosCom(history, det.Lookback);
      if (!up && avgPosCom > 0.55)
      {
        bump($"avg_pos_com={avgPosCom:F2}>0.55");
        return;
      }

      double strength = EstimateStrength(det, last, up, slopeClose, closeExt, counterTail, avgVol);
      if (strength < det.MinStrength)
      {
        bump($"strength={strength:F2}<{det.MinStrength:F2}");
        nearMiss.Add($"{last.Source.DateTime:HH:mm:ss} str={strength:F2} slope={slopeClose:F4} posCom={last.PosCom:F2} closeExt={closeExt:F2} tail={counterTail:F2}");
        return;
      }

      nearMiss.Add($"PASS {last.Source.DateTime:HH:mm:ss} str={strength:F2}");
    }

    static double AlignedBodyShare(ClusterHistory history, int count, bool up)
    {
      int aligned = 0, total = 0;
      for (int i = 0; i < count; i++)
      {
        var s = history.Last(i);
        if (s == null) break;
        total++;
        if (!up && s.ClosePrice < s.OpenPrice) aligned++;
        else if (up && s.ClosePrice > s.OpenPrice) aligned++;
      }
      return total > 0 ? (double)aligned / total : 0;
    }

    static bool LooksLikeAccumulation(ClusterHistory history, int count)
    {
      if (!WindowMath.TwoHalvesLows(history, count, out int lo1, out int lo2))
        return false;
      return lo2 > lo1;
    }

    static bool IsFreshBreakout(TrendContinuationDetector det, ClusterHistory history, ClusterStats last, bool up)
    {
      int prior = WindowMath.LowestLowExcludingLast(history, det.Lookback, 1);
      if (prior == int.MaxValue) return false;
      return last.ClosePrice <= prior - det.DeferToBreakoutTicks;
    }

    static double CloseExtremeShare(ClusterStats s, bool up)
    {
      if (s.Range <= 0) return 0;
      if (up)
        return 1.0 - (double)(s.MaxPrice - s.ClosePrice) / s.Range;
      return 1.0 - (double)(s.ClosePrice - s.MinPrice) / s.Range;
    }

    static bool HasTrendMomentum(TrendContinuationDetector det, ClusterHistory history, bool up, double slopeClose)
    {
      if (up)
      {
        if (slopeClose >= det.MinTrendSlopePct) return true;
      }
      else if (slopeClose <= -det.MinTrendSlopePct)
        return true;

      var oldest = history.Last(det.Lookback - 1);
      var last = history.Last(0);
      if (oldest == null || last == null) return false;
      double mean = (oldest.ClosePrice + last.ClosePrice) / 2.0;
      if (mean <= 0) return false;
      double net = (last.ClosePrice - oldest.ClosePrice) / mean;
      return up ? net >= det.MinNetMovePct : net <= -det.MinNetMovePct;
    }

    static double AveragePosCom(ClusterHistory history, int count)
    {
      double sum = 0; int n = 0;
      for (int i = 0; i < count; i++)
      {
        var s = history.Last(i);
        if (s == null) break;
        sum += s.PosCom;
        n++;
      }
      return n > 0 ? sum / n : 0.5;
    }

    static double EstimateStrength(TrendContinuationDetector det, ClusterStats last, bool up,
      double slopeClose, double closeExtreme, double counterTail, double avgVol)
    {
      double slopeScore = Math.Min(1.0, Math.Abs(slopeClose) / Math.Max(1e-6, det.MinTrendSlopePct * 4));
      double closeScore = Math.Min(1.0, Math.Max(0, (closeExtreme - 0.5) / 0.5));
      double posScore = Math.Min(1.0, Math.Max(0, (0.5 - last.PosCom) / 0.5));
      double tailScore = Math.Max(0, 1.0 - counterTail / Math.Max(1e-6, det.MaxCounterTailShare));
      double volScore = avgVol > 0 ? Math.Min(1.0, (double)last.Volume / avgVol) : 0.5;
      double quarterScore = Math.Min(1.0, last.ShareInQuarter(up) / Math.Max(1e-6, det.MinQuarterShare));
      double strength = 0.22 * slopeScore + 0.22 * closeScore + 0.20 * posScore
        + 0.16 * tailScore + 0.10 * volScore + 0.10 * quarterScore;
      if (!up && last.Skewness > 0) strength -= 0.04;
      if (last.Shape == ProfileShape.Trending) strength += 0.06;
      return Math.Min(1.0, Math.Max(0, strength));
    }
  }
}
