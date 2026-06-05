using System;
using System.Collections.Generic;
using System.Globalization;

using QScalp.View.ClustersSpace;
using QScalp.View.ClustersSpace.Analytics;
using QScalp.View.ClustersSpace.Analytics.Detectors;

namespace QScalp.Tools.ClusterReplay
{
  static class FlushDiag
  {
    public static void DumpBar(IList<Cluster> clusters, int priceStep, string timeSuffix)
    {
      var det = new FlushDetector();
      var history = new ClusterHistory(128);
      ClusterStats target = null;
      int targetIdx = -1;

      for (int i = 0; i < clusters.Count; i++)
      {
        var stats = ClusterStats.Compute(clusters[i], priceStep);
        if (stats == null) continue;
        history.Add(stats);
        if (clusters[i].DateTime.ToString("HH:mm:ss") == timeSuffix)
        {
          target = stats;
          targetIdx = i;
        }
      }

      if (target == null)
      {
        Console.WriteLine("Bar not found: " + timeSuffix);
        return;
      }

      history = new ClusterHistory(128);
      for (int j = 0; j <= targetIdx; j++)
      {
        var st = ClusterStats.Compute(clusters[j], priceStep);
        if (st != null) history.Add(st);
      }

      bool up = false;
      string fail = Diagnose(det, history, target, up);
      Console.WriteLine("--- Flush DOWN @ " + target.Source.DateTime.ToString("yyyy-MM-dd HH:mm:ss") + " ---");
      Console.WriteLine("  fail: " + (fail ?? "PASS (checks only, no cooldown)"));
    }

    static string Diagnose(FlushDetector det, ClusterHistory history, ClusterStats last, bool up)
    {
      if (history.Count < det.AverageWindow + 1)
        return "history_short";

      if (last.Volume == 0 || last.Range < det.MinRangeTicks)
        return "range_or_vol";

      int body = last.ClosePrice - last.OpenPrice;
      if (up && body <= 0) return "body";
      if (!up && body >= 0) return "body";

      int tickMove = up ? body : -body;
      if (tickMove < det.MinFlushTicks)
        return "tickMove=" + tickMove;

      double bodyRatio = (double)Math.Abs(body) / last.Range;
      if (bodyRatio < det.MinBodyRatio)
        return "bodyRatio=" + bodyRatio.ToString("F2");

      double closeExt = up
        ? 1.0 - (double)(last.MaxPrice - last.ClosePrice) / last.Range
        : 1.0 - (double)(last.ClosePrice - last.MinPrice) / last.Range;
      if (closeExt < det.MinCloseAtExtreme)
        return "closeExt=" + closeExt.ToString("F2");

      if (!up && last.PosCom > det.MaxPosComDown)
        return "posCom=" + last.PosCom.ToString("F2");

      double counterTail = last.ShareBeyondCom(!up);
      if (counterTail > det.MaxCounterTailShare)
        return "counterTail=" + counterTail.ToString("F2");

      if (last.Top3Share > det.MaxTop3Share)
        return "top3";

      double avgVol = history.AverageVolumeBefore(det.AverageWindow);
      bool volumeOk = avgVol <= 0
        || last.Volume >= avgVol * det.MinVolumeRatio
        || tickMove >= det.MinFlushTicks * 2;
      if (!volumeOk)
        return "volume volX=" + (last.Volume / avgVol).ToString("F2");

      double avgRange = history.AverageRangeBefore(det.AverageWindow);
      double density = (double)last.Volume / last.Range;
      double avgDensity = avgRange > 0 ? avgVol / avgRange : 0;
      bool densityOk = avgDensity <= 0
        || density >= avgDensity * det.MinDensityMultiplier
        || tickMove >= det.MinFlushTicks * 2;
      if (!densityOk)
        return string.Format(CultureInfo.InvariantCulture,
          "density {0:F0} < {1:F0} (avgDens {2:F0})",
          density, avgDensity * det.MinDensityMultiplier, avgDensity);

      return null;
    }
  }
}
