// ==========================================================================
//  AbsorptionDetector.cs — Абсорбция на границе диапазона (ситуация PL @3900)
// ==========================================================================
//
//  Что это такое:
//    Крупный лимитный участник стоит на границе бара (чаще всего — на
//    круглом уровне). Рынок атакует этот уровень маркет-ордерами, объём
//    концентрируется в 1-3 тиках у границы, но цена не пробивает уровень:
//    за границей (тикетный хвост) почти нет объёма, Close возвращается к
//    центру или идёт против атаки. Это предвещает разворот против атаки.
//
//  Формальные признаки на последнем закрытом кластере:
//    • PocShare >= MinTop3Share (крупный сгусток на одном уровне)
//    • POC находится у границы бара: PosPoc >= 0.85 (сверху) или <= 0.15
//    • Объём за POC к этой границе <= TailMaxShare (тонкий хвост, "отказ")
//    • Объём минуты >= VolumeMultiplier * среднее по окну (аномальная минута)
//    • Close не ушёл ЗА уровень: для sell-absorption Close <= POC, для buy —
//      Close >= POC (покупатели не удержали пробой / продавцы не удержали пробой)
//
//  Гистерезис: детектор не выдаёт сигнал, если он уже выдавал его
//  на том же (или предыдущем) кластере.
//
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.View.ClustersSpace.Analytics.Detectors
{
  // ==========================================================================

  sealed class AbsorptionDetector : ISignalDetector
  {
    // **********************************************************************

    public string Name { get { return "Absorption"; } }
    public bool Enabled { get; set; }

    // --- параметры --------------------------------------------------------

    public double MinTop3Share       { get; set; }
    public double EdgeThreshold     { get; set; } // PosPoc пороги у границ
    public double TailMaxShare      { get; set; } // доля объёма за POC (в "хвосте")
    public double VolumeMultiplier  { get; set; } // во сколько раз > среднего
    public int AverageWindow        { get; set; } // длина окна среднего объёма

    // --- гистерезис -------------------------------------------------------

    DateTime lastEmittedAt = DateTime.MinValue;

    // **********************************************************************

    public AbsorptionDetector()
    {
      Enabled = true;
      MinTop3Share = 0.30;
      EdgeThreshold = 0.85;
      TailMaxShare = 0.05;
      VolumeMultiplier = 1.4;
      AverageWindow = 5;
    }

    // **********************************************************************

    public Signal Evaluate(ClusterHistory history)
    {
      var s = history.Last(0);
      if(s == null)
        return null;

      if(s.Volume == 0 || s.Range <= 0)
        return null;

      // Аномальный объём по отношению к окну (если окна нет — пропускаем ограничение)
      double avg = history.AverageVolumeBefore(AverageWindow);
      if(avg > 0 && s.Volume < avg * VolumeMultiplier)
        return null;

      bool atTop    = s.PosCom >= EdgeThreshold;
      bool atBottom = s.PosCom <= (1.0 - EdgeThreshold);

      if(!atTop && !atBottom)
        return null;

      if(s.Top3Share < MinTop3Share)
        return null;

      // Тонкий хвост ЗА POC к соответствующей границе бара.
      double tailShare = s.ShareBeyondCom(atTop);
      if(tailShare > TailMaxShare)
        return null;

      // Close не должен пробить POC наружу.
      if(atTop    && s.ClosePrice > s.ComPrice) return null;
      if(atBottom && s.ClosePrice < s.ComPrice) return null;

      // Гистерезис по времени: не дублировать сигнал на том же кластере.
      if(s.Source.DateTime == lastEmittedAt)
        return null;

      lastEmittedAt = s.Source.DateTime;

      // Сила сигнала — линейная комбинация:
      //   50% — превышение PocShare над порогом,
      //   30% — «сухость» хвоста (1 - tailShare/TailMaxShare),
      //   20% — превышение объёма над средним.
      double top3Score = Math.Min(1.0, (s.Top3Share - MinTop3Share) / Math.Max(1e-6, 1.0 - MinTop3Share));
      double tailScore = 1.0 - (tailShare / Math.Max(1e-6, TailMaxShare));
      double volScore = avg > 0 ? Math.Min(1.0, (s.Volume / avg - VolumeMultiplier) / VolumeMultiplier) : 0.3;
      double strength = 0.5 * top3Score + 0.3 * tailScore + 0.2 * Math.Max(0, volScore);

      var result = new Signal
      {
        Time = s.Source.DateTime,
        Kind = atTop ? SignalKind.AbsorptionSell : SignalKind.AbsorptionBuy,
        Direction = atTop ? SignalDirection.Down : SignalDirection.Up,
        Price = s.ComPrice,
        Strength = strength,
        Message = FormatMessage(s, atTop, tailShare, avg),
        Details = FormatDetails(s, atTop, tailShare, avg)
      };

      return result;
    }

    // **********************************************************************

    static string FormatMessage(ClusterStats s, bool atTop, double tailShare, double avg)
    {
      string side = atTop ? "продавца" : "покупателя";
      string dir  = atTop ? "вниз" : "вверх";

      return string.Format(CultureInfo.InvariantCulture,
        "Абсорбция {0} на {1}: Top3 {2}%, за COM {3}%, ожидание отката {4}",
        side,
        s.ComPrice,
        (int)Math.Round(s.Top3Share * 100),
        (int)Math.Round(tailShare * 100),
        dir);
    }

    static string FormatDetails(ClusterStats s, bool atTop, double tailShare, double avg)
    {
      return string.Format(CultureInfo.InvariantCulture,
        "com={0} top3Share={1:F2} posCom={2:F2} tail={3:F3} vol={4} avg={5:F0} close={6} shape={7}",
        s.ComPrice,
        s.Top3Share,
        s.PosCom,
        tailShare,
        s.Volume,
        avg,
        s.ClosePrice,
        s.Shape);
    }

    // **********************************************************************
  }

  // ==========================================================================
}
