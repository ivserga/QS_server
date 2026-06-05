// ==========================================================================
//  DoubleTopDetector.cs — Двойная вершина (M-pattern)
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.Client.Clusters.Analytics.Detectors
{
    internal sealed class DoubleTopDetector : ISignalDetector
    {
        public string Name => "DoubleTop";
        public bool Enabled { get; set; }

        public int Lookback { get; set; }
        public int PeakLeftRight { get; set; }
        public int MaxPeakDiffTicks { get; set; }
        public int MinBarsBetweenPeaks { get; set; }
        public int MinCorrectionTicks { get; set; }
        public int MinSecondPeakAgeBars { get; set; }
        public int MaxSecondPeakAgeBars { get; set; }
        public int MinPostPeakDropTicks { get; set; }
        public bool UseClusterUplift { get; set; }
        public double MaxVolumeRatio { get; set; }
        public double VolumeCenterTolTicks { get; set; }
        public double MinStrength { get; set; }
        public int CooldownBars { get; set; }

        DateTime _lastEmittedAt = DateTime.MinValue;
        int _barsSinceLastEmit = int.MaxValue;

        public DoubleTopDetector()
        {
            Enabled = true;
            Lookback = 25;
            PeakLeftRight = 2;
            MaxPeakDiffTicks = 5;
            MinBarsBetweenPeaks = 3;
            MinCorrectionTicks = 8;
            MinSecondPeakAgeBars = 1;
            MaxSecondPeakAgeBars = 5;
            MinPostPeakDropTicks = 3;
            UseClusterUplift = true;
            MaxVolumeRatio = 1.10;
            VolumeCenterTolTicks = 0.5;
            MinStrength = 0.50;
            CooldownBars = 8;
        }

        public Signal Evaluate(ClusterHistory history)
        {
            if (history == null) return null;
            if (history.Count < Lookback) return null;

            if (_barsSinceLastEmit != int.MaxValue) _barsSinceLastEmit++;
            if (_barsSinceLastEmit < CooldownBars) return null;

            var last = history.Last(0);
            if (last == null) return null;

            var peaks = WindowMath.FindLocalPeakIndices(history, Lookback, PeakLeftRight);
            if (peaks.Count < 2) return null;

            int idx2 = peaks[peaks.Count - 1];
            int idx1 = peaks[peaks.Count - 2];

            if (idx2 < MinSecondPeakAgeBars) return null;
            if (idx2 > MaxSecondPeakAgeBars) return null;

            var peak1 = history.Last(idx1);
            var peak2 = history.Last(idx2);
            if (peak1 == null || peak2 == null) return null;

            int diff = Math.Abs(peak1.MaxPrice - peak2.MaxPrice);
            if (diff > MaxPeakDiffTicks) return null;

            int barsBetween = idx1 - idx2;
            if (barsBetween < MinBarsBetweenPeaks) return null;

            int troughLow = WindowMath.LowestLowBetween(history, idx1 - 1, idx2 + 1);
            if (troughLow == int.MaxValue) return null;

            int peakLevel = Math.Max(peak1.MaxPrice, peak2.MaxPrice);
            int correction = peakLevel - troughLow;
            if (correction < MinCorrectionTicks) return null;

            int postDrop = peak2.MaxPrice - last.ClosePrice;
            if (postDrop < MinPostPeakDropTicks) return null;

            double diffScore = 1.0 - Math.Min(1.0, diff / Math.Max(1.0, (double)MaxPeakDiffTicks));
            double correctionScore = Math.Min(1.0, (correction - MinCorrectionTicks) / Math.Max(1.0, (double)MinCorrectionTicks * 2));
            double dropScore = Math.Min(1.0, postDrop / Math.Max(1.0, (double)MinPostPeakDropTicks * 3));

            double baseStrength = 0.40 * diffScore + 0.35 * correctionScore + 0.25 * dropScore;

            double uplift = 0;
            if (UseClusterUplift)
            {
                double volCenter1 = WindowMath.VolumeCenter(peak1);
                double volCenter2 = WindowMath.VolumeCenter(peak2);
                double volTol = VolumeCenterTolTicks * Math.Max(1, last.PriceStep);
                if (volCenter2 <= volCenter1 + volTol) uplift += 0.10;
                else uplift -= 0.05;

                if (peak1.Volume > 0)
                {
                    double volRatio = (double)peak2.Volume / peak1.Volume;
                    if (volRatio <= MaxVolumeRatio) uplift += 0.10;
                    if (volRatio < 0.80) uplift += 0.05;
                }

                if (peak2.PosCom < peak1.PosCom) uplift += 0.05;
                if (peak2.Top3Share > peak1.Top3Share + 0.05) uplift += 0.05;
            }

            double strength = Math.Min(1.0, Math.Max(0, baseStrength + uplift));
            if (strength < MinStrength) return null;

            if (last.Source.DateTime == _lastEmittedAt) return null;
            _lastEmittedAt = last.Source.DateTime;
            _barsSinceLastEmit = 0;

            return new Signal
            {
                Time = last.Source.DateTime,
                Kind = SignalKind.DoubleTop,
                Direction = SignalDirection.Down,
                Price = troughLow,
                Strength = strength,
                Message = FormatMessage(peak1, peak2, troughLow, idx2),
                Details = FormatDetails(peak1, peak2, idx1, idx2, troughLow, correction, postDrop, baseStrength, uplift)
            };
        }

        static string FormatMessage(ClusterStats peak1, ClusterStats peak2, int neckline, int peak2AgeBars)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "Двойная вершина: пики {0}/{1} ({2} бар(ов) назад), объём {3}/{4}, цель {5}",
                peak1.MaxPrice, peak2.MaxPrice, peak2AgeBars,
                FormatVolume(peak1.Volume), FormatVolume(peak2.Volume), neckline);
        }

        static string FormatDetails(ClusterStats peak1, ClusterStats peak2,
            int idx1, int idx2, int neckline, int correction, int postDrop,
            double baseStrength, double uplift)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "p1Hi={0} p2Hi={1} idx1={2} idx2={3} neckline={4} correction={5} postDrop={6} p1Vol={7} p2Vol={8} p1Com={9} p2Com={10} p1VolC={11:F2} p2VolC={12:F2} p1PosCom={13:F2} p2PosCom={14:F2} base={15:F2} uplift={16:F2}",
                peak1.MaxPrice, peak2.MaxPrice, idx1, idx2, neckline, correction, postDrop,
                peak1.Volume, peak2.Volume, peak1.ComPrice, peak2.ComPrice,
                WindowMath.VolumeCenter(peak1), WindowMath.VolumeCenter(peak2),
                peak1.PosCom, peak2.PosCom, baseStrength, uplift);
        }

        static string FormatVolume(long v)
        {
            if (v >= 1_000_000) return (v / 1_000_000.0).ToString("F1", CultureInfo.InvariantCulture) + "M";
            if (v >= 1_000) return (v / 1000.0).ToString("F1", CultureInfo.InvariantCulture) + "k";
            return v.ToString(CultureInfo.InvariantCulture);
        }
    }
}
