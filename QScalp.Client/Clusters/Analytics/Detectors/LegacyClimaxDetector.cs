// ==========================================================================
//  LegacyClimaxDetector.cs — Порт ClusterAnalyzer.AnalyzeClimax
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.Client.Clusters.Analytics.Detectors
{
    internal sealed class LegacyClimaxDetector : ISignalDetector
    {
        public string Name => "LegacyClimax";
        public bool Enabled { get; set; }

        public double VolumeClimaxMultiplier { get; set; }
        public double VolumeRatioThreshold { get; set; }

        DateTime _lastEmittedAt = DateTime.MinValue;

        public LegacyClimaxDetector()
        {
            Enabled = true;
            VolumeClimaxMultiplier = 3.0;
            VolumeRatioThreshold = 0.6;
        }

        public Signal Evaluate(ClusterHistory history)
        {
            var s3 = history.Last(0);
            var s2 = history.Last(1);
            var s1 = history.Last(2);
            if (s1 == null || s2 == null || s3 == null) return null;

            var c1 = s1.Source;
            var c2 = s2.Source;
            var c3 = s3.Source;

            if (c1.Volume == 0 || c2.Volume == 0 || c3.Volume == 0) return null;

            int maxPrevVolume = c1.Volume > c2.Volume ? c1.Volume : c2.Volume;
            if (c3.Volume < maxPrevVolume * VolumeClimaxMultiplier) return null;

            c3.GetVolumeDistribution(out long volumeAbove, out long volumeBelow);
            long distributed = volumeAbove + volumeBelow;
            if (distributed == 0) return null;

            double ratioBelow = (double)volumeBelow / distributed;
            double ratioAbove = (double)volumeAbove / distributed;

            SignalKind kind = SignalKind.None;
            SignalDirection dir = SignalDirection.None;
            bool bearish = false;

            if (c3.ClosePrice < c3.OpenPrice && ratioBelow > VolumeRatioThreshold)
            {
                kind = SignalKind.BearishClimax;
                dir = SignalDirection.Up;
                bearish = true;
            }
            else if (c3.ClosePrice > c3.OpenPrice && ratioAbove > VolumeRatioThreshold)
            {
                kind = SignalKind.BullishClimax;
                dir = SignalDirection.Down;
            }
            else
                return null;

            if (c3.DateTime == _lastEmittedAt) return null;
            _lastEmittedAt = c3.DateTime;

            double volRatio = (double)c3.Volume / maxPrevVolume;
            int pct = distributed > 0
                ? (int)(100.0 * (bearish ? volumeBelow : volumeAbove) / distributed)
                : 0;

            string direction = bearish ? "вниз" : "вверх";
            string side = bearish ? "ниже" : "выше";

            string msg = string.Format(CultureInfo.InvariantCulture,
                "Объёмный выброс {0}: объём x{1:F1} ({2}), {3}% объёма {4} закрытия ({5}) — возможна кульминация и разворот",
                direction, volRatio, c3.Volume, pct, side, c3.ClosePrice);

            return new Signal
            {
                Time = c3.DateTime,
                Kind = kind,
                Direction = dir,
                Price = c3.ClosePrice,
                Strength = Math.Min(1.0, volRatio / (VolumeClimaxMultiplier * 2.0)),
                Message = msg,
                Details = string.Format(CultureInfo.InvariantCulture,
                    "c1Vol={0} c2Vol={1} c3Vol={2} volRatio={3:F2} pct={4} close={5}",
                    c1.Volume, c2.Volume, c3.Volume, volRatio, pct, c3.ClosePrice)
            };
        }
    }
}
