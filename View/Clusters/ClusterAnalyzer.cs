// ======================================================================
//  ClusterAnalyzer.cs — Анализ паттернов поглощения объёма в кластерах
// ======================================================================

using System;
using System.Collections.Generic;
using System.Globalization;

namespace QScalp.View.ClustersSpace
{
  static class ClusterAnalyzer
  {
    // **********************************************************************

    public enum Signal { None, BearishDivergence, BullishDivergence }
    public enum ClimaxSignal { None, BearishClimax, BullishClimax }
    public enum RejectionSignal { None, ResistanceRejection, SupportRejection }

    /// <summary>Порог доли объёма для legacy-климакса (отдельно от поглощения).</summary>
    const double LegacyClimaxVolumeRatioThreshold = 0.6;

    /// <summary>Параметры 3-кластерного поглощения (из cfg.u.LegacyAbsorption*).</summary>
    public struct LegacyAbsorptionParams
    {
      public double VolumeRatioThreshold;
      public double VolumeMultiplier;

      public static LegacyAbsorptionParams Default
      {
        get
        {
          return new LegacyAbsorptionParams
          {
            VolumeRatioThreshold = 0.68,
            VolumeMultiplier = 1.35
          };
        }
      }
    }

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

    static readonly int[] TimeframeMultipliers = new int[] { 1, 2, 4 };

    // **********************************************************************

    /// <summary>
    /// Анализирует последние закрытые time-based кластеры на текущем и старших
    /// таймфреймах: base, base*2, base*4. Старшие бары собираются rolling-окном,
    /// которое заканчивается последним закрытым базовым кластером.
    /// </summary>
    public static IList<string> AnalyzeTimeframes(IList<Cluster> closedClusters, int baseSeconds)
    {
      return AnalyzeTimeframes(closedClusters, baseSeconds, LegacyAbsorptionParams.Default);
    }

    public static IList<string> AnalyzeTimeframes(
      IList<Cluster> closedClusters,
      int baseSeconds,
      LegacyAbsorptionParams absorption)
    {
      var messages = new List<string>();

      if(closedClusters == null || closedClusters.Count < 3 || baseSeconds <= 0)
        return messages;

      int maxRequired = 0;
      for(int i = 0; i < TimeframeMultipliers.Length; i++)
      {
        int required = TimeframeMultipliers[i] * 3;
        if(required > maxRequired)
          maxRequired = required;
      }

      int sourceStart = Math.Max(0, closedClusters.Count - maxRequired);
      var frames = new List<ClusterFrame>(closedClusters.Count - sourceStart);

      for(int i = sourceStart; i < closedClusters.Count; i++)
      {
        ClusterFrame f = ClusterFrame.FromCluster(closedClusters[i]);
        if(f == null)
          return messages;

        frames.Add(f);
      }

      var results = new List<TimeframeAnalysis>();

      for(int i = 0; i < TimeframeMultipliers.Length; i++)
      {
        TimeframeAnalysis result = AnalyzeTimeframe(frames, baseSeconds, TimeframeMultipliers[i], absorption);
        if(result != null)
          results.Add(result);
      }

      AddAbsorptionMessages(messages, results, Signal.BearishDivergence, absorption);
      AddAbsorptionMessages(messages, results, Signal.BullishDivergence, absorption);

      AddClimaxMessages(messages, results, ClimaxSignal.BearishClimax);
      AddClimaxMessages(messages, results, ClimaxSignal.BullishClimax);

      AddRejectionMessages(messages, results, RejectionSignal.ResistanceRejection);
      AddRejectionMessages(messages, results, RejectionSignal.SupportRejection);

      return messages;
    }

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
      return Analyze(c1, c2, c3, LegacyAbsorptionParams.Default);
    }

    public static Signal Analyze(Cluster c1, Cluster c2, Cluster c3, LegacyAbsorptionParams absorption)
    {
      ClusterFrame f1 = ClusterFrame.FromCluster(c1);
      ClusterFrame f2 = ClusterFrame.FromCluster(c2);
      ClusterFrame f3 = ClusterFrame.FromCluster(c3);

      if(f1 == null || f2 == null || f3 == null)
        return Signal.None;

      return Analyze(f1, f2, f3, absorption);
    }

    static Signal Analyze(ClusterFrame c1, ClusterFrame c2, ClusterFrame c3, LegacyAbsorptionParams absorption)
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

      if(c3.Volume < c2.Volume * absorption.VolumeMultiplier || c3.Volume <= c1.Volume)
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

      if(uptrend && (double)volumeAbove / distributed > absorption.VolumeRatioThreshold)
        return Signal.BearishDivergence;

      if(downtrend && (double)volumeBelow / distributed > absorption.VolumeRatioThreshold)
        return Signal.BullishDivergence;

      return Signal.None;
    }

    // **********************************************************************

    /// <summary>
    /// Формирует текстовое сообщение для пользователя по результату анализа поглощения.
    /// </summary>
    public static string FormatMessage(Signal signal, Cluster c3)
    {
      return FormatMessage(signal, null, null, c3, LegacyAbsorptionParams.Default);
    }

    public static string FormatMessage(Signal signal, Cluster c1, Cluster c2, Cluster c3)
    {
      return FormatMessage(signal, c1, c2, c3, LegacyAbsorptionParams.Default);
    }

    public static string FormatMessage(Signal signal, Cluster c1, Cluster c2, Cluster c3, LegacyAbsorptionParams absorption)
    {
      ClusterFrame f1 = ClusterFrame.FromCluster(c1);
      ClusterFrame f2 = ClusterFrame.FromCluster(c2);
      ClusterFrame f3 = ClusterFrame.FromCluster(c3);

      return f3 != null ? FormatMessage(signal, f1, f2, f3, absorption) : string.Empty;
    }

    static string FormatMessage(Signal signal, ClusterFrame c1, ClusterFrame c2, ClusterFrame c3, LegacyAbsorptionParams absorption)
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

      double volumeRatio = AbsorptionVolumeRatio(c1, c2, c3);
      double strength = AbsorptionStrength(volumeRatio, absorption.VolumeMultiplier);

      if(signal == Signal.BearishDivergence)
        return string.Format(CultureInfo.InvariantCulture,
          "Поглощение продавцами: тренд вверх, объём x{0:F1} к двум предыдущим, сила {1:F2}, {2}% объёма выше {3} ({4}) — возможен разворот вниз",
          volumeRatio, strength, pct, refLabel, FormatPrice(refPrice));

      return string.Format(CultureInfo.InvariantCulture,
        "Поглощение покупателями: тренд вниз, объём x{0:F1} к двум предыдущим, сила {1:F2}, {2}% объёма ниже {3} ({4}) — возможен разворот вверх",
        volumeRatio, strength, pct, refLabel, FormatPrice(refPrice));
    }

    // **********************************************************************

    static double AbsorptionVolumeRatio(ClusterFrame c1, ClusterFrame c2, ClusterFrame c3)
    {
      if(c1 == null || c2 == null || c3 == null)
        return 0;

      long prevMax = c1.Volume > c2.Volume ? c1.Volume : c2.Volume;
      return prevMax > 0 ? c3.Volume / (double)prevMax : 0;
    }

    static double AbsorptionStrength(double volumeRatio, double volumeMultiplier)
    {
      if(volumeRatio <= 0)
        return 0;

      const double FullStrengthVolumeRatio = 3.0;
      double strength = 0.50
        + (volumeRatio - volumeMultiplier)
          / (FullStrengthVolumeRatio - volumeMultiplier) * 0.50;

      if(strength < 0) return 0;
      if(strength > 1) return 1;
      return strength;
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
      ClusterFrame f1 = ClusterFrame.FromCluster(c1);
      ClusterFrame f2 = ClusterFrame.FromCluster(c2);
      ClusterFrame f3 = ClusterFrame.FromCluster(c3);

      if(f1 == null || f2 == null || f3 == null)
        return ClimaxSignal.None;

      return AnalyzeClimax(f1, f2, f3);
    }

    static ClimaxSignal AnalyzeClimax(ClusterFrame c1, ClusterFrame c2, ClusterFrame c3)
    {
      if(c1.Volume == 0 || c2.Volume == 0 || c3.Volume == 0)
        return ClimaxSignal.None;

      long maxPrevVolume = c1.Volume > c2.Volume ? c1.Volume : c2.Volume;
      if(c3.Volume < maxPrevVolume * VolumeClimaxMultiplier)
        return ClimaxSignal.None;

      long volumeAbove, volumeBelow;
      c3.GetVolumeDistribution(out volumeAbove, out volumeBelow);

      long distributed = volumeAbove + volumeBelow;
      if(distributed == 0)
        return ClimaxSignal.None;

      double ratioBelow = (double)volumeBelow / distributed;
      double ratioAbove = (double)volumeAbove / distributed;

      if(c3.ClosePrice < c3.OpenPrice && ratioBelow > LegacyClimaxVolumeRatioThreshold)
        return ClimaxSignal.BearishClimax;

      if(c3.ClosePrice > c3.OpenPrice && ratioAbove > LegacyClimaxVolumeRatioThreshold)
        return ClimaxSignal.BullishClimax;

      return ClimaxSignal.None;
    }

    // **********************************************************************

    /// <summary>
    /// Формирует текстовое сообщение для пользователя по результату анализа кульминации.
    /// </summary>
    public static string FormatClimaxMessage(ClimaxSignal signal, Cluster c1, Cluster c2, Cluster c3)
    {
      ClusterFrame f1 = ClusterFrame.FromCluster(c1);
      ClusterFrame f2 = ClusterFrame.FromCluster(c2);
      ClusterFrame f3 = ClusterFrame.FromCluster(c3);

      if(f1 == null || f2 == null || f3 == null)
        return string.Empty;

      return FormatClimaxMessage(signal, f1, f2, f3);
    }

    static string FormatClimaxMessage(ClimaxSignal signal, ClusterFrame c1, ClusterFrame c2, ClusterFrame c3)
    {
      long maxPrevVolume = c1.Volume > c2.Volume ? c1.Volume : c2.Volume;
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
        direction, volRatio, c3.Volume, pct, side, FormatPrice(c3.ClosePrice));
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
      ClusterFrame f1 = ClusterFrame.FromCluster(c1);
      ClusterFrame f2 = ClusterFrame.FromCluster(c2);
      ClusterFrame f3 = ClusterFrame.FromCluster(c3);

      if(f1 == null || f2 == null || f3 == null)
        return RejectionSignal.None;

      return AnalyzeRejection(f1, f2, f3);
    }

    static RejectionSignal AnalyzeRejection(ClusterFrame c1, ClusterFrame c2, ClusterFrame c3)
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
      ClusterFrame f1 = ClusterFrame.FromCluster(c1);
      ClusterFrame f2 = ClusterFrame.FromCluster(c2);
      ClusterFrame f3 = ClusterFrame.FromCluster(c3);

      if(f1 == null || f2 == null || f3 == null)
        return string.Empty;

      return FormatRejectionMessage(signal, f1, f2, f3);
    }

    static string FormatRejectionMessage(RejectionSignal signal, ClusterFrame c1, ClusterFrame c2, ClusterFrame c3)
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
        type, FormatPrice(level), pct, volAtLevel, touches, FormatPrice(c3.ClosePrice));
    }

    // **********************************************************************

    static string FormatPrice(int price)
    {
      return Price.GetString(price);
    }

    // **********************************************************************

    static TimeframeAnalysis AnalyzeTimeframe(
      IList<ClusterFrame> frames,
      int baseSeconds,
      int multiplier,
      LegacyAbsorptionParams absorption)
    {
      int required = multiplier * 3;
      if(frames.Count < required)
        return null;

      int start = frames.Count - required;
      if(!IsContiguous(frames, start, frames.Count, baseSeconds))
        return null;

      ClusterFrame c1 = ClusterFrame.Merge(frames, start, multiplier);
      ClusterFrame c2 = ClusterFrame.Merge(frames, start + multiplier, multiplier);
      ClusterFrame c3 = ClusterFrame.Merge(frames, start + multiplier * 2, multiplier);

      if(c1 == null || c2 == null || c3 == null)
        return null;

      return new TimeframeAnalysis
      {
        Seconds = baseSeconds * multiplier,
        C1 = c1,
        C2 = c2,
        C3 = c3,
        Absorption = Analyze(c1, c2, c3, absorption),
        Climax = AnalyzeClimax(c1, c2, c3),
        Rejection = AnalyzeRejection(c1, c2, c3)
      };
    }

    static bool IsContiguous(IList<ClusterFrame> frames, int start, int end, int baseSeconds)
    {
      long expectedTicks = baseSeconds * TimeSpan.TicksPerSecond;

      for(int i = start + 1; i < end; i++)
      {
        if(frames[i].DateTime.Ticks - frames[i - 1].DateTime.Ticks != expectedTicks)
          return false;
      }

      return true;
    }

    static void AddAbsorptionMessages(
      List<string> messages,
      IList<TimeframeAnalysis> results,
      Signal signal,
      LegacyAbsorptionParams absorption)
    {
      var seconds = new List<int>();
      TimeframeAnalysis sample = null;

      for(int i = 0; i < results.Count; i++)
        if(results[i].Absorption == signal)
        {
          seconds.Add(results[i].Seconds);
          if(sample == null)
            sample = results[i];
        }

      if(seconds.Count > 0 && sample != null)
        messages.Add(AppendTimeframes(FormatMessage(signal, sample.C1, sample.C2, sample.C3, absorption), seconds));
    }

    static void AddClimaxMessages(List<string> messages, IList<TimeframeAnalysis> results, ClimaxSignal signal)
    {
      var seconds = new List<int>();
      TimeframeAnalysis sample = null;

      for(int i = 0; i < results.Count; i++)
        if(results[i].Climax == signal)
        {
          seconds.Add(results[i].Seconds);
          if(sample == null)
            sample = results[i];
        }

      if(seconds.Count > 0 && sample != null)
        messages.Add(AppendTimeframes(FormatClimaxMessage(signal, sample.C1, sample.C2, sample.C3), seconds));
    }

    static void AddRejectionMessages(List<string> messages, IList<TimeframeAnalysis> results, RejectionSignal signal)
    {
      var seconds = new List<int>();
      TimeframeAnalysis sample = null;

      for(int i = 0; i < results.Count; i++)
        if(results[i].Rejection == signal)
        {
          seconds.Add(results[i].Seconds);
          if(sample == null)
            sample = results[i];
        }

      if(seconds.Count > 0 && sample != null)
        messages.Add(AppendTimeframes(FormatRejectionMessage(signal, sample.C1, sample.C2, sample.C3), seconds));
    }

    static string AppendTimeframes(string message, IList<int> seconds)
    {
      if(string.IsNullOrEmpty(message))
        return message;

      return message + ". Сигнал есть на ТФ: " + FormatTimeframes(seconds);
    }

    static string FormatTimeframes(IList<int> seconds)
    {
      var labels = new List<string>();
      for(int i = 0; i < seconds.Count; i++)
        labels.Add(FormatTimeframe(seconds[i]));

      return string.Join(", ", labels.ToArray());
    }

    static string FormatTimeframe(int seconds)
    {
      if(seconds >= 3600 && seconds % 3600 == 0)
        return (seconds / 3600).ToString() + "ч";

      if(seconds >= 60 && seconds % 60 == 0)
        return (seconds / 60).ToString() + "м";

      return seconds.ToString() + "с";
    }

    // **********************************************************************

    sealed class TimeframeAnalysis
    {
      public int Seconds;
      public ClusterFrame C1;
      public ClusterFrame C2;
      public ClusterFrame C3;
      public Signal Absorption;
      public ClimaxSignal Climax;
      public RejectionSignal Rejection;
    }

    // **********************************************************************

    sealed class ClusterFrame
    {
      public DateTime DateTime;
      public long Volume;
      public long BuyVolume;
      public long SellVolume;
      public long NeutralVolume;
      public int OpenPrice;
      public int ClosePrice;
      public int MinPrice;
      public int MaxPrice;

      public long AggressiveVolume { get { return BuyVolume + SellVolume; } }
      public long Delta { get { return BuyVolume - SellVolume; } }

      public double InsideShare
      {
        get { return Volume > 0 ? NeutralVolume / (double)Volume : 0; }
      }

      public double AggressiveShare
      {
        get { return Volume > 0 ? AggressiveVolume / (double)Volume : 0; }
      }

      public double DeltaShare
      {
        get { return Volume > 0 ? Delta / (double)Volume : 0; }
      }

      public double AggressiveDeltaShare
      {
        get { return AggressiveVolume > 0 ? Delta / (double)AggressiveVolume : 0; }
      }

      readonly Dictionary<int, ClusterCell> cells = new Dictionary<int, ClusterCell>();

      public static ClusterFrame FromCluster(Cluster cluster)
      {
        if(cluster == null || cluster.Volume == 0)
          return null;

        if(cluster.MinPrice == int.MaxValue || cluster.MaxPrice < cluster.MinPrice)
          return null;

        var frame = new ClusterFrame
        {
          DateTime = cluster.DateTime,
          Volume = cluster.Volume,
          OpenPrice = cluster.OpenPrice,
          ClosePrice = cluster.ClosePrice,
          MinPrice = cluster.MinPrice,
          MaxPrice = cluster.MaxPrice
        };

        IList<ClusterPriceLevel> levels = cluster.GetPriceLevels();
        for(int i = 0; i < levels.Count; i++)
        {
          ClusterPriceLevel level = levels[i];
          if(level.Total > 0)
            frame.AddCell(level.Price, level.AskVolume, level.BidVolume,
              level.InsideSpreadVolume);
        }

        return frame;
      }

      public static ClusterFrame Merge(IList<ClusterFrame> frames, int start, int count)
      {
        if(frames == null || count <= 0 || start < 0 || start + count > frames.Count)
          return null;

        ClusterFrame first = frames[start];
        ClusterFrame last = frames[start + count - 1];
        if(first == null || last == null)
          return null;

        var merged = new ClusterFrame
        {
          DateTime = last.DateTime,
          OpenPrice = first.OpenPrice,
          ClosePrice = last.ClosePrice,
          MinPrice = int.MaxValue,
          MaxPrice = int.MinValue
        };

        for(int i = start; i < start + count; i++)
        {
          ClusterFrame f = frames[i];
          if(f == null)
            return null;

          merged.Volume += f.Volume;

          if(f.MinPrice < merged.MinPrice)
            merged.MinPrice = f.MinPrice;

          if(f.MaxPrice > merged.MaxPrice)
            merged.MaxPrice = f.MaxPrice;

          foreach(KeyValuePair<int, ClusterCell> kv in f.cells)
            merged.AddCell(kv.Key, kv.Value.BuyVolume, kv.Value.SellVolume,
              kv.Value.NeutralVolume);
        }

        return merged.Volume > 0 ? merged : null;
      }

      public long GetCellVolume(int price)
      {
        ClusterCell cell;
        return cells.TryGetValue(price, out cell) ? cell.Total : 0;
      }

      public ClusterCell GetCell(int price)
      {
        ClusterCell cell;
        return cells.TryGetValue(price, out cell) ? cell : new ClusterCell();
      }

      public void GetVolumeDistribution(out long volumeAbove, out long volumeBelow)
      {
        GetVolumeDistribution(ClosePrice, out volumeAbove, out volumeBelow);
      }

      public void GetVolumeDistribution(int referencePrice, out long volumeAbove, out long volumeBelow)
      {
        volumeAbove = 0;
        volumeBelow = 0;

        foreach(KeyValuePair<int, ClusterCell> kv in cells)
        {
          if(kv.Key > referencePrice)
            volumeAbove += kv.Value.Total;
          else if(kv.Key < referencePrice)
            volumeBelow += kv.Value.Total;
        }
      }

      void AddCell(int price, long buyVolume, long sellVolume, long neutralVolume)
      {
        ClusterCell current;
        cells.TryGetValue(price, out current);
        current.BuyVolume += buyVolume;
        current.SellVolume += sellVolume;
        current.NeutralVolume += neutralVolume;
        cells[price] = current;

        BuyVolume += buyVolume;
        SellVolume += sellVolume;
        NeutralVolume += neutralVolume;
      }
    }

    // **********************************************************************

    struct ClusterCell
    {
      public long BuyVolume;
      public long SellVolume;
      public long NeutralVolume;

      public long Total
      {
        get { return BuyVolume + SellVolume + NeutralVolume; }
      }

      public long Delta
      {
        get { return BuyVolume - SellVolume; }
      }

      public double InsideShare
      {
        get { return Total > 0 ? NeutralVolume / (double)Total : 0; }
      }
    }

    // **********************************************************************
  }
}
