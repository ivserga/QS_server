// ==========================================================================
//  DoubleTopDetector.cs — Двойная вершина (M-pattern) на двух пиках близких
//  цен с провалом между ними и затуханием на втором пике
// ==========================================================================
//
//  Идея паттерна:
//    Пик 1 (старый) — крупняк может ещё покупать.
//    Между пиками — коррекция вниз ≥ MinCorrectionTicks.
//    Пик 2 (новый, не позже MinSecondPeakAgeBars баров назад) — на той же
//    или близкой цене, НО с признаками затухания: меньше объём, POC ниже,
//    POC съезжает к нижней части бара (продают «в верх»).
//    После пика 2 цена УЖЕ начала откат: ClosePrice последнего бара ниже
//    пика 2 как минимум на MinPostPeakDropTicks.
//
//  Алгоритм:
//    1. Находим локальные вершины в окне Lookback баров (через WindowMath).
//    2. Берём две самые свежие подходящие пары (peak2 более свежий, peak1 —
//       старый).
//    3. Проверяем:
//         |peakHigh1 - peakHigh2| ≤ MaxPeakDiffTicks
//         barsBetween ≥ MinBarsBetweenPeaks
//         коррекция между ними ≥ MinCorrectionTicks
//         peak2 на расстоянии MinSecondPeakAgeBars..MaxSecondPeakAgeBars от конца
//         currentClose ниже peak2 на ≥ MinPostPeakDropTicks
//    4. OHLC-слой пройден.
//
//  Cluster-uplift:
//    + PocCenter peak2 ≤ PocCenter peak1 + tol (сглаженный центр объёма
//      второго пика не выше первого; tol защищает от дрожания POC на 1 тик
//      между двумя соседними уровнями с близкими объёмами)
//    + Volume peak2 ≤ Volume peak1 * MaxVolumeRatio (затухание входа)
//    + PosPoc peak2 < PosPoc peak1 (POC съехал внутри бара вниз)
//    + Top3Share peak2 > Top3Share peak1 (большие лимитки удерживают)
//
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.View.ClustersSpace.Analytics.Detectors
{
  // ==========================================================================

  sealed class DoubleTopDetector : ISignalDetector
  {
    // **********************************************************************

    public string Name { get { return "DoubleTop"; } }
    public bool Enabled { get; set; }

    // --- параметры --------------------------------------------------------

    public int    Lookback                  { get; set; }
    public int    PeakLeftRight             { get; set; }
    public int    MaxPeakDiffTicks          { get; set; }
    public int    MinBarsBetweenPeaks       { get; set; }
    public int    MinCorrectionTicks        { get; set; }
    public int    MinSecondPeakAgeBars      { get; set; }
    public int    MaxSecondPeakAgeBars      { get; set; }
    public int    MinPostPeakDropTicks      { get; set; }

    public bool   UseClusterUplift          { get; set; }
    public double MaxVolumeRatio            { get; set; }
    /// <summary>
    /// Допуск (в priceStep) для сравнения PocCenter двух пиков. Учитывает,
    /// что сглаженный центр объёма может слегка плавать даже при практически
    /// идентичных профилях. 0.5 priceStep — стандартное значение.
    /// </summary>
    public double VolumeCenterTolTicks         { get; set; }

    public double MinStrength               { get; set; }
    public int    CooldownBars              { get; set; }

    // --- состояние --------------------------------------------------------

    DateTime lastEmittedAt = DateTime.MinValue;
    int      barsSinceLastEmit = int.MaxValue;

    // **********************************************************************

    public DoubleTopDetector()
    {
      Enabled                 = true;
      Lookback                = 25;
      PeakLeftRight           = 2;
      MaxPeakDiffTicks        = 5;
      MinBarsBetweenPeaks     = 3;
      MinCorrectionTicks      = 8;
      MinSecondPeakAgeBars    = 1;
      MaxSecondPeakAgeBars    = 5;
      MinPostPeakDropTicks    = 3;
      UseClusterUplift        = true;
      MaxVolumeRatio          = 1.10; // peak2.volume <= peak1.volume * 1.10 (т.е. ≈ не выше)
      VolumeCenterTolTicks       = 0.5;
      MinStrength             = 0.50;
      CooldownBars            = 8;
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

      var peaks = WindowMath.FindLocalPeakIndices(history, Lookback, PeakLeftRight);
      if(peaks.Count < 2) return null;

      // Идём от ПОСЛЕДНЕЙ свежей пары: peak2 = самый свежий допустимый,
      // peak1 = более старая вершина с подходящим diff.
      // peaks отсортированы от старых (большой index) к новым (малый index).
      // Берём peak2 = peaks[last], peak1 = peaks[last-1].
      int idx2 = peaks[peaks.Count - 1];
      int idx1 = peaks[peaks.Count - 2];

      // peak2 должен быть в окне [Min..Max] баров от конца.
      if(idx2 < MinSecondPeakAgeBars) return null;
      if(idx2 > MaxSecondPeakAgeBars) return null;

      var peak1 = history.Last(idx1);
      var peak2 = history.Last(idx2);
      if(peak1 == null || peak2 == null) return null;

      int diff = Math.Abs(peak1.MaxPrice - peak2.MaxPrice);
      if(diff > MaxPeakDiffTicks) return null;

      int barsBetween = idx1 - idx2;
      if(barsBetween < MinBarsBetweenPeaks) return null;

      // Коррекция между пиками: минимальный low в [idx2+1..idx1-1].
      int troughLow = WindowMath.LowestLowBetween(history, idx1 - 1, idx2 + 1);
      if(troughLow == int.MaxValue) return null;

      int peakLevel = Math.Max(peak1.MaxPrice, peak2.MaxPrice);
      int correction = peakLevel - troughLow;
      if(correction < MinCorrectionTicks) return null;

      // После peak2 цена уже откатилась.
      int postDrop = peak2.MaxPrice - last.ClosePrice;
      if(postDrop < MinPostPeakDropTicks) return null;

      // === Strength ===
      double diffScore       = 1.0 - Math.Min(1.0, diff / Math.Max(1.0, (double)MaxPeakDiffTicks));
      double correctionScore = Math.Min(1.0, (correction - MinCorrectionTicks) / Math.Max(1.0, (double)MinCorrectionTicks * 2));
      double dropScore       = Math.Min(1.0, postDrop / Math.Max(1.0, (double)MinPostPeakDropTicks * 3));

      double baseStrength = 0.40 * diffScore + 0.35 * correctionScore + 0.25 * dropScore;

      double uplift = 0;
      if(UseClusterUplift)
      {
        // Сравниваем сглаженный центр объёма с допуском, чтобы не штрафовать
        // за дискретный скачок POC на 1 тик при близких объёмах двух соседних
        // уровней (иначе один тик дрожания обнуляет весь uplift).
        double volCenter1 = WindowMath.VolumeCenter(peak1);
        double volCenter2 = WindowMath.VolumeCenter(peak2);
        double volTol     = VolumeCenterTolTicks * Math.Max(1, last.PriceStep);
        if(volCenter2 <= volCenter1 + volTol) uplift += 0.10;
        else uplift -= 0.05;

        if(peak1.Volume > 0)
        {
          double volRatio = (double)peak2.Volume / peak1.Volume;
          if(volRatio <= MaxVolumeRatio) uplift += 0.10;
          if(volRatio < 0.80) uplift += 0.05; // сильное затухание объёма
        }

        if(peak2.PosCom < peak1.PosCom) uplift += 0.05;

        if(peak2.Top3Share > peak1.Top3Share + 0.05) uplift += 0.05;
      }

      double strength = Math.Min(1.0, Math.Max(0, baseStrength + uplift));
      if(strength < MinStrength) return null;

      // === гистерезис ===
      if(last.Source.DateTime == lastEmittedAt) return null;
      lastEmittedAt = last.Source.DateTime;
      barsSinceLastEmit = 0;

      return new Signal
      {
        Time      = last.Source.DateTime,
        Kind      = SignalKind.DoubleTop,
        Direction = SignalDirection.Down,
        Price     = troughLow, // ближайшая цель — neckline (минимум впадины)
        Strength  = strength,
        Message   = FormatMessage(peak1, peak2, troughLow, idx2),
        Details   = FormatDetails(peak1, peak2, idx1, idx2, troughLow, correction, postDrop, baseStrength, uplift)
      };
    }

    // **********************************************************************

    static string FormatMessage(ClusterStats peak1, ClusterStats peak2, int neckline, int peak2AgeBars)
    {
      return string.Format(CultureInfo.InvariantCulture,
        "Двойная вершина: пики {0}/{1} ({2} бар(ов) назад), объём {3}/{4}, цель {5}",
        peak1.MaxPrice, peak2.MaxPrice, peak2AgeBars,
        FormatVolume(peak1.Volume), FormatVolume(peak2.Volume),
        neckline);
    }

    static string FormatDetails(ClusterStats peak1, ClusterStats peak2,
      int idx1, int idx2, int neckline, int correction, int postDrop,
      double baseStrength, double uplift)
    {
      return string.Format(CultureInfo.InvariantCulture,
        "p1Hi={0} p2Hi={1} idx1={2} idx2={3} neckline={4} correction={5} postDrop={6} p1Vol={7} p2Vol={8} p1Com={9} p2Com={10} p1VolC={11:F2} p2VolC={12:F2} p1PosCom={13:F2} p2PosCom={14:F2} base={15:F2} uplift={16:F2}",
        peak1.MaxPrice, peak2.MaxPrice, idx1, idx2, neckline, correction, postDrop,
        peak1.Volume, peak2.Volume, peak1.ComPrice, peak2.ComPrice,
        WindowMath.VolumeCenter(peak1), WindowMath.VolumeCenter(peak2),
        peak1.PosCom, peak2.PosCom, baseStrength, uplift);
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
