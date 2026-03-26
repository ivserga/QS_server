// ======================================================================
//  ClusterAnalyzer.cs — Анализ паттернов поглощения объёма в кластерах
// ======================================================================

namespace QScalp.View.ClustersSpace
{
  static class ClusterAnalyzer
  {
    // **********************************************************************

    public enum Signal { None, BearishDivergence, BullishDivergence }
    public enum ClimaxSignal { None, BearishClimax, BullishClimax }
    public enum RejectionSignal { None, ResistanceRejection, SupportRejection }

    /// <summary>
    /// Порог доли объёма (выше/ниже ориентирной цены) для срабатывания сигнала поглощения.
    /// При значении 0.6 сигнал выдаётся, если >= 60% объёма последнего кластера
    /// расположено по противоположную от тренда сторону от ориентирной цены.
    /// </summary>
    const double VolumeRatioThreshold = 0.6;

    /// <summary>
    /// Минимальное превышение объёма c3 над c2 для паттерна поглощения (1.2 = +20%).
    /// </summary>
    const double AbsorptionVolumeMultiplier = 1.2;

    /// <summary>
    /// Множитель объёма для определения кульминации.
    /// Объём c3 должен быть >= VolumeClimaxMultiplier * max(c1, c2).
    /// </summary>
    const double VolumeClimaxMultiplier = 3.0;

    /// <summary>
    /// Порог доли объёма одной ячейки (на уровне max/min) от общего объёма кластера
    /// для определения уровня отторжения. 0.10 = 10%.
    /// </summary>
    const double RejectionCellRatioThreshold = 0.10;
    const int RejectionMinTouches = 2;

    

    // **********************************************************************

    /// <summary>
    /// Анализирует три последовательных завершённых кластера на паттерн поглощения:
    /// 
    /// BearishDivergence — восходящий тренд, объём растёт, основной объём выше ориентирной цены
    ///                     (продавцы поглощают — возможен разворот вниз).
    ///
    /// BullishDivergence — нисходящий тренд, объём растёт, основной объём ниже ориентирной цены
    ///                     (покупатели поглощают — возможен разворот вверх).
    ///
    /// Тренд определяется по первым двум кластерам (c1 → c2), причём c1 и c2 должны
    /// иметь одинаковое направление (оба вверх или оба вниз). Объём c3 должен быть
    /// минимум на 20% больше, чем у c2 и просто больше, чем у c1.
    /// Если c3 развернулся (close против тренда), распределение объёма проверяется
    /// относительно цены открытия c3; если c3 продолжил тренд — относительно закрытия.
    /// </summary>
    public static Signal Analyze(Cluster c1, Cluster c2, Cluster c3)
    {
      if(c1.Volume == 0 || c2.Volume == 0 || c3.Volume == 0)
        return Signal.None;

      bool c1Up = c1.ClosePrice > c1.OpenPrice;
      bool c2Up = c2.ClosePrice > c2.OpenPrice;

      if(c1Up != c2Up)
        return Signal.None;

      bool uptrend = c1.ClosePrice < c2.ClosePrice && c3.ClosePrice > c1.ClosePrice;
      bool downtrend = c1.ClosePrice > c2.ClosePrice && c3.ClosePrice < c1.ClosePrice;

      if(!uptrend && !downtrend)
        return Signal.None;

      if(c3.Volume < c2.Volume * AbsorptionVolumeMultiplier || c3.Volume <= c1.Volume)
        return Signal.None;

      bool c3Reversed = uptrend
        ? c3.ClosePrice < c3.OpenPrice
        : c3.ClosePrice > c3.OpenPrice;

      int refPrice = c3Reversed ? c3.OpenPrice : c3.ClosePrice;

      long volumeAbove, volumeBelow;
      c3.GetVolumeDistribution(refPrice, out volumeAbove, out volumeBelow);

      long distributed = volumeAbove + volumeBelow;
      if(distributed == 0)
        return Signal.None;

      if(uptrend && (double)volumeAbove / distributed > VolumeRatioThreshold)
        return Signal.BearishDivergence;

      if(downtrend && (double)volumeBelow / distributed > VolumeRatioThreshold)
        return Signal.BullishDivergence;

      return Signal.None;
    }

    // **********************************************************************

    /// <summary>
    /// Формирует текстовое сообщение для пользователя по результату анализа поглощения.
    /// </summary>
    public static string FormatMessage(Signal signal, Cluster c3)
    {
      bool c3Reversed = signal == Signal.BearishDivergence
        ? c3.ClosePrice < c3.OpenPrice
        : c3.ClosePrice > c3.OpenPrice;

      int refPrice = c3Reversed ? c3.OpenPrice : c3.ClosePrice;
      string refLabel = c3Reversed ? "открытия" : "закрытия";

      long volumeAbove, volumeBelow;
      c3.GetVolumeDistribution(refPrice, out volumeAbove, out volumeBelow);

      long distributed = volumeAbove + volumeBelow;
      int pct = distributed > 0
        ? (int)(100.0 * (signal == Signal.BearishDivergence ? volumeAbove : volumeBelow) / distributed)
        : 0;

      if(signal == Signal.BearishDivergence)
        return string.Format(
          "Поглощение продавцами: тренд вверх, объём +20%, {0}% объёма выше {1} ({2}) — возможен разворот вниз",
          pct, refLabel, refPrice);

      return string.Format(
        "Поглощение покупателями: тренд вниз, объём +20%, {0}% объёма ниже {1} ({2}) — возможен разворот вверх",
        pct, refLabel, refPrice);
    }

    // **********************************************************************

    /// <summary>
    /// Определяет кульминационный выброс объёма (Volume Climax):
    /// резкий всплеск объёма на третьем кластере по сравнению с двумя предыдущими,
    /// с концентрацией объёма против направления движения.
    /// Сигнализирует о возможном развороте после кульминационного движения.
    ///
    /// BearishClimax — выброс вниз (close &lt; open), основной объём ниже закрытия.
    /// BullishClimax — выброс вверх (close &gt; open), основной объём выше закрытия.
    /// </summary>
    public static ClimaxSignal AnalyzeClimax(Cluster c1, Cluster c2, Cluster c3)
    {
      if(c1.Volume == 0 || c2.Volume == 0 || c3.Volume == 0)
        return ClimaxSignal.None;

      int maxPrevVolume = c1.Volume > c2.Volume ? c1.Volume : c2.Volume;
      if(c3.Volume < maxPrevVolume * VolumeClimaxMultiplier)
        return ClimaxSignal.None;

      long volumeAbove, volumeBelow;
      c3.GetVolumeDistribution(out volumeAbove, out volumeBelow);

      long distributed = volumeAbove + volumeBelow;
      if(distributed == 0)
        return ClimaxSignal.None;

      double ratioBelow = (double)volumeBelow / distributed;
      double ratioAbove = (double)volumeAbove / distributed;

      if(c3.ClosePrice < c3.OpenPrice && ratioBelow > VolumeRatioThreshold)
        return ClimaxSignal.BearishClimax;

      if(c3.ClosePrice > c3.OpenPrice && ratioAbove > VolumeRatioThreshold)
        return ClimaxSignal.BullishClimax;

      return ClimaxSignal.None;
    }

    // **********************************************************************

    /// <summary>
    /// Формирует текстовое сообщение для пользователя по результату анализа кульминации.
    /// </summary>
    public static string FormatClimaxMessage(ClimaxSignal signal, Cluster c1, Cluster c2, Cluster c3)
    {
      int maxPrevVolume = c1.Volume > c2.Volume ? c1.Volume : c2.Volume;
      double volRatio = (double)c3.Volume / maxPrevVolume;

      long volumeAbove, volumeBelow;
      c3.GetVolumeDistribution(out volumeAbove, out volumeBelow);
      long distributed = volumeAbove + volumeBelow;

      bool bearish = signal == ClimaxSignal.BearishClimax;
      int pct = distributed > 0
        ? (int)(100.0 * (bearish ? volumeBelow : volumeAbove) / distributed)
        : 0;

      string direction = bearish ? "вниз" : "вверх";
      string side = bearish ? "ниже" : "выше";

      return string.Format(
        "Объёмный выброс {0}: объём x{1:F1} ({2}), {3}% объёма {4} закрытия ({5}) — возможна кульминация и разворот",
        direction, volRatio, c3.Volume, pct, side, c3.ClosePrice);
    }

    // **********************************************************************

    /// <summary>
    /// Определяет отторжение ценового уровня (Price Level Rejection):
    /// на крайней цене кластера (maxPrice или minPrice) сконцентрирован аномально
    /// большой объём, цена закрытия ушла от этого уровня — уровень выступил
    /// как сопротивление/поддержка.
    ///
    /// ResistanceRejection — отторжение сверху (стена на maxPrice, close ниже).
    /// SupportRejection    — отторжение снизу (стена на minPrice, close выше).
    /// </summary>
    public static RejectionSignal AnalyzeRejection(Cluster c1, Cluster c2, Cluster c3)
    {
      if(c3.Volume == 0)
        return RejectionSignal.None;

      long volAtMax = c3.GetCellVolume(c3.MaxPrice);
      long volAtMin = c3.GetCellVolume(c3.MinPrice);

      double ratioMax = (double)volAtMax / c3.Volume;
      double ratioMin = (double)volAtMin / c3.Volume;

      int resistanceTouches = 1;
      if(c1.MaxPrice == c3.MaxPrice) resistanceTouches++;
      if(c2.MaxPrice == c3.MaxPrice) resistanceTouches++;

      int supportTouches = 1;
      if(c1.MinPrice == c3.MinPrice) supportTouches++;
      if(c2.MinPrice == c3.MinPrice) supportTouches++;

      if(ratioMax >= RejectionCellRatioThreshold
        && c3.ClosePrice < c3.MaxPrice
        && resistanceTouches >= RejectionMinTouches)
        return RejectionSignal.ResistanceRejection;

      if(ratioMin >= RejectionCellRatioThreshold
        && c3.ClosePrice > c3.MinPrice
        && supportTouches >= RejectionMinTouches)
        return RejectionSignal.SupportRejection;

      return RejectionSignal.None;
    }

    // **********************************************************************

    /// <summary>
    /// Формирует текстовое сообщение для пользователя по результату анализа отторжения.
    /// </summary>
    public static string FormatRejectionMessage(RejectionSignal signal, Cluster c1, Cluster c2, Cluster c3)
    {
      bool resistance = signal == RejectionSignal.ResistanceRejection;
      int level = resistance ? c3.MaxPrice : c3.MinPrice;
      long volAtLevel = c3.GetCellVolume(level);
      int pct = c3.Volume > 0 ? (int)(100.0 * volAtLevel / c3.Volume) : 0;

      int touches = 1;
      if((resistance ? c1.MaxPrice : c1.MinPrice) == level) touches++;
      if((resistance ? c2.MaxPrice : c2.MinPrice) == level) touches++;

      string type = resistance ? "Сопротивление" : "Поддержка";

      return string.Format(
        "{0} на {1}: {2}% объёма ({3}) на уровне, касаний: {4}, закрытие {5}",
        type, level, pct, volAtLevel, touches, c3.ClosePrice);
    }

    // **********************************************************************
  }
}
