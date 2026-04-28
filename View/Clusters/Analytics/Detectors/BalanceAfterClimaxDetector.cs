// ==========================================================================
//  BalanceAfterClimaxDetector.cs — Баланс после климакса (NVDA 14:04)
// ==========================================================================
//
//  Подтверждающий сигнал: после кульминационного бара со сжатой концентрацией
//  объёма идёт бар, в котором:
//    • объём резко упал (<= BalanceVolumeShare от предыдущего),
//    • диапазон сжался (<= BalanceRangeShare от предыдущего),
//    • |Delta| маленькая (рынок "замер").
//
//  Это классическое подтверждение завершения импульса и "базы" для разворота.
//  Детектор опирается на предыдущий кластер в истории, не требуя, чтобы
//  ClimaxDetector выдал сигнал ранее (они считаются независимо, чтобы один
//  не маскировал другой).
//
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.View.ClustersSpace.Analytics.Detectors
{
  // ==========================================================================

  sealed class BalanceAfterClimaxDetector : ISignalDetector
  {
    // **********************************************************************

    public string Name { get { return "BalanceAfterClimax"; } }
    public bool Enabled { get; set; }

    // --- параметры --------------------------------------------------------

    public double BalanceVolumeShare { get; set; }
    public double BalanceRangeShare  { get; set; }
    public double MaxDeltaShare      { get; set; }  // |Delta|/Volume
    public double ClimaxMinTop3Share { get; set; }
    public double ClimaxDensityMult  { get; set; }
    public int AverageWindow         { get; set; }

    // --- состояние --------------------------------------------------------

    DateTime lastEmittedAt = DateTime.MinValue;

    // **********************************************************************

    public BalanceAfterClimaxDetector()
    {
      Enabled = true;
      BalanceVolumeShare = 0.65;
      BalanceRangeShare  = 0.65;
      MaxDeltaShare      = 0.15;
      ClimaxMinTop3Share = 0.35;
      ClimaxDensityMult  = 1.3;
      AverageWindow      = 5;
    }

    // **********************************************************************

    public Signal Evaluate(ClusterHistory history)
    {
      var curr = history.Last(0);
      var prev = history.Last(1);

      if(curr == null || prev == null)
        return null;

      if(prev.Volume == 0 || prev.Range <= 0 || curr.Volume == 0)
        return null;

      // Предыдущий бар должен выглядеть как климакс: концентрация + плотность.
      double avgVol = history.AverageVolumeBefore(AverageWindow + 1);
      double avgRange = history.AverageRangeBefore(AverageWindow + 1);

      double prevDensity = (double)prev.Volume / Math.Max(1, prev.Range);
      double avgDensity = avgRange > 0 ? (avgVol / avgRange) : 0;

      bool prevIsClimax =
        prev.Top3Share >= ClimaxMinTop3Share &&
        (avgDensity == 0 || prevDensity >= avgDensity * ClimaxDensityMult);

      if(!prevIsClimax)
        return null;

      // Текущий бар — "замирание": объём и range сжаты.
      double volRatio   = (double)curr.Volume / prev.Volume;
      double rangeRatio = (double)curr.Range / prev.Range;
      double absDelta   = Math.Abs(curr.Delta) / (double)Math.Max(1, curr.Volume);

      if(volRatio > BalanceVolumeShare)
        return null;

      if(rangeRatio > BalanceRangeShare)
        return null;

      if(absDelta > MaxDeltaShare)
        return null;

      if(curr.Source.DateTime == lastEmittedAt)
        return null;

      lastEmittedAt = curr.Source.DateTime;

      // Направление ожидаемого отката — по PosPoc предыдущего (climax) бара:
      //   selling climax → ожидание up, buying climax → ожидание down.
      SignalDirection dir = SignalDirection.None;
      if(prev.PosPoc <= 0.25) dir = SignalDirection.Up;
      else if(prev.PosPoc >= 0.75) dir = SignalDirection.Down;

      double strength =
        0.5 * Math.Min(1.0, (BalanceVolumeShare - volRatio) / Math.Max(1e-6, BalanceVolumeShare)) +
        0.3 * Math.Min(1.0, (BalanceRangeShare - rangeRatio) / Math.Max(1e-6, BalanceRangeShare)) +
        0.2 * Math.Min(1.0, (prev.Top3Share - ClimaxMinTop3Share) / Math.Max(1e-6, 1.0 - ClimaxMinTop3Share));

      return new Signal
      {
        Time = curr.Source.DateTime,
        Kind = SignalKind.BalanceAfterClimax,
        Direction = dir,
        Price = prev.PocPrice,
        Strength = strength,
        Message = FormatMessage(curr, prev, volRatio, rangeRatio, dir),
        Details = FormatDetails(curr, prev, volRatio, rangeRatio, absDelta)
      };
    }

    // **********************************************************************

    static string FormatMessage(ClusterStats curr, ClusterStats prev,
      double volRatio, double rangeRatio, SignalDirection dir)
    {
      string hint;
      switch(dir)
      {
        case SignalDirection.Up:   hint = "подтверждение отката вверх от зоны " + prev.PocPrice; break;
        case SignalDirection.Down: hint = "подтверждение отката вниз от зоны " + prev.PocPrice; break;
        default:                   hint = "подтверждение завершения импульса";                    break;
      }

      return string.Format(CultureInfo.InvariantCulture,
        "Баланс после климакса: объём x{0:F2}, range x{1:F2} — {2}",
        volRatio, rangeRatio, hint);
    }

    static string FormatDetails(ClusterStats curr, ClusterStats prev,
      double volRatio, double rangeRatio, double absDelta)
    {
      return string.Format(CultureInfo.InvariantCulture,
        "prevPoc={0} prevTop3={1:F2} prevVol={2} curVol={3} volRatio={4:F2} rangeRatio={5:F2} |delta|/vol={6:F3}",
        prev.PocPrice, prev.Top3Share, prev.Volume, curr.Volume,
        volRatio, rangeRatio, absDelta);
    }

    // **********************************************************************
  }

  // ==========================================================================
}
