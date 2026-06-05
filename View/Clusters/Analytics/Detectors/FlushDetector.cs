// ==========================================================================
//  FlushDetector.cs — Импульсный пролив / вынос одним закрытым кластером
// ==========================================================================
//
//  Отдельно от TrendContinuation (окно 8 баров) и Breakout (пробой экстремума).
//  Ловит «удар» одной 30s-свечи: резкий ход, close у экстремума, объём и
//  плотность выше среднего, COM в сторону движения. POC/VA не используются.
//
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.View.ClustersSpace.Analytics.Detectors
{
  // ==========================================================================

  sealed class FlushDetector : ISignalDetector
  {
    // **********************************************************************

    public string Name { get { return "Flush"; } }
    public bool Enabled { get; set; }

    public int    AverageWindow         { get; set; }
    public int    MinRangeTicks         { get; set; }
    public int    MinFlushTicks         { get; set; }
    public double MinBodyRatio          { get; set; }
    public double MinCloseAtExtreme     { get; set; }
    public double MinVolumeRatio        { get; set; }
    public double MinDensityMultiplier{ get; set; }
    public double MaxPosComDown         { get; set; }
    public double MinPosComUp           { get; set; }
    public double MaxCounterTailShare   { get; set; }
    public double MaxTop3Share          { get; set; }
    public double MinQuarterShare       { get; set; }
    public double MinStrength           { get; set; }
    public int    CooldownBars          { get; set; }

    DateTime lastEmittedAt = DateTime.MinValue;
    int      barsSinceLastEmit = int.MaxValue;

    // **********************************************************************

    public FlushDetector()
    {
      Enabled               = true;
      AverageWindow         = 5;
      MinRangeTicks         = 8;
      MinFlushTicks         = 14;
      MinBodyRatio          = 0.70;
      MinCloseAtExtreme     = 0.80;
      MinVolumeRatio        = 1.55;
      MinDensityMultiplier  = 1.20;
      MaxPosComDown         = 0.50;
      MinPosComUp           = 0.61;
      MaxCounterTailShare   = 0.49;
      MaxTop3Share          = 0.68;
      MinQuarterShare       = 0.0;
      MinStrength           = 0.62;
      CooldownBars          = 14;
    }

    // **********************************************************************

    public Signal Evaluate(ClusterHistory history)
    {
      if(history == null || history.Count < AverageWindow + 1)
        return null;

      if(barsSinceLastEmit != int.MaxValue)
        barsSinceLastEmit++;
      if(barsSinceLastEmit < CooldownBars)
        return null;

      var last = history.Last(0);
      if(last == null || last.Volume == 0 || last.Range < MinRangeTicks)
        return null;

      var down = TryEvaluate(history, last, false);
      if(down != null)
        return Commit(down, last.Source.DateTime);

      var up = TryEvaluate(history, last, true);
      if(up != null)
        return Commit(up, last.Source.DateTime);

      return null;
    }

    // **********************************************************************

    Signal TryEvaluate(ClusterHistory history, ClusterStats last, bool up)
    {
      int body = last.ClosePrice - last.OpenPrice;
      if(up && body <= 0)
        return null;
      if(!up && body >= 0)
        return null;

      int tickMove = up ? body : -body;
      if(tickMove < MinFlushTicks)
        return null;

      double bodyRatio = (double)Math.Abs(body) / last.Range;
      if(bodyRatio < MinBodyRatio)
        return null;

      double closeExt = CloseAtFavorableExtreme(last, up);
      if(closeExt < MinCloseAtExtreme)
        return null;

      if(up)
      {
        if(last.PosCom < MinPosComUp)
          return null;
      }
      else
      {
        if(last.PosCom > MaxPosComDown)
          return null;
      }

      double counterTail = up
        ? last.ShareBeyondCom(false)
        : last.ShareBeyondCom(true);
      if(counterTail > MaxCounterTailShare + 1e-6)
        return null;

      if(last.Top3Share > MaxTop3Share)
        return null;

      if(MinQuarterShare > 0 && last.ShareInQuarter(up) < MinQuarterShare)
        return null;

      double avgVol = history.AverageVolumeBefore(AverageWindow);
      int bigMoveTicks = MinFlushTicks + 11;
      bool sparseVolImpulse = tickMove >= bigMoveTicks
        && closeExt >= 0.99
        && avgVol > 0
        && last.Volume >= avgVol * 0.88
        && last.Volume < avgVol * MinVolumeRatio;
      bool volumeOk = avgVol <= 0
        || last.Volume >= avgVol * MinVolumeRatio
        || sparseVolImpulse;
      if(!volumeOk)
        return null;

      double avgRange = history.AverageRangeBefore(AverageWindow);
      double density = (double)last.Volume / last.Range;
      double avgDensity = avgRange > 0 ? avgVol / avgRange : 0;
      bool densityOk = avgDensity <= 0
        || density >= avgDensity * MinDensityMultiplier
        || (tickMove >= bigMoveTicks && closeExt >= 0.95);
      if(!densityOk)
        return null;

      double strength = ComputeStrength(last, up, bodyRatio, closeExt, tickMove,
        counterTail, avgVol, density, avgDensity, sparseVolImpulse);
      if(strength < MinStrength)
        return null;

      return new Signal
      {
        Time      = last.Source.DateTime,
        Kind      = up ? SignalKind.FlushUp : SignalKind.FlushDown,
        Direction = up ? SignalDirection.Up : SignalDirection.Down,
        Price     = last.ComPrice,
        Strength  = strength,
        Message   = FormatMessage(up, last, tickMove, bodyRatio, closeExt, avgVol),
        Details   = FormatDetails(up, last, tickMove, bodyRatio, closeExt, counterTail,
          avgVol, density, avgDensity, strength)
      };
    }

    // **********************************************************************

    static double CloseAtFavorableExtreme(ClusterStats s, bool up)
    {
      if(s.Range <= 0)
        return 0;

      if(up)
        return 1.0 - (double)(s.MaxPrice - s.ClosePrice) / s.Range;

      return 1.0 - (double)(s.ClosePrice - s.MinPrice) / s.Range;
    }

    static double ComputeStrength(ClusterStats last, bool up, double bodyRatio,
      double closeExt, int tickMove, double counterTail, double avgVol,
      double density, double avgDensity, bool sparseVolImpulse)
    {
      double bodyScore = Math.Min(1.0, (bodyRatio - 0.65) / 0.35);
      double closeScore = Math.Min(1.0, (closeExt - 0.75) / 0.25);
      double tickScore = Math.Min(1.0, tickMove / 30.0);
      double volScore = avgVol > 0
        ? Math.Min(1.0, ((double)last.Volume / avgVol - 1.0) / 1.5)
        : 0.4;
      double densScore = avgDensity > 0
        ? Math.Min(1.0, (density / avgDensity - 1.0) / 1.5)
        : 0.4;
      double posScore = up
        ? Math.Min(1.0, (last.PosCom - 0.5) / 0.5)
        : Math.Min(1.0, (0.5 - last.PosCom) / 0.5);
      if(posScore < 0)
        posScore = 0;
      double tailScore = 1.0 - counterTail / 0.52;
      if(tailScore < 0)
        tailScore = 0;

      double strength = 0.22 * bodyScore + 0.20 * closeScore + 0.18 * tickScore
        + 0.15 * volScore + 0.10 * densScore + 0.10 * posScore + 0.05 * tailScore;

      if(last.Shape == ProfileShape.Trending)
        strength += 0.04;

      if(closeExt >= 0.99 && tickMove >= 22)
        strength += 0.08;

      if(sparseVolImpulse)
        strength += 0.18;

      if(strength < 0)
        return 0;
      if(strength > 1)
        return 1;
      return strength;
    }

    // **********************************************************************

    Signal Commit(Signal s, DateTime barTime)
    {
      if(barTime == lastEmittedAt)
        return null;

      lastEmittedAt = barTime;
      barsSinceLastEmit = 0;
      return s;
    }

    static string FormatMessage(bool up, ClusterStats s, int tickMove,
      double bodyRatio, double closeExt, double avgVol)
    {
      double volX = avgVol > 0 ? s.Volume / avgVol : 0;
      string dir = up ? "вверх" : "вниз";
      return string.Format(CultureInfo.InvariantCulture,
        "Пролив {0}: {1} тик, тело {2:P0}, close у {3:P0} экстремума, объём x{4:F1}, COM={5:F2}",
        dir, tickMove, bodyRatio, closeExt, volX, s.PosCom);
    }

    static string FormatDetails(bool up, ClusterStats s, int tickMove,
      double bodyRatio, double closeExt, double counterTail,
      double avgVol, double density, double avgDensity, double strength)
    {
      return string.Format(CultureInfo.InvariantCulture,
        "dir={0} ticks={1} body={2:F2} closeExt={3:F2} posCom={4:F2} counterTail={5:F2} top3={6:F2} vol={7} volX={8:F2} dens={9:F0} avgDens={10:F0} str={11:F2}",
        up ? "up" : "down",
        tickMove,
        bodyRatio,
        closeExt,
        s.PosCom,
        counterTail,
        s.Top3Share,
        s.Volume,
        avgVol > 0 ? s.Volume / avgVol : 0,
        density,
        avgDensity,
        strength);
    }

    // **********************************************************************
  }

  // ==========================================================================
}
