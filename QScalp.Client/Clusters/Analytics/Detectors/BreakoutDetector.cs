// ==========================================================================
//  BreakoutDetector.cs — Чистый пробой диапазона (вверх / вниз)
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.Client.Clusters.Analytics.Detectors
{
    internal sealed class BreakoutDetector : ISignalDetector
    {
        public string Name => "Breakout";
        public bool Enabled { get; set; }

        public int LookbackBars { get; set; }
        public int MinBreakoutTicks { get; set; }
        public double MinBodyRatio { get; set; }
        public double MinVolumeRatio { get; set; }
        public bool UseClusterUplift { get; set; }
        public double PosComFavorable { get; set; }
        public double MaxTop3Share { get; set; }
        public double MinStrength { get; set; }
        public int CooldownBars { get; set; }

        DateTime _lastEmittedAt = DateTime.MinValue;
        int _barsSinceLastEmit = int.MaxValue;

        public BreakoutDetector()
        {
            Enabled = true;
            LookbackBars = 10;
            MinBreakoutTicks = 2;
            MinBodyRatio = 0.45;
            MinVolumeRatio = 1.00;
            UseClusterUplift = true;
            PosComFavorable = 0.60;
            MaxTop3Share = 0.55;
            MinStrength = 0.50;
            CooldownBars = 3;
        }

        public Signal Evaluate(ClusterHistory history)
        {
            if (history == null) return null;
            if (history.Count < LookbackBars + 1) return null;

            if (_barsSinceLastEmit != int.MaxValue) _barsSinceLastEmit++;
            if (_barsSinceLastEmit < CooldownBars) return null;

            var last = history.Last(0);
            if (last == null || last.Range <= 0 || last.Volume == 0) return null;

            var up = TryEvaluate(history, last, true);
            if (up != null) return Commit(up, last.Source.DateTime);

            var down = TryEvaluate(history, last, false);
            if (down != null) return Commit(down, last.Source.DateTime);

            return null;
        }

        Signal TryEvaluate(ClusterHistory history, ClusterStats last, bool up)
        {
            int priorExtreme = up
                ? WindowMath.HighestHighExcludingLast(history, LookbackBars, 1)
                : WindowMath.LowestLowExcludingLast(history, LookbackBars, 1);

            if (up)
            {
                if (priorExtreme == int.MinValue) return null;
                if (last.ClosePrice < priorExtreme + MinBreakoutTicks) return null;
            }
            else
            {
                if (priorExtreme == int.MaxValue) return null;
                if (last.ClosePrice > priorExtreme - MinBreakoutTicks) return null;
            }

            int body = last.ClosePrice - last.OpenPrice;
            if (up && body <= 0) return null;
            if (!up && body >= 0) return null;

            double bodyRatio = Math.Abs(body) / (double)last.Range;
            if (bodyRatio < MinBodyRatio) return null;

            double avgVol = history.AverageVolumeBefore(LookbackBars);
            if (avgVol > 0 && last.Volume < avgVol * MinVolumeRatio) return null;

            int breakDepth = up
                ? last.ClosePrice - priorExtreme
                : priorExtreme - last.ClosePrice;

            double depthScore = Math.Min(1.0, breakDepth / Math.Max(1.0, MinBreakoutTicks * 5.0));
            double bodyScore = Math.Min(1.0, (bodyRatio - MinBodyRatio) / Math.Max(1e-6, 1.0 - MinBodyRatio));
            double volScore = avgVol > 0
                ? Math.Min(1.0, ((double)last.Volume / avgVol - MinVolumeRatio) / Math.Max(1e-6, MinVolumeRatio))
                : 0.3;

            double baseStrength = 0.40 * depthScore + 0.35 * bodyScore + 0.25 * Math.Max(0, volScore);

            double uplift = 0;
            if (UseClusterUplift)
            {
                bool posOk = up ? (last.PosCom >= PosComFavorable) : (last.PosCom <= 1.0 - PosComFavorable);
                if (posOk) uplift += 0.12;

                if (last.Top3Share <= MaxTop3Share) uplift += 0.05;
                else uplift -= 0.15;

                if (up && last.Skewness > 0) uplift += 0.05;
                if (!up && last.Skewness < 0) uplift += 0.05;

                if (last.Shape == ProfileShape.Thin) uplift -= 0.10;
            }

            double strength = Math.Min(1.0, Math.Max(0, baseStrength + uplift));
            if (strength < MinStrength) return null;

            return new Signal
            {
                Time = last.Source.DateTime,
                Kind = up ? SignalKind.BreakoutUp : SignalKind.BreakoutDown,
                Direction = up ? SignalDirection.Up : SignalDirection.Down,
                Price = priorExtreme,
                Strength = strength,
                Message = FormatMessage(up, priorExtreme, last.ClosePrice, bodyRatio, last.Volume, avgVol),
                Details = FormatDetails(up, priorExtreme, last, breakDepth, bodyRatio, avgVol, baseStrength, uplift)
            };
        }

        Signal Commit(Signal s, DateTime barTime)
        {
            if (barTime == _lastEmittedAt) return null;
            _lastEmittedAt = barTime;
            _barsSinceLastEmit = 0;
            return s;
        }

        static string FormatMessage(bool up, int priorExtreme, int close, double bodyRatio,
            long volume, double avgVol)
        {
            double volX = avgVol > 0 ? (double)volume / avgVol : 0;
            string side = up ? "вверх" : "вниз";
            string action = up ? "выше" : "ниже";
            return string.Format(CultureInfo.InvariantCulture,
                "Пробой {0}: close {1} {2} уровня {3}, тело {4}% диапазона, объём x{5:F1}",
                side, close, action, priorExtreme, (int)Math.Round(bodyRatio * 100), volX);
        }

        static string FormatDetails(bool up, int priorExtreme, ClusterStats s,
            int breakDepth, double bodyRatio, double avgVol, double baseStrength, double uplift)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "dir={0} priorExt={1} close={2} breakDepth={3} body={4:F2} vol={5} avgVol={6:F0} posCom={7:F2} top3Share={8:F2} skew={9:F2} shape={10} base={11:F2} uplift={12:F2}",
                up ? "up" : "down", priorExtreme, s.ClosePrice,
                breakDepth, bodyRatio, s.Volume, avgVol,
                s.PosCom, s.Top3Share, s.Skewness, s.Shape, baseStrength, uplift);
        }
    }
}
