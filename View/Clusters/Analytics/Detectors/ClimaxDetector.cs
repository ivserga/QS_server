// ==========================================================================
//  ClimaxDetector.cs — Продающий/покупающий климакс (ситуация NVDA 14:03)
// ==========================================================================
//
//  Климакс — лавинная капитуляция одной из сторон в узкой ценовой зоне:
//  за одну минуту на 2-3 соседних ценовых уровнях проходит непропорционально
//  большая доля объёма бара, диапазон хода сжат относительно объёма.
//
//  Формальные признаки на последнем закрытом кластере:
//    • Top3Share >= MinTop3Share (40% объёма на трёх смежных тиках)
//    • Volume >= VolumeMultiplier * avg(N последних) — объём не ниже тренда
//    • Плотность = Volume / max(1, Range) >= MinDensityMultiplier * ср. плотности
//    • PosPoc у низа (<=0.25) → selling climax, у верха (>=0.75) → buying climax
//
//  Это отдельная сущность от Absorption: там шип формируется лимитным
//  защитником уровня; здесь — маркет-капитуляция агрессора в узкой зоне.
//
//  Гистерезис: сигнал выдаётся не более одного раза на один и тот же
//  кластер. Кроме того, чтобы избежать ложных срабатываний внутри тренда,
//  требуется, чтобы рядом не было такого же сигнала в последних K барах.
//
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.View.ClustersSpace.Analytics.Detectors
{
  // ==========================================================================

  sealed class ClimaxDetector : ISignalDetector
  {
    // **********************************************************************

    public string Name { get { return "Climax"; } }
    public bool Enabled { get; set; }

    // --- параметры --------------------------------------------------------

    public double MinTop3Share        { get; set; }
    public double VolumeMultiplier    { get; set; }
    public double MinDensityMultiplier{ get; set; }
    public double EdgePosComTop       { get; set; }
    public double EdgePosComBottom    { get; set; }
    public int AverageWindow          { get; set; }
    public int CooldownBars           { get; set; }

    // --- состояние --------------------------------------------------------

    DateTime lastEmittedAt = DateTime.MinValue;
    int barsSinceLastEmit = int.MaxValue;

    // **********************************************************************

    public ClimaxDetector()
    {
      Enabled = true;
      MinTop3Share = 0.40;
      VolumeMultiplier = 1.0;
      MinDensityMultiplier = 1.3;
      EdgePosComTop = 0.75;
      EdgePosComBottom = 0.25;
      AverageWindow = 5;
      CooldownBars = 3;
    }

    // **********************************************************************

    public Signal Evaluate(ClusterHistory history)
    {
      var s = history.Last(0);
      if(s == null)
        return null;

      if(barsSinceLastEmit != int.MaxValue)
        barsSinceLastEmit++;

      if(s.Volume == 0 || s.Range <= 0)
        return null;

      // Концентрация в узкой зоне.
      if(s.Top3Share < MinTop3Share)
        return null;

      // Направление по позиции POC.
      bool selling = s.PosCom <= EdgePosComBottom;
      bool buying  = s.PosCom >= EdgePosComTop;

      if(!selling && !buying)
        return null;

      // Объём не ниже среднего по окну.
      double avgVol = history.AverageVolumeBefore(AverageWindow);
      if(avgVol > 0 && s.Volume < avgVol * VolumeMultiplier)
        return null;

      // Плотность (объём на тик) сильно выше среднего.
      double avgRange = history.AverageRangeBefore(AverageWindow);
      double density = (double)s.Volume / Math.Max(1, s.Range);
      double avgDensity = avgRange > 0 ? (avgVol / avgRange) : 0;

      if(avgDensity > 0 && density < avgDensity * MinDensityMultiplier)
        return null;

      // Гистерезис по кластеру и по окну.
      if(s.Source.DateTime == lastEmittedAt)
        return null;

      if(barsSinceLastEmit < CooldownBars)
        return null;

      lastEmittedAt = s.Source.DateTime;
      barsSinceLastEmit = 0;

      // Сила: насколько концентрация и плотность выше порогов.
      double top3Score  = Math.Min(1.0, (s.Top3Share - MinTop3Share) / Math.Max(1e-6, 1.0 - MinTop3Share));
      double densScore  = avgDensity > 0
                            ? Math.Min(1.0, (density / avgDensity - MinDensityMultiplier) / MinDensityMultiplier)
                            : 0.3;
      double strength = 0.6 * top3Score + 0.4 * Math.Max(0, densScore);

      return new Signal
      {
        Time = s.Source.DateTime,
        Kind = selling ? SignalKind.SellingClimax : SignalKind.BuyingClimax,
        Direction = selling ? SignalDirection.Up : SignalDirection.Down, // ожидаемый откат
        Price = s.ComPrice,
        Strength = strength,
        Message = FormatMessage(s, selling, density, avgDensity),
        Details = FormatDetails(s, selling, density, avgDensity, avgVol)
      };
    }

    // **********************************************************************

    static string FormatMessage(ClusterStats s, bool selling, double density, double avgDensity)
    {
      string dir = selling ? "продаж" : "покупок";
      string react = selling ? "возможен откат вверх" : "возможен откат вниз";
      double densityRatio = avgDensity > 0 ? density / avgDensity : 0;

      return string.Format(CultureInfo.InvariantCulture,
        "Климакс {0}: {1}% объёма в зоне {2}-{3} (x{4:F1} плотности), COM {5} — {6}",
        dir,
        (int)Math.Round(s.Top3Share * 100),
        s.Top3From,
        s.Top3To,
        densityRatio,
        s.ComPrice,
        react);
    }

    static string FormatDetails(ClusterStats s, bool selling, double density, double avgDensity, double avgVol)
    {
      return string.Format(CultureInfo.InvariantCulture,
        "com={0} top3Share={1:F2} top3={2}-{3} vol={4} avgVol={5:F0} density={6:F1} avgDensity={7:F1} posCom={8:F2} shape={9}",
        s.ComPrice,
        s.Top3Share,
        s.Top3From, s.Top3To,
        s.Volume,
        avgVol,
        density,
        avgDensity,
        s.PosCom,
        s.Shape);
    }

    // **********************************************************************
  }

  // ==========================================================================
}
