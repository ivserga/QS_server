// ==========================================================================
//  AccumulationDetector.cs — Накопление у дна тренда
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.Client.Clusters.Analytics.Detectors
{
    internal sealed class AccumulationDetector : ISignalDetector
    {
        public string Name => "Accumulation";
        public bool Enabled { get; set; }

        public int Lookback { get; set; }
        public int MinSwingDiffTicks { get; set; }
        public double MinVolumeSlope { get; set; }
        public int MinLowAgeBars { get; set; }
        public bool UseClusterUplift { get; set; }
        public double PosComHighThreshold { get; set; }
        public double MinShareHighPosCom { get; set; }
        public double MinStrength { get; set; }
        public int CooldownBars { get; set; }

        DateTime _lastEmittedAt = DateTime.MinValue;
        int _barsSinceLastEmit = int.MaxValue;

        public AccumulationDetector()
        {
            Enabled = true;
            Lookback = 12;
            MinSwingDiffTicks = 2;
            MinVolumeSlope = 0.00;
            MinLowAgeBars = 2;
            UseClusterUplift = true;
            PosComHighThreshold = 0.60;
            MinShareHighPosCom = 0.45;
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

            if (!WindowMath.TwoHalvesLows(history, Lookback, out int lo1, out int lo2)) return null;

            int swingDiff = lo2 - lo1;
            if (swingDiff < MinSwingDiffTicks) return null;

            double volSlope = WindowMath.SlopePctVolume(history, 0, Lookback);
            if (volSlope < MinVolumeSlope) return null;

            int hh = WindowMath.HighestHigh(history, 0, Lookback);
            int ll = WindowMath.LowestLow(history, 0, Lookback);
            if (hh == int.MinValue || ll == int.MaxValue) return null;
            double mid = (hh + ll) * 0.5;
            if (last.ClosePrice <= mid) return null;

            int llAge = AgeOfLowestLow(history, Lookback, ll);
            if (llAge < MinLowAgeBars) return null;

            double swingScore = Math.Min(1.0, swingDiff / Math.Max(1.0, ll * 0.005));
            double volScore = volSlope >= 0
                ? Math.Min(1.0, volSlope / 0.05 + 0.4)
                : 0.2;
            double midScore = Math.Min(1.0, (last.ClosePrice - mid) / Math.Max(1.0, (hh - ll) * 0.5));

            double baseStrength = 0.40 * swingScore + 0.30 * volScore + 0.30 * Math.Max(0, midScore);

            double uplift = 0;
            double sharePosHigh = 0;
            double avgSkew = 0;

            if (UseClusterUplift)
            {
                sharePosHigh = WindowMath.SharePosComAbove(history, Lookback, PosComHighThreshold);
                if (sharePosHigh >= MinShareHighPosCom)
                    uplift += 0.15 * Math.Min(1.0, (sharePosHigh - MinShareHighPosCom) / Math.Max(1e-6, 1.0 - MinShareHighPosCom) + 0.5);

                avgSkew = WindowMath.AverageSkewness(history, Lookback);
                if (avgSkew > 0) uplift += 0.10 * Math.Min(1.0, avgSkew / 0.5);

                double avgVol = history.AverageVolumeBefore(Lookback - 1);
                if (avgVol > 0 && last.Volume > avgVol * 1.10) uplift += 0.05;
            }

            double strength = Math.Min(1.0, baseStrength + uplift);
            if (strength < MinStrength) return null;

            if (last.Source.DateTime == _lastEmittedAt) return null;
            _lastEmittedAt = last.Source.DateTime;
            _barsSinceLastEmit = 0;

            return new Signal
            {
                Time = last.Source.DateTime,
                Kind = SignalKind.Accumulation,
                Direction = SignalDirection.Up,
                Price = hh,
                Strength = strength,
                Message = FormatMessage(lo1, lo2, volSlope, hh, sharePosHigh),
                Details = FormatDetails(lo1, lo2, hh, ll, llAge, volSlope, sharePosHigh, avgSkew, baseStrength, uplift)
            };
        }

        static int AgeOfLowestLow(ClusterHistory history, int count, int ll)
        {
            for (int i = 0; i < count; i++)
            {
                var s = history.Last(i);
                if (s == null) return count;
                if (s.MinPrice == ll) return i;
            }
            return count;
        }

        static string FormatMessage(int lo1, int lo2, double volSlope, int targetHigh, double sharePosHigh)
        {
            int volPct = (int)Math.Round(volSlope * 100);
            int posHighPct = (int)Math.Round(sharePosHigh * 100);
            return string.Format(CultureInfo.InvariantCulture,
                "Накопление у дна: higher_lows {0}→{1}, объём {2:+0;-0;0}%/бар, COM вверху баров {3}% — цель {4}",
                lo1, lo2, volPct, posHighPct, targetHigh);
        }

        static string FormatDetails(int lo1, int lo2, int hh, int ll, int llAge,
            double volSlope, double sharePosHigh, double avgSkew,
            double baseStrength, double uplift)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "lo1={0} lo2={1} hh={2} ll={3} llAge={4} volSlope={5:F3} posHighShare={6:F2} avgSkew={7:F2} base={8:F2} uplift={9:F2}",
                lo1, lo2, hh, ll, llAge, volSlope, sharePosHigh, avgSkew, baseStrength, uplift);
        }
    }
}
