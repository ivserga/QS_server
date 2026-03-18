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

    /// <summary>
    /// Порог доли объёма (выше/ниже закрытия) для срабатывания сигнала поглощения.
    /// При значении 0.6 сигнал выдаётся, если >= 60% объёма последнего кластера
    /// расположено по противоположную от тренда сторону от цены закрытия.
    /// </summary>
    const double VolumeRatioThreshold = 0.6;

    /// <summary>
    /// Множитель объёма для определения кульминации.
    /// Объём c3 должен быть >= VolumeClimaxMultiplier * max(c1, c2).
    /// </summary>
    const double VolumeClimaxMultiplier = 3.0;

    

    // **********************************************************************

    /// <summary>
    /// Анализирует три последовательных завершённых кластера на паттерн поглощения:
    /// 
    /// BearishDivergence — восходящий тренд, объём растёт, основной объём выше закрытия
    ///                     (продавцы поглощают — возможен разворот вниз).
    ///
    /// BullishDivergence — нисходящий тренд, объём растёт, основной объём ниже закрытия
    ///                     (покупатели поглощают — возможен разворот вверх).
    ///
    /// Тренд определяется по первым двум кластерам (c1 → c2). Третий кластер — кластер
    /// поглощения, его закрытие может отскочить (это часть паттерна), но не должно
    /// полностью развернуть тренд (c3 остаётся по ту же сторону от c1).
    /// </summary>
    public static Signal Analyze(Cluster c1, Cluster c2, Cluster c3)
    {
      if(c1.Volume == 0 || c2.Volume == 0 || c3.Volume == 0)
        return Signal.None;

      bool uptrend = c1.ClosePrice < c2.ClosePrice && c3.ClosePrice > c1.ClosePrice;
      bool downtrend = c1.ClosePrice > c2.ClosePrice && c3.ClosePrice < c1.ClosePrice;

      if(!uptrend && !downtrend)
        return Signal.None;

      if(c3.Volume <= c1.Volume || c3.Volume <= c2.Volume)
        return Signal.None;

      long volumeAbove, volumeBelow;
      c3.GetVolumeDistribution(out volumeAbove, out volumeBelow);

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
      long volumeAbove, volumeBelow;
      c3.GetVolumeDistribution(out volumeAbove, out volumeBelow);

      long distributed = volumeAbove + volumeBelow;
      int pct = distributed > 0
        ? (int)(100.0 * (signal == Signal.BearishDivergence ? volumeAbove : volumeBelow) / distributed)
        : 0;

      if(signal == Signal.BearishDivergence)
        return string.Format(
          "Поглощение продавцами: тренд вверх, объём растёт, {0}% объёма выше закрытия ({1}) — возможен разворот вниз",
          pct, c3.ClosePrice);

      return string.Format(
        "Поглощение покупателями: тренд вниз, объём растёт, {0}% объёма ниже закрытия ({1}) — возможен разворот вверх",
        pct, c3.ClosePrice);
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
  }
}
