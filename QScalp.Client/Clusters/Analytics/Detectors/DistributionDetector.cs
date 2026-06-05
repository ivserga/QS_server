// ==========================================================================
//  DistributionDetector.cs — Распределение (раздача) у вершины тренда
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.Client.Clusters.Analytics.Detectors
{
    internal sealed class DistributionDetector : ISignalDetector
    {
        public string Name => "Distribution";
        public bool Enabled { get; set; }

        public int Lookback { get; set; }
        public int MinSwingDiffTicks { get; set; }
        public double MaxVolumeSlope { get; set; }
        public int MinHighAgeBars { get; set; }
        public bool UseClusterUplift { get; set; }
        public double PosComLowThreshold { get; set; }
        public double MinShareLowPosCom { get; set; }
        public double MinStrength { get; set; }
        public int CooldownBars { get; set; }

        DateTime _lastEmittedAt = DateTime.MinValue;
        int _barsSinceLastEmit = int.MaxValue;

        public DistributionDetector()
        {
            Enabled = true;
            Lookback = 12;
            MinSwingDiffTicks = 2;
            MaxVolumeSlope = -0.03;
            MinHighAgeBars = 2;
            UseClusterUplift = true;
            PosComLowThreshold = 0.40;
            MinShareLowPosCom = 0.45;
            MinStrength = 0.50;
            CooldownBars = 5;
        }

        public Signal Evaluate(ClusterHistory history)
        {
            if (history == null) return null;
            if (history.Count < Lookback) return null;

            if (_barsSinceLastEmit != int.MaxValue) _barsSinceLastEmit++;
            if (_barsSinceLastEmit < CooldownBars) return null;

            var last = history.Last(0);
            if (last == null) return null;

            if (!WindowMath.TwoHalvesHighs(history, Lookback, out int hi1, out int hi2)) return null;

            int swingDiff = hi1 - hi2;
            if (swingDiff < MinSwingDiffTicks) return null;

            double volSlope = WindowMath.SlopePctVolume(history, 0, Lookback);
            if (volSlope > MaxVolumeSlope) return null;

            int hh = WindowMath.HighestHigh(history, 0, Lookback);
            int ll = WindowMath.LowestLow(history, 0, Lookback);
            if (hh == int.MinValue || ll == int.MaxValue) return null;
            double mid = (hh + ll) * 0.5;
            if (last.ClosePrice >= mid) return null;

            int hhAge = AgeOfHighestHigh(history, Lookback, hh);
            if (hhAge < MinHighAgeBars) return null;

            double swingScore = Math.Min(1.0, swingDiff / Math.Max(1.0, hh * 0.005));
            double volScore = Math.Min(1.0, (MaxVolumeSlope - volSlope) / Math.Max(1e-6, Math.Abs(MaxVolumeSlope) * 2.0));
            double midScore = Math.Min(1.0, (mid - last.ClosePrice) / Math.Max(1.0, (hh - ll) * 0.5));

            double baseStrength = 0.40 * swingScore + 0.40 * Math.Max(0, volScore) + 0.20 * Math.Max(0, midScore);

            double uplift = 0;
            double sharePosLow = 0;
            double avgSkew = 0;

            if (UseClusterUplift)
            {
                sharePosLow = WindowMath.SharePosComBelow(history, Lookback, PosComLowThreshold);
                if (sharePosLow >= MinShareLowPosCom)
                    uplift += 0.15 * Math.Min(1.0, (sharePosLow - MinShareLowPosCom) / Math.Max(1e-6, 1.0 - MinShareLowPosCom) + 0.5);

                avgSkew = WindowMath.AverageSkewness(history, Lookback);
                if (avgSkew < 0) uplift += 0.10 * Math.Min(1.0, -avgSkew / 0.5);

                double avgVol = history.AverageVolumeBefore(Lookback - 1);
                if (avgVol > 0 && last.Volume < avgVol * 0.85) uplift += 0.05;
            }

            double strength = Math.Min(1.0, baseStrength + uplift);
            if (strength < MinStrength) return null;

            if (last.Source.DateTime == _lastEmittedAt) return null;
            _lastEmittedAt = last.Source.DateTime;
            _barsSinceLastEmit = 0;

            return new Signal
            {
                Time = last.Source.DateTime,
                Kind = SignalKind.Distribution,
                Direction = SignalDirection.Down,
                Price = ll,
                Strength = strength,
                Message = FormatMessage(hi1, hi2, volSlope, ll, sharePosLow),
                Details = FormatDetails(hi1, hi2, hh, ll, hhAge, volSlope, sharePosLow, avgSkew, baseStrength, uplift)
            };
        }

        static int AgeOfHighestHigh(ClusterHistory history, int count, int hh)
        {
            for (int i = 0; i < count; i++)
            {
                var s = history.Last(i);
                if (s == null) return count;
                if (s.MaxPrice == hh) return i;
            }
            return count;
        }

        static string FormatMessage(int hi1, int hi2, double volSlope, int targetLow, double sharePosLow)
        {
            int volPct = (int)Math.Round(volSlope * 100);
            int posLowPct = (int)Math.Round(sharePosLow * 100);
            return string.Format(CultureInfo.InvariantCulture,
                "Распределение у вершины: lower_highs {0}→{1}, объём {2:+0;-0;0}%/бар, COM внизу баров {3}% — цель {4}",
                hi1, hi2, volPct, posLowPct, targetLow);
        }

        static string FormatDetails(int hi1, int hi2, int hh, int ll, int hhAge,
            double volSlope, double sharePosLow, double avgSkew,
            double baseStrength, double uplift)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "hi1={0} hi2={1} hh={2} ll={3} hhAge={4} volSlope={5:F3} posLowShare={6:F2} avgSkew={7:F2} base={8:F2} uplift={9:F2}",
                hi1, hi2, hh, ll, hhAge, volSlope, sharePosLow, avgSkew, baseStrength, uplift);
        }
    }
}
