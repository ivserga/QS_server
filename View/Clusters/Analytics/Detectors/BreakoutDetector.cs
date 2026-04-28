// ==========================================================================
//  BreakoutDetector.cs — Чистый пробой диапазона (вверх / вниз)
// ==========================================================================
//
//  Breakout — момент, когда цена закрывается ВЫШЕ максимума прошлых N
//  закрытых баров (или НИЖЕ минимума) с подтверждающим объёмом и реальным
//  телом свечи. В отличие от PocMigration (который ловит «продолжение тренда»
//  по 3 POC подряд) и Climax (один экстремальный бар), Breakout — это именно
//  смена диапазона: предыдущие N баров были ограничены сверху уровнем X,
//  а текущий бар X пробил.
//
//  Двухслойная проверка. OHLC-слой:
//    1. ClosePrice последнего бара > HighestHigh(prior LookbackBars baров,
//       исключая последний) + MinBreakoutTicks (для пробоя вверх).
//    2. body_ratio = |close-open|/range последнего бара ≥ MinBodyRatio
//       (отсев doji / шипов без тела).
//    3. Volume ≥ avg_volume(LookbackBars) * MinVolumeRatio.
//
//  Cluster-uplift (если включён):
//    + PosPoc у верхней (для up-пробоя) части бара ≥ PosPocFavorable:
//      «POC ушёл вверх — реально торговали на пробое, не просто шип».
//    + Top3Share НЕ выше MaxTop3Share: иначе это абсорбция (HFT мучает
//      уровень), а не настоящий пробой.
//    + Skewness в сторону пробоя (положительная для up).
//
//  Гистерезис: после срабатывания CooldownBars баров молчим, чтобы не
//  спамить «второй пробой» на следующем баре в том же направлении.
//
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.View.ClustersSpace.Analytics.Detectors
{
  // ==========================================================================

  sealed class BreakoutDetector : ISignalDetector
  {
    // **********************************************************************

    public string Name { get { return "Breakout"; } }
    public bool Enabled { get; set; }

    // --- параметры --------------------------------------------------------

    /// <summary>Сколько предыдущих баров рассматривать как «диапазон».</summary>
    public int    LookbackBars        { get; set; }

    /// <summary>На сколько priceStep close должен превышать prior high (или быть ниже prior low).</summary>
    public int    MinBreakoutTicks    { get; set; }

    /// <summary>Минимальное тело последнего бара (|close-open|/range).</summary>
    public double MinBodyRatio        { get; set; }

    /// <summary>Минимальное соотношение volume / avg_volume по окну.</summary>
    public double MinVolumeRatio      { get; set; }

    // --- cluster uplift ---

    public bool   UseClusterUplift    { get; set; }

    /// <summary>«Благоприятный» pos_poc — для пробоя вверх ≥, для пробоя вниз ≤ (1 - threshold).</summary>
    public double PosPocFavorable     { get; set; }

    /// <summary>Если top3_share выше — это абсорбция / уплотнение, а не пробой.</summary>
    public double MaxTop3Share        { get; set; }

    public double MinStrength         { get; set; }
    public int    CooldownBars        { get; set; }

    // --- состояние --------------------------------------------------------

    DateTime lastEmittedAt = DateTime.MinValue;
    int      barsSinceLastEmit = int.MaxValue;

    // **********************************************************************

    public BreakoutDetector()
    {
      Enabled            = true;
      LookbackBars       = 10;
      MinBreakoutTicks   = 2;
      MinBodyRatio       = 0.45;
      MinVolumeRatio     = 1.00;
      UseClusterUplift   = true;
      PosPocFavorable    = 0.60;
      MaxTop3Share       = 0.55;
      MinStrength        = 0.50;
      CooldownBars       = 3;
    }

    // **********************************************************************

    public Signal Evaluate(ClusterHistory history)
    {
      if(history == null) return null;
      if(history.Count < LookbackBars + 1) return null;

      if(barsSinceLastEmit != int.MaxValue) barsSinceLastEmit++;
      if(barsSinceLastEmit < CooldownBars) return null;

      var last = history.Last(0);
      if(last == null || last.Range <= 0 || last.Volume == 0) return null;

      // Сначала пробуем пробой вверх, потом вниз.
      var up = TryEvaluate(history, last, true);
      if(up != null) return Commit(up, last.Source.DateTime);

      var down = TryEvaluate(history, last, false);
      if(down != null) return Commit(down, last.Source.DateTime);

      return null;
    }

    // **********************************************************************

    Signal TryEvaluate(ClusterHistory history, ClusterStats last, bool up)
    {
      // 1. Пробой OHLC-уровня.
      int priorExtreme = up
        ? WindowMath.HighestHighExcludingLast(history, LookbackBars, 1)
        : WindowMath.LowestLowExcludingLast(history, LookbackBars, 1);

      if(up)
      {
        if(priorExtreme == int.MinValue) return null;
        if(last.ClosePrice < priorExtreme + MinBreakoutTicks) return null;
      }
      else
      {
        if(priorExtreme == int.MaxValue) return null;
        if(last.ClosePrice > priorExtreme - MinBreakoutTicks) return null;
      }

      // 2. Тело свечи в нужную сторону.
      int body = last.ClosePrice - last.OpenPrice;
      if(up && body <= 0) return null;
      if(!up && body >= 0) return null;

      double bodyRatio = Math.Abs(body) / (double)last.Range;
      if(bodyRatio < MinBodyRatio) return null;

      // 3. Объём.
      double avgVol = history.AverageVolumeBefore(LookbackBars);
      if(avgVol > 0 && last.Volume < avgVol * MinVolumeRatio) return null;

      // === Strength ===
      int breakDepth = up
        ? last.ClosePrice - priorExtreme
        : priorExtreme - last.ClosePrice;

      double depthScore = Math.Min(1.0, breakDepth / Math.Max(1.0, MinBreakoutTicks * 5.0));
      double bodyScore  = Math.Min(1.0, (bodyRatio - MinBodyRatio) / Math.Max(1e-6, 1.0 - MinBodyRatio));
      double volScore   = avgVol > 0
                            ? Math.Min(1.0, ((double)last.Volume / avgVol - MinVolumeRatio) / Math.Max(1e-6, MinVolumeRatio))
                            : 0.3;

      double baseStrength = 0.40 * depthScore + 0.35 * bodyScore + 0.25 * Math.Max(0, volScore);

      double uplift = 0;
      if(UseClusterUplift)
      {
        // pos_poc в нужной части бара
        bool posOk = up ? (last.PosPoc >= PosPocFavorable) : (last.PosPoc <= 1.0 - PosPocFavorable);
        if(posOk) uplift += 0.12;

        // Не должно быть слишком высокой top3_share (иначе это абсорбция).
        if(last.Top3Share <= MaxTop3Share) uplift += 0.05;
        else uplift -= 0.15; // штраф

        // Skewness в сторону пробоя
        if(up && last.Skewness > 0) uplift += 0.05;
        if(!up && last.Skewness < 0) uplift += 0.05;

        // Shape: не Thin
        if(last.Shape == ProfileShape.Thin) uplift -= 0.10;
      }

      double strength = Math.Min(1.0, Math.Max(0, baseStrength + uplift));
      if(strength < MinStrength) return null;

      return new Signal
      {
        Time      = last.Source.DateTime,
        Kind      = up ? SignalKind.BreakoutUp : SignalKind.BreakoutDown,
        Direction = up ? SignalDirection.Up : SignalDirection.Down,
        Price     = priorExtreme,
        Strength  = strength,
        Message   = FormatMessage(up, priorExtreme, last.ClosePrice, bodyRatio, last.Volume, avgVol),
        Details   = FormatDetails(up, priorExtreme, last, breakDepth, bodyRatio, avgVol, baseStrength, uplift)
      };
    }

    // **********************************************************************

    Signal Commit(Signal s, DateTime barTime)
    {
      if(barTime == lastEmittedAt) return null;
      lastEmittedAt = barTime;
      barsSinceLastEmit = 0;
      return s;
    }

    // **********************************************************************

    static string FormatMessage(bool up, int priorExtreme, int close, double bodyRatio,
      long volume, double avgVol)
    {
      double volX = avgVol > 0 ? (double)volume / avgVol : 0;
      string side = up ? "вверх" : "вниз";
      string action = up ? "выше" : "ниже";

      return string.Format(CultureInfo.InvariantCulture,
        "Пробой {0}: close {1} {2} уровня {3}, тело {4}% диапазона, объём x{5:F1}",
        side, close, action, priorExtreme, (int)Math.Round(bodyRatio * 100), volX);
    }

    static string FormatDetails(bool up, int priorExtreme, ClusterStats s,
      int breakDepth, double bodyRatio, double avgVol, double baseStrength, double uplift)
    {
      return string.Format(CultureInfo.InvariantCulture,
        "dir={0} priorExt={1} close={2} breakDepth={3} body={4:F2} vol={5} avgVol={6:F0} posPoc={7:F2} top3Share={8:F2} skew={9:F2} shape={10} base={11:F2} uplift={12:F2}",
        up ? "up" : "down",
        priorExtreme,
        s.ClosePrice,
        breakDepth,
        bodyRatio,
        s.Volume,
        avgVol,
        s.PosPoc,
        s.Top3Share,
        s.Skewness,
        s.Shape,
        baseStrength,
        uplift);
    }

    // **********************************************************************
  }

  // ==========================================================================
}
