// ==========================================================================
//  OrphanCloseDetector.cs — «Сиротское» закрытие далеко от области торговли
// ==========================================================================
//
//  Идея паттерна:
//    На капитуляционных движениях / failed breakout'ах цена закрывается
//    далеко за пределами зоны, где реально шла торговля внутри бара.
//    Top3 (трёх соседних доминирующих уровней) и POC остаются «осиротевшими»
//    высоко (для медвежьего бара) или низко (для бычьего шипа), а close
//    «улетел» в противоположный конец бара. Статистически такие гэпы между
//    close и POC внутри одного кластера заполняются обратно к POC хотя бы
//    частично — там было распределено реальное предложение/спрос, а на
//    самом close уже почти никто не торговал.
//
//  Реальный пример (NVDA 13:51, дневной low):
//    high=20934 low=20851 close=20854 range=83
//    POC=20900 (доля 5.5%) — POC в верхней половине бара (pos_poc=0.59)
//    Top3From..Top3To = 20859..20900
//    close=20854 НИЖЕ Top3From на 5 тиков (вне зоны Top3)
//    |close - POC| = 46 тиков = 55% range — большой gap
//    Lower wick = 3 тика — на самом low никто не подбирал
//  Через 2 бара цена вернулась к 20937 (POC + 37) — gap отыгран.
//
//  Формальные признаки:
//    • Range последнего бара ≥ MinRangeTicks (отсев мелочи).
//    • |close - POC| / range ≥ MinGapShare (≈ 0.45..0.55).
//    • close ВНЕ диапазона Top3From..Top3To (хотя бы на 1 priceStep).
//    • Volume последнего бара ≥ MinVolumeRatio * avg по окну (отсев
//      «тонких» баров без участников — на низкой ликвидности возврат
//      к POC не гарантирован).
//
//  Направление сигнала — ожидаемая сторона возврата:
//    close НИЖЕ POC → close улетел вниз → ожидаем UP-отскок к POC.
//    close ВЫШЕ POC → close улетел вверх → ожидаем DOWN-возврат к POC.
//
//  Strength:
//    50% — относительный gap (gap_share / 1.0, cap = 1).
//    30% — фитиль в сторону close: для UP сигнала бычьим подтверждением
//          служит МАЛЕНЬКИЙ нижний фитиль (на самом low не торговали),
//          для DOWN — маленький верхний.
//    20% — превышение объёма над средним.
//
//  Гистерезис:
//    После срабатывания CooldownBars баров молчим. На последовательных
//    «сиротских» барах (часто бывает на 2–3 капитуляциях подряд) выдаём
//    только первый сигнал.
//
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.View.ClustersSpace.Analytics.Detectors
{
  // ==========================================================================

  sealed class OrphanCloseDetector : ISignalDetector
  {
    // **********************************************************************

    public string Name { get { return "OrphanClose"; } }
    public bool Enabled { get; set; }

    // --- параметры --------------------------------------------------------

    /// <summary>Минимальный range последнего бара (priceStep).</summary>
    public int    MinRangeTicks      { get; set; }

    /// <summary>Минимальная доля |close − POC| / range. 0.45 = close в дальней четверти бара от POC.</summary>
    public double MinGapShare        { get; set; }

    /// <summary>Окно для среднего объёма (для проверки ликвидности).</summary>
    public int    AverageWindow      { get; set; }

    /// <summary>Минимальное отношение Volume / avg для «жирного» бара.</summary>
    public double MinVolumeRatio     { get; set; }

    /// <summary>Минимальная итоговая strength для эмиссии.</summary>
    public double MinStrength        { get; set; }

    /// <summary>Кулдаун в барах после сигнала.</summary>
    public int    CooldownBars       { get; set; }

    // --- состояние --------------------------------------------------------

    DateTime lastEmittedAt = DateTime.MinValue;
    int      barsSinceLastEmit = int.MaxValue;

    // **********************************************************************

    public OrphanCloseDetector()
    {
      Enabled         = true;
      MinRangeTicks   = 10;
      MinGapShare     = 0.45;
      AverageWindow   = 10;
      MinVolumeRatio  = 0.80;
      MinStrength     = 0.50;
      CooldownBars    = 3;
    }

    // **********************************************************************

    public Signal Evaluate(ClusterHistory history)
    {
      if(history == null) return null;

      if(barsSinceLastEmit != int.MaxValue) barsSinceLastEmit++;
      if(barsSinceLastEmit < CooldownBars) return null;

      var last = history.Last(0);
      if(last == null) return null;
      if(last.PriceStep <= 0) return null;
      if(last.Volume <= 0) return null;

      int range = last.MaxPrice - last.MinPrice;
      if(range < MinRangeTicks) return null;

      // Gap между close и POC.
      int gap = last.ClosePrice - last.PocPrice;
      double gapShare = Math.Abs((double)gap) / range;
      if(gapShare < MinGapShare) return null;

      bool closeBelowPoc = gap < 0;
      bool closeAbovePoc = gap > 0;
      if(!closeBelowPoc && !closeAbovePoc) return null;

      // Close должен быть ВНЕ диапазона Top3From..Top3To.
      bool outsideTop3;
      if(closeBelowPoc)
        outsideTop3 = last.ClosePrice < last.Top3From;
      else
        outsideTop3 = last.ClosePrice > last.Top3To;

      if(!outsideTop3) return null;

      // Ликвидность.
      double avgVol = history.AverageVolumeBefore(AverageWindow);
      if(avgVol > 0 && last.Volume < avgVol * MinVolumeRatio) return null;

      // === Strength ===
      double gapScore = Math.Min(1.0, gapShare);

      // Фитиль на стороне close: для UP-сигнала (close внизу) меньший
      // нижний фитиль = сильнее (на дне никто не торговал).
      int wickAtClose = closeBelowPoc
        ? Math.Max(0, Math.Min(last.OpenPrice, last.ClosePrice) - last.MinPrice)
        : Math.Max(0, last.MaxPrice - Math.Max(last.OpenPrice, last.ClosePrice));

      double wickShare = (double)wickAtClose / range;
      double wickScore = Math.Max(0, 1.0 - wickShare * 4.0); // фитиль 25% и больше → 0

      double volScore = avgVol > 0
        ? Math.Min(1.0, ((double)last.Volume / avgVol - MinVolumeRatio) /
                         Math.Max(1e-6, 1.0 - MinVolumeRatio))
        : 0.3;

      double strength = 0.50 * gapScore + 0.30 * wickScore + 0.20 * Math.Max(0, volScore);
      if(strength < MinStrength) return null;

      if(last.Source.DateTime == lastEmittedAt) return null;
      lastEmittedAt = last.Source.DateTime;
      barsSinceLastEmit = 0;

      var dir  = closeBelowPoc ? SignalDirection.Up : SignalDirection.Down;
      var kind = closeBelowPoc ? SignalKind.OrphanCloseUp : SignalKind.OrphanCloseDown;

      return new Signal
      {
        Time      = last.Source.DateTime,
        Kind      = kind,
        Direction = dir,
        Price     = last.PocPrice, // ожидаемая цель возврата
        Strength  = strength,
        Message   = FormatMessage(last, gap, gapShare, closeBelowPoc),
        Details   = FormatDetails(last, gap, gapShare, wickShare, avgVol, strength)
      };
    }

    // **********************************************************************

    static string FormatMessage(ClusterStats last, int gap, double gapShare, bool closeBelowPoc)
    {
      string side = closeBelowPoc ? "вверх" : "вниз";
      int gapPct = (int)Math.Round(gapShare * 100);

      return string.Format(CultureInfo.InvariantCulture,
        "Сиротское закрытие: close {0} в {1} тиках от POC {2} ({3}% диапазона) — ожидание возврата {4}",
        last.ClosePrice,
        Math.Abs(gap),
        last.PocPrice,
        gapPct,
        side);
    }

    static string FormatDetails(ClusterStats last, int gap, double gapShare,
                                double wickShare, double avgVol, double strength)
    {
      return string.Format(CultureInfo.InvariantCulture,
        "close={0} poc={1} gap={2} gapShare={3:F2} top3=[{4}..{5}] posPoc={6:F2} range={7} wickShareAtClose={8:F2} vol={9} avgVol={10:F0} strength={11:F2}",
        last.ClosePrice,
        last.PocPrice,
        gap,
        gapShare,
        last.Top3From, last.Top3To,
        last.PosPoc,
        last.MaxPrice - last.MinPrice,
        wickShare,
        last.Volume,
        avgVol,
        strength);
    }

    // **********************************************************************
  }

  // ==========================================================================
}
