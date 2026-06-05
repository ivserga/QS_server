// ==========================================================================
//  DoubleBottomDetector.cs — Двойное дно (W-pattern), зеркало DoubleTop
// ==========================================================================
//
//  Идея паттерна:
//    Дно 1 (старое) — крупняк ещё может продавать.
//    Между донами — отскок вверх ≥ MinReboundTicks.
//    Дно 2 (новое, не позже MinSecondTroughAgeBars баров назад) — на той
//    же или близкой цене, НО продавцы устают: меньше объём, POC выше,
//    POC уезжает к верхней части бара (покупают «в низ»).
//    После дна 2 цена УЖЕ начала расти: ClosePrice последнего бара выше
//    дна 2 как минимум на MinPostTroughRiseTicks.
//
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.View.ClustersSpace.Analytics.Detectors
{
  // ==========================================================================

  sealed class DoubleBottomDetector : ISignalDetector
  {
    // **********************************************************************

    public string Name { get { return "DoubleBottom"; } }
    public bool Enabled { get; set; }

    // --- параметры --------------------------------------------------------

    public int    Lookback                 { get; set; }
    public int    TroughLeftRight          { get; set; }
    public int    MaxTroughDiffTicks       { get; set; }
    public int    MinBarsBetweenTroughs    { get; set; }
    public int    MinReboundTicks          { get; set; }
    public int    MinSecondTroughAgeBars   { get; set; }
    public int    MaxSecondTroughAgeBars   { get; set; }
    public int    MinPostTroughRiseTicks   { get; set; }

    public bool   UseClusterUplift         { get; set; }
    public double MaxVolumeRatio           { get; set; }
    /// <summary>
    /// Допуск (в priceStep) для сравнения PocCenter двух дон. См. описание
    /// в DoubleTopDetector.VolumeCenterTolTicks.
    /// </summary>
    public double VolumeCenterTolTicks        { get; set; }

    public double MinStrength              { get; set; }
    public int    CooldownBars             { get; set; }

    // --- состояние --------------------------------------------------------

    DateTime lastEmittedAt = DateTime.MinValue;
    int      barsSinceLastEmit = int.MaxValue;

    // **********************************************************************

    public DoubleBottomDetector()
    {
      Enabled                = true;
      Lookback               = 25;
      TroughLeftRight        = 2;
      MaxTroughDiffTicks     = 5;
      MinBarsBetweenTroughs  = 3;
      MinReboundTicks        = 8;
      MinSecondTroughAgeBars = 1;
      MaxSecondTroughAgeBars = 5;
      MinPostTroughRiseTicks = 3;
      UseClusterUplift       = true;
      MaxVolumeRatio         = 1.10;
      VolumeCenterTolTicks      = 0.5;
      MinStrength            = 0.50;
      CooldownBars           = 8;
    }

    // **********************************************************************

    public Signal Evaluate(ClusterHistory history)
    {
      if(history == null) return null;
      if(history.Count < Lookback) return null;

      if(barsSinceLastEmit != int.MaxValue) barsSinceLastEmit++;
      if(barsSinceLastEmit < CooldownBars) return null;

      var last = history.Last(0);
      if(last == null) return null;

      var troughs = WindowMath.FindLocalTroughIndices(history, Lookback, TroughLeftRight);
      if(troughs.Count < 2) return null;

      int idx2 = troughs[troughs.Count - 1];
      int idx1 = troughs[troughs.Count - 2];

      if(idx2 < MinSecondTroughAgeBars) return null;
      if(idx2 > MaxSecondTroughAgeBars) return null;

      var trough1 = history.Last(idx1);
      var trough2 = history.Last(idx2);
      if(trough1 == null || trough2 == null) return null;

      int diff = Math.Abs(trough1.MinPrice - trough2.MinPrice);
      if(diff > MaxTroughDiffTicks) return null;

      int barsBetween = idx1 - idx2;
      if(barsBetween < MinBarsBetweenTroughs) return null;

      int peakHigh = WindowMath.HighestHighBetween(history, idx1 - 1, idx2 + 1);
      if(peakHigh == int.MinValue) return null;

      int troughLevel = Math.Min(trough1.MinPrice, trough2.MinPrice);
      int rebound = peakHigh - troughLevel;
      if(rebound < MinReboundTicks) return null;

      int postRise = last.ClosePrice - trough2.MinPrice;
      if(postRise < MinPostTroughRiseTicks) return null;

      // === Strength ===
      double diffScore     = 1.0 - Math.Min(1.0, diff / Math.Max(1.0, (double)MaxTroughDiffTicks));
      double reboundScore  = Math.Min(1.0, (rebound - MinReboundTicks) / Math.Max(1.0, (double)MinReboundTicks * 2));
      double riseScore     = Math.Min(1.0, postRise / Math.Max(1.0, (double)MinPostTroughRiseTicks * 3));

      double baseStrength = 0.40 * diffScore + 0.35 * reboundScore + 0.25 * riseScore;

      double uplift = 0;
      if(UseClusterUplift)
      {
        // Сглаженный центр объёма с допуском (см. DoubleTopDetector).
        double volCenter1 = WindowMath.VolumeCenter(trough1);
        double volCenter2 = WindowMath.VolumeCenter(trough2);
        double volTol     = VolumeCenterTolTicks * Math.Max(1, last.PriceStep);
        if(volCenter2 >= volCenter1 - volTol) uplift += 0.10;
        else uplift -= 0.05;

        if(trough1.Volume > 0)
        {
          double volRatio = (double)trough2.Volume / trough1.Volume;
          if(volRatio <= MaxVolumeRatio) uplift += 0.10;
          if(volRatio < 0.80) uplift += 0.05;
        }

        if(trough2.PosCom > trough1.PosCom) uplift += 0.05;

        if(trough2.Top3Share > trough1.Top3Share + 0.05) uplift += 0.05;
      }

      double strength = Math.Min(1.0, Math.Max(0, baseStrength + uplift));
      if(strength < MinStrength) return null;

      if(last.Source.DateTime == lastEmittedAt) return null;
      lastEmittedAt = last.Source.DateTime;
      barsSinceLastEmit = 0;

      return new Signal
      {
        Time      = last.Source.DateTime,
        Kind      = SignalKind.DoubleBottom,
        Direction = SignalDirection.Up,
        Price     = peakHigh,
        Strength  = strength,
        Message   = FormatMessage(trough1, trough2, peakHigh, idx2),
        Details   = FormatDetails(trough1, trough2, idx1, idx2, peakHigh, rebound, postRise, baseStrength, uplift)
      };
    }

    // **********************************************************************

    static string FormatMessage(ClusterStats t1, ClusterStats t2, int neckline, int t2AgeBars)
    {
      return string.Format(CultureInfo.InvariantCulture,
        "Двойное дно: минимумы {0}/{1} ({2} бар(ов) назад), объём {3}/{4}, цель {5}",
        t1.MinPrice, t2.MinPrice, t2AgeBars,
        FormatVolume(t1.Volume), FormatVolume(t2.Volume),
        neckline);
    }

    static string FormatDetails(ClusterStats t1, ClusterStats t2,
      int idx1, int idx2, int neckline, int rebound, int postRise,
      double baseStrength, double uplift)
    {
      return string.Format(CultureInfo.InvariantCulture,
        "t1Lo={0} t2Lo={1} idx1={2} idx2={3} neckline={4} rebound={5} postRise={6} t1Vol={7} t2Vol={8} t1Com={9} t2Com={10} t1VolC={11:F2} t2VolC={12:F2} t1PosCom={13:F2} t2PosCom={14:F2} base={15:F2} uplift={16:F2}",
        t1.MinPrice, t2.MinPrice, idx1, idx2, neckline, rebound, postRise,
        t1.Volume, t2.Volume, t1.ComPrice, t2.ComPrice,
        WindowMath.VolumeCenter(t1), WindowMath.VolumeCenter(t2),
        t1.PosCom, t2.PosCom, baseStrength, uplift);
    }

    static string FormatVolume(long v)
    {
      if(v >= 1_000_000) return (v / 1000_000.0).ToString("F1", CultureInfo.InvariantCulture) + "M";
      if(v >= 1_000)     return (v / 1000.0).ToString("F1", CultureInfo.InvariantCulture) + "k";
      return v.ToString(CultureInfo.InvariantCulture);
    }

    // **********************************************************************
  }

  // ==========================================================================
}
