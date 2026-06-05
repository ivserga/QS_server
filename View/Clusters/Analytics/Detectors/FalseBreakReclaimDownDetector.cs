// ==========================================================================
//  FalseBreakReclaimDownDetector.cs — Ложный пробой уровня вверх + возврат ниже
// ==========================================================================
//  Зеркало FalseBreakReclaimDetector: уровень = LowestLow за lookback,
//  trap пробивает вверх, reclaim-бар закрывается ниже уровня (short / upthrust).
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.View.ClustersSpace.Analytics.Detectors
{
  sealed class FalseBreakReclaimDownDetector : ISignalDetector
  {
    public string Name { get { return "FalseBreakReclaimDown"; } }
    public bool Enabled { get; set; }

    public int    LookbackBars          { get; set; }
    public int    NearLevelTicks        { get; set; }
    public int    MinNearTouches        { get; set; }
    public int    FalseBreakMinTicks    { get; set; }
    public double FalseBreakVolumeRatio { get; set; }
    public int    MaxBarsAfterTrap      { get; set; }
    public int    ReclaimBreakTicks     { get; set; }
    public double ReclaimVolumeRatio    { get; set; }
    public double ReclaimMinBodyRatio   { get; set; }
    /// <summary>Макс. PosCom на reclaim-баре (продавцы у низа).</summary>
    public double ReclaimPosComMax       { get; set; }
    public double ReclaimMaxTop3Share   { get; set; }
    public double MinStrength           { get; set; }
    public int    CooldownBars          { get; set; }

    DateTime lastEmittedAt = DateTime.MinValue;
    int barsSinceLastEmit = int.MaxValue;

    public FalseBreakReclaimDownDetector()
    {
      Enabled               = true;
      LookbackBars          = 12;
      NearLevelTicks        = 4;
      MinNearTouches        = 3;
      FalseBreakMinTicks    = 3;
      FalseBreakVolumeRatio = 1.05;
      MaxBarsAfterTrap      = 4;
      ReclaimBreakTicks     = 2;
      ReclaimVolumeRatio    = 0.95;
      ReclaimMinBodyRatio   = 0.40;
      ReclaimPosComMax      = 0.42;
      ReclaimMaxTop3Share   = 0.50;
      MinStrength           = 0.52;
      CooldownBars          = 6;
    }

    public Signal Evaluate(ClusterHistory history)
    {
      if(history == null) return null;
      if(history.Count < LookbackBars + 2) return null;

      if(barsSinceLastEmit != int.MaxValue) barsSinceLastEmit++;
      if(barsSinceLastEmit < CooldownBars) return null;

      var curr = history.Last(0);
      if(curr == null || curr.Range <= 0 || curr.Volume == 0) return null;

      int body = curr.ClosePrice - curr.OpenPrice;
      if(body >= 0) return null;

      double bodyRatio = Math.Abs(body) / (double)curr.Range;
      if(bodyRatio < ReclaimMinBodyRatio) return null;

      double avgVol = history.AverageVolumeBefore(LookbackBars);
      if(avgVol > 0 && curr.Volume < avgVol * ReclaimVolumeRatio) return null;

      if(curr.PosCom > ReclaimPosComMax) return null;
      if(curr.Top3Share > ReclaimMaxTop3Share) return null;

      ClusterStats bestTrap = null;
      int bestTrapOffset = -1;
      int bestLevel = 0;
      int bestNearTouches = 0;
      int bestTrapDepth = -1;

      int maxOffset = Math.Min(MaxBarsAfterTrap, history.Count - 2);
      for(int trapOffset = 1; trapOffset <= maxOffset; trapOffset++)
      {
        var trap = history.Last(trapOffset);
        if(trap == null || trap.Range <= 0 || trap.Volume == 0) continue;

        int level = WindowMath.LowestLowExcludingLast(history, LookbackBars, trapOffset + 1);
        if(level == int.MaxValue) continue;

        int nearTouches = CountNearTouches(history, level, trapOffset);
        if(nearTouches < MinNearTouches) continue;

        if(trap.MaxPrice < level + FalseBreakMinTicks) continue;
        if(trap.ClosePrice < level - NearLevelTicks) continue;
        if(curr.ClosePrice > level - ReclaimBreakTicks) continue;

        int trapDepth = Math.Max(0, trap.MaxPrice - level);
        bool better = trapDepth > bestTrapDepth
          || (trapDepth == bestTrapDepth && bestTrap != null && trap.Volume > bestTrap.Volume);
        if(!better) continue;

        bestTrap = trap;
        bestTrapOffset = trapOffset;
        bestLevel = level;
        bestNearTouches = nearTouches;
        bestTrapDepth = trapDepth;
      }

      if(bestTrap == null) return null;

      double avgVolBeforeBestTrap = history.AverageVolumeBeforeFrom(bestTrapOffset, LookbackBars);
      if(avgVolBeforeBestTrap > 0 && bestTrap.Volume <= avgVolBeforeBestTrap * FalseBreakVolumeRatio)
        return null;

      int reclaimDepthFinal = Math.Max(0, bestLevel - curr.ClosePrice);
      double trapScoreFinal = Math.Min(1.0, bestTrapDepth / Math.Max(1.0, FalseBreakMinTicks * 4.0));
      double reclaimScoreFinal = Math.Min(1.0, reclaimDepthFinal / Math.Max(1.0, ReclaimBreakTicks * 4.0));
      double bodyScoreFinal = Math.Min(1.0, (bodyRatio - ReclaimMinBodyRatio) / Math.Max(1e-6, 1.0 - ReclaimMinBodyRatio));
      double touchScoreFinal = Math.Min(1.0, (bestNearTouches - MinNearTouches + 1) / 3.0);

      double volTrapScoreFinal = avgVolBeforeBestTrap > 0
        ? Math.Min(1.0, ((double)bestTrap.Volume / avgVolBeforeBestTrap - FalseBreakVolumeRatio) / Math.Max(1e-6, FalseBreakVolumeRatio))
        : 0.2;
      double volReclaimScoreFinal = avgVol > 0
        ? Math.Min(1.0, ((double)curr.Volume / avgVol - ReclaimVolumeRatio) / Math.Max(1e-6, ReclaimVolumeRatio))
        : 0.2;

      double posComScoreFinal = Math.Min(1.0, (ReclaimPosComMax - curr.PosCom) / Math.Max(1e-6, ReclaimPosComMax));

      double strengthFinal = 0.22 * trapScoreFinal
                           + 0.22 * reclaimScoreFinal
                           + 0.16 * bodyScoreFinal
                           + 0.12 * touchScoreFinal
                           + 0.10 * Math.Max(0, volTrapScoreFinal)
                           + 0.10 * Math.Max(0, volReclaimScoreFinal)
                           + 0.08 * Math.Max(0, posComScoreFinal);
      strengthFinal = Math.Max(0, Math.Min(1, strengthFinal));
      if(strengthFinal < MinStrength) return null;

      var sFinal = new Signal
      {
        Time = curr.Source.DateTime,
        Kind = SignalKind.FalseBreakReclaimDown,
        Direction = SignalDirection.Down,
        Price = bestLevel,
        Strength = strengthFinal,
        Message = FormatMessage(bestLevel, bestTrap, curr, bestNearTouches, bestTrapOffset),
        Details = FormatDetails(bestLevel, bestTrap, curr, bestNearTouches, bestTrapOffset, bodyRatio, avgVolBeforeBestTrap, avgVol, strengthFinal)
      };

      return Commit(sFinal, curr.Source.DateTime);
    }

    int CountNearTouches(ClusterHistory history, int level, int trapOffset)
    {
      int touches = 0;
      for(int i = trapOffset + 1; i <= trapOffset + LookbackBars; i++)
      {
        var s = history.Last(i);
        if(s == null) continue;

        bool nearByLow = Math.Abs(level - s.MinPrice) <= NearLevelTicks;
        bool nearByClose = Math.Abs(level - s.ClosePrice) <= NearLevelTicks;
        if(nearByLow || nearByClose) touches++;
      }
      return touches;
    }

    Signal Commit(Signal s, DateTime barTime)
    {
      if(barTime == lastEmittedAt) return null;
      lastEmittedAt = barTime;
      barsSinceLastEmit = 0;
      return s;
    }

    static string FormatMessage(int level, ClusterStats trap, ClusterStats curr, int nearTouches, int trapOffset)
    {
      return string.Format(CultureInfo.InvariantCulture,
        "Ложный пробой вверх и возврат: уровень {0}, вынос до {1} (бар -{2}) на объёме {3}, возврат close {4} ниже уровня ({5} касания до выноса)",
        level, trap.MaxPrice, trapOffset, trap.Volume, curr.ClosePrice, nearTouches);
    }

    static string FormatDetails(int level, ClusterStats trap, ClusterStats curr, int nearTouches,
      int trapOffset, double bodyRatio, double avgVolBeforeTrap, double avgVolReclaim, double strength)
    {
      return string.Format(CultureInfo.InvariantCulture,
        "level={0} nearTouches={1} trapOffset={2} trap[max={3} close={4} vol={5} avg={6:F0}] reclaim[close={7} body={8:F2} vol={9} avg={10:F0} posCom={11:F2} top3={12:F2}] strength={13:F2}",
        level, nearTouches, trapOffset, trap.MaxPrice, trap.ClosePrice, trap.Volume, avgVolBeforeTrap,
        curr.ClosePrice, bodyRatio, curr.Volume, avgVolReclaim, curr.PosCom, curr.Top3Share, strength);
    }
  }
}
