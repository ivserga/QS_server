// ==========================================================================
//  HvnRejectDetector.cs — Отскок цены от High Volume Node прошлой истории
// ==========================================================================
//
//  Что это такое:
//    HVN (High Volume Node) — ценовой уровень, на котором за последние N
//    минут прошло непропорционально много объёма. Такие уровни часто
//    работают как поддержка/сопротивление в рамках той же сессии: цена
//    подходит к ним и "отскакивает" обратно.
//
//  Ситуация из датасета NVDA 20260422:
//    13:57–14:02 — двухполюсный баланс, HVN около 20130 работал защитной
//    поддержкой: каждый тест снизу закрывался выше.
//
//  Алгоритм:
//    • Агрегируем объём по ценам за последние LookbackBars баров
//      (без текущего).
//    • Находим цену с максимальным агрегированным объёмом (HVN).
//    • Проверяем "значимость": HVN должен собрать ≥ MinHvnShare от
//      суммы объёмов всех уровней за окно.
//    • Проверяем что текущий бар ТЕСТИРУЕТ HVN и ОТКАЗАЛСЯ его пробивать:
//        - support: prevClose > HVN, MinPrice ≤ HVN, Close ≥ HVN + MinReclaimTicks
//        - resistance: prevClose < HVN, MaxPrice ≥ HVN, Close ≤ HVN − MinReclaimTicks
//    • Гистерезис: не эмитить на том же баре, и CooldownBars между
//      сигналами любой направленности.
//
//  Сила:
//    •  50% — "вес" HVN (его доля в агрегированном профиле / MinHvnShare).
//    •  30% — насколько close отошёл от HVN в нужную сторону.
//    •  20% — "сухость" хвоста в текущем баре за HVN.
//
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Globalization;

namespace QScalp.View.ClustersSpace.Analytics.Detectors
{
  // ==========================================================================

  sealed class HvnRejectDetector : ISignalDetector
  {
    // **********************************************************************

    public string Name { get { return "HvnReject"; } }
    public bool Enabled { get; set; }

    // --- параметры --------------------------------------------------------

    /// <summary>Сколько последних баров (без текущего) используем для построения профиля.</summary>
    public int LookbackBars       { get; set; }
    /// <summary>Минимальная доля объёма HVN в суммарном объёме окна [0..1].</summary>
    public double MinHvnShare     { get; set; }
    /// <summary>Минимальный отрыв close от HVN в тиках, чтобы считать отскок состоявшимся.</summary>
    public int MinReclaimTicks    { get; set; }
    /// <summary>Максимальная доля объёма бара в "хвосте" за HVN (в сторону отклонения).</summary>
    public double MaxTailShare    { get; set; }
    /// <summary>Кулдаун между сигналами (в барах).</summary>
    public int CooldownBars       { get; set; }

    // --- состояние --------------------------------------------------------

    DateTime lastEmittedAt = DateTime.MinValue;
    int barsSinceLastEmit = int.MaxValue;

    // **********************************************************************

    public HvnRejectDetector()
    {
      Enabled = true;
      LookbackBars     = 20;
      MinHvnShare      = 0.12;
      MinReclaimTicks  = 2;
      MaxTailShare     = 0.15;
      CooldownBars     = 3;
    }

    // **********************************************************************

    public Signal Evaluate(ClusterHistory history)
    {
      if(barsSinceLastEmit != int.MaxValue)
        barsSinceLastEmit++;

      var s = history.Last(0);
      var prev = history.Last(1);

      if(s == null || prev == null)
        return null;

      if(s.Volume == 0 || s.Range <= 0 || s.PriceStep <= 0)
        return null;

      if(history.Count < 3)
        return null;

      int step = s.PriceStep;

      // ----------------------------------------------------------------
      //  Агрегируем volume-профиль из истории (без текущего бара)

      var agg = new Dictionary<int, long>();
      long totalAgg = 0;
      int barsUsed = 0;

      for(int i = 1; i <= LookbackBars; i++)
      {
        var h = history.Last(i);
        if(h == null)
          break;

        for(int p = h.MinPrice; p <= h.MaxPrice; p += h.PriceStep)
        {
          long v = h.Source.GetCellVolume(p);
          if(v <= 0)
            continue;

          long acc;
          agg.TryGetValue(p, out acc);
          agg[p] = acc + v;
          totalAgg += v;
        }
        barsUsed++;
      }

      if(barsUsed < 2 || totalAgg <= 0)
        return null;

      // ----------------------------------------------------------------
      //  Ищем HVN

      int hvnPrice = 0;
      long hvnVolume = 0;

      foreach(var kv in agg)
      {
        if(kv.Value > hvnVolume)
        {
          hvnVolume = kv.Value;
          hvnPrice = kv.Key;
        }
      }

      if(hvnVolume <= 0)
        return null;

      double hvnShare = (double)hvnVolume / totalAgg;
      if(hvnShare < MinHvnShare)
        return null;

      // ----------------------------------------------------------------
      //  HVN должен быть ВНУТРИ диапазона текущего бара — бар «потрогал»
      //  HVN. Иначе это не тест.

      if(hvnPrice < s.MinPrice || hvnPrice > s.MaxPrice)
        return null;

      // ----------------------------------------------------------------
      //  Сценарии отскока

      bool support    = prev.ClosePrice > hvnPrice
                     && s.MinPrice <= hvnPrice
                     && s.ClosePrice >= hvnPrice + MinReclaimTicks * step;

      bool resistance = prev.ClosePrice < hvnPrice
                     && s.MaxPrice >= hvnPrice
                     && s.ClosePrice <= hvnPrice - MinReclaimTicks * step;

      if(!support && !resistance)
        return null;

      // ----------------------------------------------------------------
      //  Хвост бара за HVN должен быть тонким — цена не «задержалась»
      //  за уровнем, а отскочила.

      long tailVol = 0;

      if(support)
      {
        for(int p = s.MinPrice; p <= hvnPrice - step; p += step)
          tailVol += s.Source.GetCellVolume(p);
      }
      else
      {
        for(int p = hvnPrice + step; p <= s.MaxPrice; p += step)
          tailVol += s.Source.GetCellVolume(p);
      }

      double tailShare = s.Volume > 0 ? (double)tailVol / s.Volume : 0;
      if(tailShare > MaxTailShare)
        return null;

      // ----------------------------------------------------------------
      //  Гистерезис

      if(s.Source.DateTime == lastEmittedAt)
        return null;

      if(barsSinceLastEmit < CooldownBars)
        return null;

      lastEmittedAt = s.Source.DateTime;
      barsSinceLastEmit = 0;

      // ----------------------------------------------------------------
      //  Сила

      double weightScore = Math.Min(1.0,
        (hvnShare - MinHvnShare) / Math.Max(1e-6, 1.0 - MinHvnShare));

      int reclaimTicks = support
        ? (s.ClosePrice - hvnPrice) / step
        : (hvnPrice - s.ClosePrice) / step;

      double reclaimScore = Math.Min(1.0,
        (double)reclaimTicks / Math.Max(1, 4 * MinReclaimTicks));

      double tailScore = 1.0 - tailShare / Math.Max(1e-6, MaxTailShare);
      if(tailScore < 0) tailScore = 0;
      if(tailScore > 1) tailScore = 1;

      double strength = 0.5 * weightScore + 0.3 * reclaimScore + 0.2 * tailScore;

      SignalKind kind = support ? SignalKind.HvnRejectUp : SignalKind.HvnRejectDown;
      SignalDirection dir = support ? SignalDirection.Up : SignalDirection.Down;

      return new Signal
      {
        Time = s.Source.DateTime,
        Kind = kind,
        Direction = dir,
        Price = hvnPrice,
        Strength = strength,
        Message = FormatMessage(hvnPrice, hvnShare, reclaimTicks, support, barsUsed),
        Details = FormatDetails(s, hvnPrice, hvnShare, reclaimTicks, tailShare, barsUsed, totalAgg)
      };
    }

    // **********************************************************************

    static string FormatMessage(int hvn, double share, int reclaimTicks, bool support, int barsUsed)
    {
      string role = support ? "поддержкой" : "сопротивлением";
      string dir  = support ? "отскок вверх" : "откат вниз";

      return string.Format(CultureInfo.InvariantCulture,
        "HVN {0} ({1}% объёма за {2} баров) сыграл {3}: close отошёл на {4} тик(ов) — {5}",
        hvn,
        (int)Math.Round(share * 100),
        barsUsed,
        role,
        reclaimTicks,
        dir);
    }

    static string FormatDetails(ClusterStats s, int hvn, double share, int reclaimTicks,
                                double tailShare, int barsUsed, long totalAgg)
    {
      return string.Format(CultureInfo.InvariantCulture,
        "hvn={0} share={1:F3} barsUsed={2} totalAgg={3} reclaimTicks={4} tailShare={5:F3} close={6} min={7} max={8}",
        hvn,
        share,
        barsUsed,
        totalAgg,
        reclaimTicks,
        tailShare,
        s.ClosePrice, s.MinPrice, s.MaxPrice);
    }

    // **********************************************************************
  }

  // ==========================================================================
}
