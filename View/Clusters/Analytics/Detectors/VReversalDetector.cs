// ==========================================================================
//  VReversalDetector.cs — V-разворот после абсорбции у низа (NVDA 13:36-41)
// ==========================================================================
//
//  Ситуация: серия медвежьих (реже — бычьих) кластеров с планомерным
//  снижением (ростом). На последнем медвежьем баре фиксируется абсорбция
//  у нижней/верхней границы — "поглощение продаж/покупок". Следующий
//  (разворотный) бар уже не обновляет экстремум, закрывается в противо-
//  положную сторону, и центр массы смещается назад — V-образный либо
//  ^-образный разворот.
//
//  Формальные признаки (V-up, зеркально для V-down):
//    • Серия из >= MinRun подряд медвежьих баров, заканчивающаяся на
//      history.Last(1) (предыдущем кластере);
//    • history.Last(1) (climax of selloff): PosPoc <= AbsorptionEdge,
//      т.е. POC продажной минуты в нижней четверти бара;
//    • history.Last(0) (reversal bar): ClosePrice > OpenPrice,
//      MinPrice >= history.Last(1).MinPrice (не обновил дно),
//      |close-open|/range >= MinBodyRatio (уверенная бычья свеча),
//      CenterOfMass > history.Last(1).CenterOfMass (масса объёма сместилась выше).
//
//  Гистерезис: сигнал повторно не выдаётся пока не произойдёт новая
//  серия медвежьих/бычьих баров (глубина серии сбрасывается).
//
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.View.ClustersSpace.Analytics.Detectors
{
  // ==========================================================================

  sealed class VReversalDetector : ISignalDetector
  {
    // **********************************************************************

    public string Name { get { return "VReversal"; } }
    public bool Enabled { get; set; }

    // --- параметры --------------------------------------------------------

    public int MinRun                 { get; set; } // подряд противоположных баров до разворота
    public double AbsorptionEdge      { get; set; } // PosPoc порог у низа/верха
    public double AbsorptionPocShare  { get; set; } // мин. доля POC в предыдущем баре
    public double MinBodyRatio        { get; set; } // (close-open)/range разворотного бара
    public double MinCenterOfMassShift{ get; set; } // сдвиг CoM относительно range, 0..1

    // --- состояние --------------------------------------------------------

    DateTime lastEmittedAt = DateTime.MinValue;

    // **********************************************************************

    public VReversalDetector()
    {
      Enabled = true;
      MinRun = 2;
      AbsorptionEdge = 0.35;
      AbsorptionPocShare = 0.05;
      MinBodyRatio = 0.35;
      MinCenterOfMassShift = 0.15;
    }

    // **********************************************************************

    public Signal Evaluate(ClusterHistory history)
    {
      var curr = history.Last(0);
      var prev = history.Last(1);

      if(curr == null || prev == null)
        return null;

      if(curr.Range <= 0 || prev.Range <= 0 || curr.Volume == 0 || prev.Volume == 0)
        return null;

      // Попробуем сначала V-up (разворот из падения).
      var up = TryEvaluate(history, curr, prev, bearishRun: true);
      if(up != null)
        return Commit(up, curr.Source.DateTime);

      var down = TryEvaluate(history, curr, prev, bearishRun: false);
      if(down != null)
        return Commit(down, curr.Source.DateTime);

      return null;
    }

    // **********************************************************************

    Signal TryEvaluate(ClusterHistory history, ClusterStats curr, ClusterStats prev, bool bearishRun)
    {
      // Серия противоположных баров до разворотного.
      int run = bearishRun
        ? history.CountBearishRunFrom(1)
        : history.CountBullishRunFrom(1);

      if(run < MinRun)
        return null;

      // Характер последнего бара серии: абсорбция у нижней (для bear-серии) или верхней границы.
      double absorptionEdgeLow = AbsorptionEdge;
      double absorptionEdgeHigh = 1.0 - AbsorptionEdge;

      if(bearishRun)
      {
        if(prev.PosPoc > absorptionEdgeLow) return null;
      }
      else
      {
        if(prev.PosPoc < absorptionEdgeHigh) return null;
      }

      if(prev.PocShare < AbsorptionPocShare)
        return null;

      // Разворотный бар: знак тела и нарушение экстремума.
      int body = curr.ClosePrice - curr.OpenPrice;

      if(bearishRun)
      {
        if(body <= 0) return null;                              // должен быть бычий
        if(curr.MinPrice < prev.MinPrice) return null;          // не должен обновить дно
      }
      else
      {
        if(body >= 0) return null;                              // должен быть медвежий
        if(curr.MaxPrice > prev.MaxPrice) return null;          // не должен обновить вершину
      }

      double bodyRatio = Math.Abs(body) / (double)curr.Range;
      if(bodyRatio < MinBodyRatio)
        return null;

      // Сдвиг центра массы в "разворотную" сторону.
      double com0 = curr.CenterOfMass;
      double com1 = prev.CenterOfMass;
      double reference = Math.Max(curr.Range, prev.Range);
      double shiftNorm = reference > 0 ? (com0 - com1) / reference : 0;

      if(bearishRun && shiftNorm < MinCenterOfMassShift) return null;
      if(!bearishRun && -shiftNorm < MinCenterOfMassShift) return null;

      // Сила сигнала.
      double runScore  = Math.Min(1.0, (run - MinRun + 1) / 3.0);
      double bodyScore = Math.Min(1.0, (bodyRatio - MinBodyRatio) / Math.Max(1e-6, 1.0 - MinBodyRatio));
      double edgeScore = bearishRun
        ? Math.Min(1.0, (absorptionEdgeLow - prev.PosPoc) / Math.Max(1e-6, absorptionEdgeLow))
        : Math.Min(1.0, (prev.PosPoc - absorptionEdgeHigh) / Math.Max(1e-6, 1.0 - absorptionEdgeHigh));

      double strength = 0.35 * runScore + 0.35 * bodyScore + 0.30 * Math.Max(0, edgeScore);

      var result = new Signal
      {
        Time = curr.Source.DateTime,
        Kind = bearishRun ? SignalKind.VReversalUp : SignalKind.VReversalDown,
        Direction = bearishRun ? SignalDirection.Up : SignalDirection.Down,
        Price = prev.PocPrice,
        Strength = strength,
        Message = FormatMessage(curr, prev, bearishRun, run, bodyRatio),
        Details = FormatDetails(curr, prev, bearishRun, run, bodyRatio, shiftNorm)
      };

      return result;
    }

    // **********************************************************************

    Signal Commit(Signal s, DateTime barTime)
    {
      if(barTime == lastEmittedAt)
        return null;

      lastEmittedAt = barTime;
      return s;
    }

    // **********************************************************************

    static string FormatMessage(ClusterStats curr, ClusterStats prev,
      bool bearishRun, int run, double bodyRatio)
    {
      if(bearishRun)
      {
        return string.Format(CultureInfo.InvariantCulture,
          "V-разворот вверх: {0} медвежьих, абсорбция у {1}, разворот с телом {2}% диапазона, close {3}",
          run, prev.PocPrice, (int)Math.Round(bodyRatio * 100), curr.ClosePrice);
      }

      return string.Format(CultureInfo.InvariantCulture,
        "^-разворот вниз: {0} бычьих, абсорбция у {1}, разворот с телом {2}% диапазона, close {3}",
        run, prev.PocPrice, (int)Math.Round(bodyRatio * 100), curr.ClosePrice);
    }

    static string FormatDetails(ClusterStats curr, ClusterStats prev,
      bool bearishRun, int run, double bodyRatio, double shiftNorm)
    {
      return string.Format(CultureInfo.InvariantCulture,
        "run={0} dir={1} prevPoc={2} prevPosPoc={3:F2} prevPocShare={4:F2} body={5:F2} comShift={6:F2} close={7}",
        run,
        bearishRun ? "bearish->up" : "bullish->down",
        prev.PocPrice,
        prev.PosPoc,
        prev.PocShare,
        bodyRatio,
        shiftNorm,
        curr.ClosePrice);
    }

    // **********************************************************************
  }

  // ==========================================================================
}
