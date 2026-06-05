// ==========================================================================
//  LegacyAbsorptionDetector.cs — Порт оригинального ClusterAnalyzer.Analyze
// ==========================================================================
//  Анализ троек кластеров на BearishDivergence/BullishDivergence
//  (поглощение объёма продавцами/покупателями) — логика 1-в-1 из
//  View/Clusters/ClusterAnalyzer.cs, обёрнута в виде ISignalDetector.
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.Client.Clusters.Analytics.Detectors
{
    internal sealed class LegacyAbsorptionDetector : ISignalDetector
    {
        public string Name => "LegacyAbsorption";
        public bool Enabled { get; set; }

        public double VolumeRatioThreshold { get; set; }
        public double AbsorptionVolumeMultiplier { get; set; }

        DateTime _lastEmittedAt = DateTime.MinValue;

        public LegacyAbsorptionDetector()
        {
            Enabled = true;
            VolumeRatioThreshold = 0.68;
            AbsorptionVolumeMultiplier = 1.35;
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

            bool c1Up = c1.ClosePrice > c1.OpenPrice;
            bool c2Up = c2.ClosePrice > c2.OpenPrice;
            if (c1Up != c2Up) return null;

            bool uptrend = c1.ClosePrice < c2.ClosePrice && c3.ClosePrice > c1.ClosePrice;
            bool downtrend = c1.ClosePrice > c2.ClosePrice && c3.ClosePrice < c1.ClosePrice;
            if (!uptrend && !downtrend) return null;

            if (c3.Volume < c2.Volume * AbsorptionVolumeMultiplier || c3.Volume <= c1.Volume) return null;

            bool c3Reversed = uptrend
                ? c3.ClosePrice < c3.OpenPrice
                : c3.ClosePrice > c3.OpenPrice;

            int refPrice = c3Reversed ? c3.OpenPrice : c3.ClosePrice;

            c3.GetVolumeDistribution(refPrice, out long volumeAbove, out long volumeBelow);
            long distributed = volumeAbove + volumeBelow;
            if (distributed == 0) return null;

            SignalKind kind = SignalKind.None;
            SignalDirection dir = SignalDirection.None;
            int pct = 0;

            if (uptrend && (double)volumeAbove / distributed > VolumeRatioThreshold)
            {
                kind = SignalKind.BearishDivergence;
                dir = SignalDirection.Down;
                pct = (int)(100.0 * volumeAbove / distributed);
            }
            else if (downtrend && (double)volumeBelow / distributed > VolumeRatioThreshold)
            {
                kind = SignalKind.BullishDivergence;
                dir = SignalDirection.Up;
                pct = (int)(100.0 * volumeBelow / distributed);
            }
            else
                return null;

            if (c3.DateTime == _lastEmittedAt) return null;
            _lastEmittedAt = c3.DateTime;

            string refLabel = c3Reversed ? "открытия" : "закрытия";
            double volumeRatio = AbsorptionVolumeRatio(c1.Volume, c2.Volume, c3.Volume);
            double strength = AbsorptionStrength(volumeRatio);

            string msg = kind == SignalKind.BearishDivergence
                ? string.Format(CultureInfo.InvariantCulture,
                    "Поглощение продавцами: тренд вверх, объём x{0:F1} к двум предыдущим, сила {1:F2}, {2}% объёма выше {3} ({4}) — возможен разворот вниз",
                    volumeRatio, strength, pct, refLabel, refPrice)
                : string.Format(CultureInfo.InvariantCulture,
                    "Поглощение покупателями: тренд вниз, объём x{0:F1} к двум предыдущим, сила {1:F2}, {2}% объёма ниже {3} ({4}) — возможен разворот вверх",
                    volumeRatio, strength, pct, refLabel, refPrice);

            return new Signal
            {
                Time = c3.DateTime,
                Kind = kind,
                Direction = dir,
                Price = refPrice,
                Strength = strength,
                Message = msg,
                Details = string.Format(CultureInfo.InvariantCulture,
                    "trend={0} c1Vol={1} c2Vol={2} c3Vol={3} volRatio={4:F2} strength={5:F2} ref={6}({7}) volAbove={8} volBelow={9} pct={10}",
                    uptrend ? "up" : "down",
                    c1.Volume, c2.Volume, c3.Volume,
                    volumeRatio, strength,
                    refPrice, refLabel, volumeAbove, volumeBelow, pct)
            };
        }

        double AbsorptionVolumeRatio(long c1Volume, long c2Volume, long c3Volume)
        {
            long prevMax = c1Volume > c2Volume ? c1Volume : c2Volume;
            return prevMax > 0 ? c3Volume / (double)prevMax : 0;
        }

        double AbsorptionStrength(double volumeRatio)
        {
            if (volumeRatio <= 0)
                return 0;

            const double fullStrengthVolumeRatio = 3.0;
            double strength = 0.50
                + (volumeRatio - AbsorptionVolumeMultiplier)
                / (fullStrengthVolumeRatio - AbsorptionVolumeMultiplier) * 0.50;

            if (strength < 0) return 0;
            if (strength > 1) return 1;
            return strength;
        }
    }
}
