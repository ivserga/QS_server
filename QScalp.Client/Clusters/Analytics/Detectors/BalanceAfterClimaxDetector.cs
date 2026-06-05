// ==========================================================================
//  BalanceAfterClimaxDetector.cs — Баланс после климакса
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.Client.Clusters.Analytics.Detectors
{
    internal sealed class BalanceAfterClimaxDetector : ISignalDetector
    {
        public string Name => "BalanceAfterClimax";
        public bool Enabled { get; set; }

        public double BalanceVolumeShare { get; set; }
        public double BalanceRangeShare { get; set; }
        public double ClimaxMinTop3Share { get; set; }
        public double ClimaxDensityMult { get; set; }
        public int AverageWindow { get; set; }

        DateTime _lastEmittedAt = DateTime.MinValue;

        public BalanceAfterClimaxDetector()
        {
            Enabled = true;
            BalanceVolumeShare = 0.65;
            BalanceRangeShare = 0.65;
            ClimaxMinTop3Share = 0.35;
            ClimaxDensityMult = 1.3;
            AverageWindow = 5;
        }

        public Signal Evaluate(ClusterHistory history)
        {
            var curr = history.Last(0);
            var prev = history.Last(1);
            if (curr == null || prev == null) return null;
            if (prev.Volume == 0 || prev.Range <= 0 || curr.Volume == 0) return null;

            double avgVol = history.AverageVolumeBefore(AverageWindow + 1);
            double avgRange = history.AverageRangeBefore(AverageWindow + 1);

            double prevDensity = (double)prev.Volume / Math.Max(1, prev.Range);
            double avgDensity = avgRange > 0 ? (avgVol / avgRange) : 0;

            bool prevIsClimax = prev.Top3Share >= ClimaxMinTop3Share &&
                (avgDensity == 0 || prevDensity >= avgDensity * ClimaxDensityMult);
            if (!prevIsClimax) return null;

            double volRatio = (double)curr.Volume / prev.Volume;
            double rangeRatio = (double)curr.Range / prev.Range;
            if (volRatio > BalanceVolumeShare) return null;
            if (rangeRatio > BalanceRangeShare) return null;

            if (curr.Source.DateTime == _lastEmittedAt) return null;
            _lastEmittedAt = curr.Source.DateTime;

            SignalDirection dir = SignalDirection.None;
            if (prev.PosCom <= 0.25) dir = SignalDirection.Up;
            else if (prev.PosCom >= 0.75) dir = SignalDirection.Down;

            double strength =
                0.5 * Math.Min(1.0, (BalanceVolumeShare - volRatio) / Math.Max(1e-6, BalanceVolumeShare)) +
                0.3 * Math.Min(1.0, (BalanceRangeShare - rangeRatio) / Math.Max(1e-6, BalanceRangeShare)) +
                0.2 * Math.Min(1.0, (prev.Top3Share - ClimaxMinTop3Share) / Math.Max(1e-6, 1.0 - ClimaxMinTop3Share));

            return new Signal
            {
                Time = curr.Source.DateTime,
                Kind = SignalKind.BalanceAfterClimax,
                Direction = dir,
                Price = prev.ComPrice,
                Strength = strength,
                Message = FormatMessage(prev, volRatio, rangeRatio, dir),
                Details = FormatDetails(curr, prev, volRatio, rangeRatio)
            };
        }

        static string FormatMessage(ClusterStats prev, double volRatio, double rangeRatio, SignalDirection dir)
        {
            string hint;
            switch (dir)
            {
                case SignalDirection.Up: hint = "подтверждение отката вверх от зоны " + prev.ComPrice; break;
                case SignalDirection.Down: hint = "подтверждение отката вниз от зоны " + prev.ComPrice; break;
                default: hint = "подтверждение завершения импульса"; break;
            }
            return string.Format(CultureInfo.InvariantCulture,
                "Баланс после климакса: объём x{0:F2}, range x{1:F2} — {2}",
                volRatio, rangeRatio, hint);
        }

        static string FormatDetails(ClusterStats curr, ClusterStats prev,
            double volRatio, double rangeRatio)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "prevCom={0} prevTop3={1:F2} prevVol={2} curVol={3} volRatio={4:F2} rangeRatio={5:F2}",
                prev.ComPrice, prev.Top3Share, prev.Volume, curr.Volume,
                volRatio, rangeRatio);
        }
    }
}
