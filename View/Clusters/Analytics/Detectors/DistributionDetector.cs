// ==========================================================================
//  DistributionDetector.cs — Распределение (раздача) у вершины тренда
// ==========================================================================
//
//  Distribution — это структурный паттерн боковика у локальной вершины,
//  где «крупный игрок» постепенно сливает в рынок ранее набранный лонг.
//  В отличие от Climax (один экстремальный бар) и DoubleTop (две точечные
//  вершины), distribution растянут на 8–15 баров.
//
//  Двухслойная проверка: сначала классические OHLC-признаки, потом
//  cluster-uplift по профилю объёма. Если OHLC-слой не прошёл — детектор
//  не срабатывает; cluster-uplift только усиливает или ослабляет strength.
//
//  Формальные признаки (OHLC-слой):
//    1. В окне Lookback баров вторая половина имеет максимум, который
//       НЕ ВЫШЕ максимума первой половины (lower_highs / equal с допуском
//       MinSwingDiffTicks).
//    2. Slope линейной регрессии Volume по окну ≤ MaxVolumeSlope (объём
//       угасает; типичный порог -0.03..-0.05 / бар).
//    3. ClosePrice последнего бара ниже середины окна (highest+lowest)/2.
//    4. Highest_high был достигнут не позднее MinHighAgeBars баров назад
//       (чтобы не путать с активным аптрендом, где high — на текущем баре).
//
//  Cluster-uplift (если включён):
//    + Доля баров окна с PosPoc < PosComLowThreshold ≥ MinShareLowPosCom:
//      «POC регулярно сидит в нижней части бара» — продают в низ.
//    + Среднее Skewness по окну отрицательное (хвост распределения вниз).
//    + Volume последнего бара меньше среднего по окну (затухание входа).
//
//  Гистерезис:
//    После срабатывания детектор молчит CooldownBars баров — distribution
//    длится, новые сигналы каждую минуту не нужны.
//
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.View.ClustersSpace.Analytics.Detectors
{
  // ==========================================================================

  sealed class DistributionDetector : ISignalDetector
  {
    // **********************************************************************

    public string Name { get { return "Distribution"; } }
    public bool Enabled { get; set; }

    // --- параметры --------------------------------------------------------

    /// <summary>Размер окна анализа (баров, включая текущий).</summary>
    public int    Lookback              { get; set; }

    /// <summary>Допуск (в priceStep) для сравнения half-highs: lower_highs только если diff &gt; tol.</summary>
    public int    MinSwingDiffTicks     { get; set; }

    /// <summary>Максимально допустимый slope объёма (отрицательный = угасание). Например -0.03 = -3%/бар.</summary>
    public double MaxVolumeSlope        { get; set; }

    /// <summary>Возраст highest_high в барах: должен быть ≥ этого значения, иначе тренд ещё активный.</summary>
    public int    MinHighAgeBars        { get; set; }

    // --- cluster uplift ---

    /// <summary>Учитывать ли кластерные подтверждения (+поднимает strength, без них может не сработать).</summary>
    public bool   UseClusterUplift      { get; set; }

    /// <summary>«Низкий» pos_poc — продают в низ бара.</summary>
    public double PosComLowThreshold    { get; set; }

    /// <summary>Минимальная доля баров с pos_poc &lt; threshold для бонуса.</summary>
    public double MinShareLowPosCom     { get; set; }

    /// <summary>Минимальная итоговая strength для эмиссии сигнала.</summary>
    public double MinStrength           { get; set; }

    /// <summary>Сколько баров молчать после срабатывания.</summary>
    public int    CooldownBars          { get; set; }

    // --- состояние --------------------------------------------------------

    DateTime lastEmittedAt = DateTime.MinValue;
    int      barsSinceLastEmit = int.MaxValue;

    // **********************************************************************

    public DistributionDetector()
    {
      Enabled              = true;
      Lookback             = 12;
      MinSwingDiffTicks    = 2;
      MaxVolumeSlope       = -0.03;
      MinHighAgeBars       = 2;
      UseClusterUplift     = true;
      PosComLowThreshold   = 0.40;
      MinShareLowPosCom    = 0.45;
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

      // 1. Two-halves highs.
      int hi1, hi2;
      if(!WindowMath.TwoHalvesHighs(history, Lookback, out hi1, out hi2)) return null;

      int swingDiff = hi1 - hi2; // > 0 = lower_highs (1-я половина выше)
      if(swingDiff < MinSwingDiffTicks) return null;

      // 2. Volume slope: должен быть отрицательным сильнее MaxVolumeSlope.
      double volSlope = WindowMath.SlopePctVolume(history, 0, Lookback);
      if(volSlope > MaxVolumeSlope) return null;

      // 3. Last close ниже середины окна.
      int hh = WindowMath.HighestHigh(history, 0, Lookback);
      int ll = WindowMath.LowestLow(history, 0, Lookback);
      if(hh == int.MinValue || ll == int.MaxValue) return null;
      double mid = (hh + ll) * 0.5;
      if(last.ClosePrice >= mid) return null;

      // 4. Возраст highest_high.
      int hhAge = AgeOfHighestHigh(history, Lookback, hh);
      if(hhAge < MinHighAgeBars) return null;

      // === OHLC-слой пройден — считаем strength ===

      // Базовая strength: насколько ярко выражены lower_highs и угасание объёма.
      double swingScore = Math.Min(1.0, swingDiff / Math.Max(1.0, hh * 0.005)); // 0.5% от high уже = 1.0
      double volScore   = Math.Min(1.0, (MaxVolumeSlope - volSlope) / Math.Max(1e-6, Math.Abs(MaxVolumeSlope) * 2.0));
      double midScore   = Math.Min(1.0, (mid - last.ClosePrice) / Math.Max(1.0, (hh - ll) * 0.5));

      double baseStrength = 0.40 * swingScore + 0.40 * Math.Max(0, volScore) + 0.20 * Math.Max(0, midScore);

      // === Cluster-uplift ===
      double uplift = 0;
      double sharePosLow = 0;
      double avgSkew = 0;

      if(UseClusterUplift)
      {
        sharePosLow = WindowMath.SharePosComBelow(history, Lookback, PosComLowThreshold);
        if(sharePosLow >= MinShareLowPosCom)
          uplift += 0.15 * Math.Min(1.0, (sharePosLow - MinShareLowPosCom) / Math.Max(1e-6, 1.0 - MinShareLowPosCom) + 0.5);

        avgSkew = WindowMath.AverageSkewness(history, Lookback);
        if(avgSkew < 0)
          uplift += 0.10 * Math.Min(1.0, -avgSkew / 0.5);

        // Объём последнего бара меньше среднего по окну.
        double avgVol = history.AverageVolumeBefore(Lookback - 1);
        if(avgVol > 0 && last.Volume < avgVol * 0.85)
          uplift += 0.05;
      }

      double strength = Math.Min(1.0, baseStrength + uplift);
      if(strength < MinStrength) return null;

      // === гистерезис ===
      if(last.Source.DateTime == lastEmittedAt) return null;
      lastEmittedAt = last.Source.DateTime;
      barsSinceLastEmit = 0;

      return new Signal
      {
        Time      = last.Source.DateTime,
        Kind      = SignalKind.Distribution,
        Direction = SignalDirection.Down,
        Price     = ll, // ближайший целевой уровень — нижняя граница окна
        Strength  = strength,
        Message   = FormatMessage(hi1, hi2, volSlope, last.ClosePrice, ll, sharePosLow),
        Details   = FormatDetails(hi1, hi2, hh, ll, hhAge, volSlope, sharePosLow, avgSkew, baseStrength, uplift)
      };
    }

    // **********************************************************************

    static int AgeOfHighestHigh(ClusterHistory history, int count, int hh)
    {
      for(int i = 0; i < count; i++)
      {
        var s = history.Last(i);
        if(s == null) return count;
        if(s.MaxPrice == hh) return i;
      }
      return count;
    }

    // **********************************************************************

    static string FormatMessage(int hi1, int hi2, double volSlope,
      int lastClose, int targetLow, double sharePosLow)
    {
      int volPct = (int)Math.Round(volSlope * 100);
      int posLowPct = (int)Math.Round(sharePosLow * 100);

      return string.Format(CultureInfo.InvariantCulture,
        "Распределение у вершины: lower_highs {0}→{1}, объём {2:+0;-0;0}%/бар, COM внизу баров {3}% — цель {4}",
        hi1, hi2, volPct, posLowPct, targetLow);
    }

    static string FormatDetails(int hi1, int hi2, int hh, int ll, int hhAge,
      double volSlope, double sharePosLow, double avgSkew,
      double baseStrength, double uplift)
    {
      return string.Format(CultureInfo.InvariantCulture,
        "hi1={0} hi2={1} hh={2} ll={3} hhAge={4} volSlope={5:F3} posLowShare={6:F2} avgSkew={7:F2} base={8:F2} uplift={9:F2}",
        hi1, hi2, hh, ll, hhAge, volSlope, sharePosLow, avgSkew, baseStrength, uplift);
    }

    // **********************************************************************
  }

  // ==========================================================================
}
