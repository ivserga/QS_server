// ==========================================================================
//  AccumulationDetector.cs — Накопление у дна тренда (зеркало Distribution)
// ==========================================================================
//
//  Accumulation — структурный паттерн боковика у локального дна, где
//  «крупный игрок» постепенно набирает позицию у поддержки. Зеркальный
//  детектор к Distribution; см. там же подробное описание двухслойного
//  алгоритма (OHLC + cluster uplift).
//
//  Формальные признаки (OHLC-слой):
//    1. В окне Lookback баров вторая половина имеет минимум, который
//       НЕ НИЖЕ минимума первой половины (higher_lows / equal с допуском
//       MinSwingDiffTicks).
//    2. Slope линейной регрессии Volume по окну ≥ MinVolumeSlope (объём
//       НЕ угасает; для accumulation типичный порог 0.0..+0.03 / бар).
//       Объём не обязан расти — главное чтобы не падал лавинообразно.
//    3. ClosePrice последнего бара выше середины окна.
//    4. Lowest_low был достигнут не позднее MinLowAgeBars баров назад.
//
//  Cluster-uplift:
//    + Доля баров с PosPoc &gt; PosComHighThreshold ≥ MinShareHighPosCom:
//      «POC регулярно сидит в верхней части бара» — покупают «в верх».
//    + Среднее Skewness по окну положительное (хвост распределения вверх).
//    + Volume последнего бара выше среднего (свежий приток покупок).
//
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.View.ClustersSpace.Analytics.Detectors
{
  // ==========================================================================

  sealed class AccumulationDetector : ISignalDetector
  {
    // **********************************************************************

    public string Name { get { return "Accumulation"; } }
    public bool Enabled { get; set; }

    // --- параметры --------------------------------------------------------

    public int    Lookback              { get; set; }
    public int    MinSwingDiffTicks     { get; set; }
    public double MinVolumeSlope        { get; set; }
    public int    MinLowAgeBars         { get; set; }

    public bool   UseClusterUplift      { get; set; }
    public double PosComHighThreshold   { get; set; }
    public double MinShareHighPosCom    { get; set; }

    public double MinStrength           { get; set; }
    public int    CooldownBars          { get; set; }

    // --- состояние --------------------------------------------------------

    DateTime lastEmittedAt = DateTime.MinValue;
    int      barsSinceLastEmit = int.MaxValue;

    // **********************************************************************

    public AccumulationDetector()
    {
      Enabled              = true;
      Lookback             = 12;
      MinSwingDiffTicks    = 2;
      MinVolumeSlope       = 0.00;   // объём не обязан расти, главное не падает
      MinLowAgeBars        = 2;
      UseClusterUplift     = true;
      PosComHighThreshold  = 0.60;
      MinShareHighPosCom   = 0.45;
      MinStrength          = 0.50;
      CooldownBars         = 5;
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

      // 1. Two-halves lows.
      int lo1, lo2;
      if(!WindowMath.TwoHalvesLows(history, Lookback, out lo1, out lo2)) return null;

      int swingDiff = lo2 - lo1; // > 0 = higher_lows (2-я половина выше)
      if(swingDiff < MinSwingDiffTicks) return null;

      // 2. Volume slope: не должен сильно падать.
      double volSlope = WindowMath.SlopePctVolume(history, 0, Lookback);
      if(volSlope < MinVolumeSlope) return null;

      // 3. Last close выше середины окна.
      int hh = WindowMath.HighestHigh(history, 0, Lookback);
      int ll = WindowMath.LowestLow(history, 0, Lookback);
      if(hh == int.MinValue || ll == int.MaxValue) return null;
      double mid = (hh + ll) * 0.5;
      if(last.ClosePrice <= mid) return null;

      // 4. Возраст lowest_low.
      int llAge = AgeOfLowestLow(history, Lookback, ll);
      if(llAge < MinLowAgeBars) return null;

      // === OHLC-слой пройден ===

      double swingScore = Math.Min(1.0, swingDiff / Math.Max(1.0, ll * 0.005));
      double volScore   = volSlope >= 0
                            ? Math.Min(1.0, volSlope / 0.05 + 0.4)        // даже плоский объём = 0.4
                            : 0.2;                                         // отрицательный, но не сильно
      double midScore   = Math.Min(1.0, (last.ClosePrice - mid) / Math.Max(1.0, (hh - ll) * 0.5));

      double baseStrength = 0.40 * swingScore + 0.30 * volScore + 0.30 * Math.Max(0, midScore);

      // === Cluster-uplift ===
      double uplift = 0;
      double sharePosHigh = 0;
      double avgSkew = 0;

      if(UseClusterUplift)
      {
        sharePosHigh = WindowMath.SharePosComAbove(history, Lookback, PosComHighThreshold);
        if(sharePosHigh >= MinShareHighPosCom)
          uplift += 0.15 * Math.Min(1.0, (sharePosHigh - MinShareHighPosCom) / Math.Max(1e-6, 1.0 - MinShareHighPosCom) + 0.5);

        avgSkew = WindowMath.AverageSkewness(history, Lookback);
        if(avgSkew > 0)
          uplift += 0.10 * Math.Min(1.0, avgSkew / 0.5);

        double avgVol = history.AverageVolumeBefore(Lookback - 1);
        if(avgVol > 0 && last.Volume > avgVol * 1.10)
          uplift += 0.05;
      }

      double strength = Math.Min(1.0, baseStrength + uplift);
      if(strength < MinStrength) return null;

      if(last.Source.DateTime == lastEmittedAt) return null;
      lastEmittedAt = last.Source.DateTime;
      barsSinceLastEmit = 0;

      return new Signal
      {
        Time      = last.Source.DateTime,
        Kind      = SignalKind.Accumulation,
        Direction = SignalDirection.Up,
        Price     = hh,
        Strength  = strength,
        Message   = FormatMessage(lo1, lo2, volSlope, last.ClosePrice, hh, sharePosHigh),
        Details   = FormatDetails(lo1, lo2, hh, ll, llAge, volSlope, sharePosHigh, avgSkew, baseStrength, uplift)
      };
    }

    // **********************************************************************

    static int AgeOfLowestLow(ClusterHistory history, int count, int ll)
    {
      for(int i = 0; i < count; i++)
      {
        var s = history.Last(i);
        if(s == null) return count;
        if(s.MinPrice == ll) return i;
      }
      return count;
    }

    // **********************************************************************

    static string FormatMessage(int lo1, int lo2, double volSlope,
      int lastClose, int targetHigh, double sharePosHigh)
    {
      int volPct = (int)Math.Round(volSlope * 100);
      int posHighPct = (int)Math.Round(sharePosHigh * 100);

      return string.Format(CultureInfo.InvariantCulture,
        "Накопление у дна: higher_lows {0}→{1}, объём {2:+0;-0;0}%/бар, COM вверху баров {3}% — цель {4}",
        lo1, lo2, volPct, posHighPct, targetHigh);
    }

    static string FormatDetails(int lo1, int lo2, int hh, int ll, int llAge,
      double volSlope, double sharePosHigh, double avgSkew,
      double baseStrength, double uplift)
    {
      return string.Format(CultureInfo.InvariantCulture,
        "lo1={0} lo2={1} hh={2} ll={3} llAge={4} volSlope={5:F3} posHighShare={6:F2} avgSkew={7:F2} base={8:F2} uplift={9:F2}",
        lo1, lo2, hh, ll, llAge, volSlope, sharePosHigh, avgSkew, baseStrength, uplift);
    }

    // **********************************************************************
  }

  // ==========================================================================
}
