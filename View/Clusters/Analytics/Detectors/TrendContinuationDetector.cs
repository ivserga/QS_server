// ==========================================================================
//  TrendContinuationDetector.cs — Продолжение тренда по профилю объёма (без POC/VA)
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.View.ClustersSpace.Analytics.Detectors
{
  // ==========================================================================

  sealed class TrendContinuationDetector : ISignalDetector
  {
    // **********************************************************************

    public string Name { get { return "TrendContinuation"; } }
    public bool Enabled { get; set; }

    public int    Lookback              { get; set; }
    public double MinTrendSlopePct      { get; set; }
    public double MinAlignedBodyShare   { get; set; }
    public double PosComFavorable       { get; set; }
    public double MinCloseAtExtreme     { get; set; }
    public double MaxCounterTailShare   { get; set; }
    public double MinQuarterShare       { get; set; }
    public double MaxTop3Share          { get; set; }
    public double MinVolumeRatio        { get; set; }
    public double MinStrength           { get; set; }
    public int    CooldownBars          { get; set; }
    public int    DeferToBreakoutTicks  { get; set; }
    public double MinNetMovePct         { get; set; }

    DateTime lastEmittedAt = DateTime.MinValue;
    int      barsSinceLastEmit = int.MaxValue;

    // **********************************************************************

    public TrendContinuationDetector()
    {
      Enabled              = true;
      Lookback             = 8;
      // SlopePctClose на 30s NVDA ~ -0.0003…-0.0005 при сильном проливе; 0.0015 не проходил никогда.
      MinTrendSlopePct     = 0.00005;
      MinNetMovePct        = 0.00035;
      MinAlignedBodyShare  = 0.38;
      PosComFavorable      = 0.46;
      MinCloseAtExtreme    = 0.30;
      MaxCounterTailShare  = 0.42;
      MinQuarterShare      = 0.08;
      MaxTop3Share         = 0.58;
      MinVolumeRatio       = 0.65;
      MinStrength          = 0.40;
      CooldownBars         = 1;
      // 0 — не отдавать каскадные low Breakout; продолжение и пробой могут совпасть на импульсе.
      DeferToBreakoutTicks = 0;
    }

    // **********************************************************************

    public Signal Evaluate(ClusterHistory history)
    {
      if(history == null || history.Count < Lookback)
        return null;

      if(barsSinceLastEmit != int.MaxValue)
        barsSinceLastEmit++;
      if(barsSinceLastEmit < CooldownBars)
        return null;

      var last = history.Last(0);
      if(last == null || last.Range <= 0 || last.Volume == 0)
        return null;

      // Сначала down: на импульсных проливах оба направления иногда проходят slope/net из-за откатов в окне.
      var down = TryEvaluate(history, last, false);
      if(down != null)
        return Commit(down, last.Source.DateTime);

      var up = TryEvaluate(history, last, true);
      if(down != null)
        return Commit(down, last.Source.DateTime);

      return null;
    }

    // **********************************************************************

    Signal TryEvaluate(ClusterHistory history, ClusterStats last, bool up)
    {
      double slopeClose = WindowMath.SlopePctClose(history, 0, Lookback);
      if(!HasTrendMomentum(history, up, slopeClose))
        return null;

      if(AlignedBodyShare(history, Lookback, up) < MinAlignedBodyShare)
        return null;

      if(up && LooksLikeDistribution(history, Lookback))
        return null;
      // Higher lows только если тренд вверх по close уже слабый — иначе отсекаем отскок у дна на проливе.
      if(!up && slopeClose > -MinTrendSlopePct * 2.0 && LooksLikeAccumulation(history, Lookback))
        return null;

      if(DeferToBreakoutTicks > 0 && IsFreshBreakout(history, last, up))
        return null;

      double posThreshold = up ? PosComFavorable : (1.0 - PosComFavorable);
      if(up)
      {
        if(last.PosCom < posThreshold)
          return null;
      }
      else
      {
        if(last.PosCom > posThreshold)
          return null;
      }

      double closeAtExtreme = CloseExtremeShare(last, up);
      if(closeAtExtreme < MinCloseAtExtreme)
        return null;

      double counterTail = up
        ? last.ShareBeyondCom(false)
        : last.ShareBeyondCom(true);
      if(counterTail > MaxCounterTailShare)
        return null;

      if(last.ShareInQuarter(up) < MinQuarterShare)
        return null;

      if(last.Top3Share > MaxTop3Share)
        return null;

      double avgVol = history.AverageVolumeBefore(Lookback);
      if(avgVol > 0 && last.Volume < avgVol * MinVolumeRatio)
        return null;

      if(last.Shape == ProfileShape.Thin)
        return null;

      double avgPosCom = AveragePosCom(history, Lookback);
      if(up && avgPosCom < 0.45)
        return null;
      if(!up && avgPosCom > 0.58)
        return null;

      double strength = ComputeStrength(last, up, slopeClose, closeAtExtreme,
        counterTail, avgVol);
      if(strength < MinStrength)
        return null;

      return new Signal
      {
        Time      = last.Source.DateTime,
        Kind      = up ? SignalKind.TrendContinuationUp : SignalKind.TrendContinuationDown,
        Direction = up ? SignalDirection.Up : SignalDirection.Down,
        Price     = last.ComPrice,
        Strength  = strength,
        Message   = FormatMessage(up, last, slopeClose, closeAtExtreme, counterTail),
        Details   = FormatDetails(up, last, slopeClose, closeAtExtreme, counterTail, avgVol, avgPosCom, strength)
      };
    }

    // **********************************************************************

    static double AlignedBodyShare(ClusterHistory history, int count, bool up)
    {
      int aligned = 0;
      int total = 0;
      for(int i = 0; i < count; i++)
      {
        var s = history.Last(i);
        if(s == null)
          break;

        total++;
        if(up && s.ClosePrice > s.OpenPrice)
          aligned++;
        else if(!up && s.ClosePrice < s.OpenPrice)
          aligned++;
      }

      return total > 0 ? (double)aligned / total : 0;
    }

    static bool LooksLikeDistribution(ClusterHistory history, int count)
    {
      if(!WindowMath.TwoHalvesHighs(history, count, out int hi1, out int hi2))
        return false;

      return hi2 < hi1;
    }

    static bool LooksLikeAccumulation(ClusterHistory history, int count)
    {
      if(!WindowMath.TwoHalvesLows(history, count, out int lo1, out int lo2))
        return false;

      return lo2 > lo1;
    }

    bool IsFreshBreakout(ClusterHistory history, ClusterStats last, bool up)
    {
      int prior = up
        ? WindowMath.HighestHighExcludingLast(history, Lookback, 1)
        : WindowMath.LowestLowExcludingLast(history, Lookback, 1);

      if(up)
      {
        if(prior == int.MinValue)
          return false;
        return last.ClosePrice >= prior + DeferToBreakoutTicks;
      }

      if(prior == int.MaxValue)
        return false;
      return last.ClosePrice <= prior - DeferToBreakoutTicks;
    }

    /// <summary>
    /// Насколько close прижат к благоприятному экстремуму бара: 1 = у high (up) / у low (down).
    /// </summary>
    static double CloseExtremeShare(ClusterStats s, bool up)
    {
      if(s.Range <= 0)
        return 0;

      if(up)
        return 1.0 - (double)(s.MaxPrice - s.ClosePrice) / s.Range;

      return 1.0 - (double)(s.ClosePrice - s.MinPrice) / s.Range;
    }

    bool HasTrendMomentum(ClusterHistory history, bool up, double slopeClose)
    {
      if(up)
      {
        if(slopeClose >= MinTrendSlopePct)
          return true;
      }
      else
      {
        if(slopeClose <= -MinTrendSlopePct)
          return true;
      }

      var oldest = history.Last(Lookback - 1);
      var last = history.Last(0);
      if(oldest == null || last == null)
        return false;

      double mean = (oldest.ClosePrice + last.ClosePrice) / 2.0;
      if(mean <= 0)
        return false;

      double net = (last.ClosePrice - oldest.ClosePrice) / mean;
      if(up)
        return net >= MinNetMovePct;
      return net <= -MinNetMovePct;
    }

    static double AveragePosCom(ClusterHistory history, int count)
    {
      double sum = 0;
      int n = 0;
      for(int i = 0; i < count; i++)
      {
        var s = history.Last(i);
        if(s == null)
          break;
        sum += s.PosCom;
        n++;
      }
      return n > 0 ? sum / n : 0.5;
    }

    double ComputeStrength(ClusterStats last, bool up, double slopeClose,
      double closeExtreme, double counterTail, double avgVol)
    {
      double slopeScore = Math.Min(1.0, Math.Abs(slopeClose) / Math.Max(1e-6, MinTrendSlopePct * 4));
      double closeScore = Math.Min(1.0, closeExtreme / Math.Max(1e-6, MinCloseAtExtreme));

      double posScore = up
        ? Math.Min(1.0, (last.PosCom - 0.5) / 0.5)
        : Math.Min(1.0, (0.5 - last.PosCom) / 0.5);
      if(posScore < 0)
        posScore = 0;

      double tailScore = 1.0 - (counterTail / Math.Max(1e-6, MaxCounterTailShare));
      if(tailScore < 0)
        tailScore = 0;

      double volScore = avgVol > 0
        ? Math.Min(1.0, (double)last.Volume / avgVol)
        : 0.5;

      double quarterScore = Math.Min(1.0, last.ShareInQuarter(up) / Math.Max(1e-6, MinQuarterShare));

      double strength = 0.22 * slopeScore + 0.22 * closeScore + 0.20 * posScore
        + 0.16 * tailScore + 0.10 * volScore + 0.10 * quarterScore;

      if(up && last.Skewness < 0)
        strength -= 0.04;
      if(!up && last.Skewness > 0)
        strength -= 0.04;

      if(last.Shape == ProfileShape.Trending)
        strength += 0.06;

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

    static string FormatMessage(bool up, ClusterStats s, double slopeClose,
      double closeExtreme, double counterTail)
    {
      string dir = up ? "вверх" : "вниз";
      return string.Format(CultureInfo.InvariantCulture,
        "Продолжение тренда {0}: COM={1:F2}, close у {2:P0} экстремума, хвост против {3:P0}, slope {4:F4}/бар",
        dir, s.PosCom, closeExtreme, counterTail, slopeClose);
    }

    static string FormatDetails(bool up, ClusterStats s, double slopeClose,
      double closeExtreme, double counterTail, double avgVol, double avgPosCom, double strength)
    {
      return string.Format(CultureInfo.InvariantCulture,
        "dir={0} com={1:F2} avgPosCom={2:F2} closeExt={3:F2} counterTail={4:F2} qShare={5:F2} top3={6:F2} skew={7:F2} shape={8} vol={9} slope={10:F4} str={11:F2}",
        up ? "up" : "down",
        s.PosCom,
        avgPosCom,
        closeExtreme,
        counterTail,
        s.ShareInQuarter(up),
        s.Top3Share,
        s.Skewness,
        s.Shape,
        s.Volume,
        slopeClose,
        strength);
    }

    // **********************************************************************
  }

  // ==========================================================================
}
