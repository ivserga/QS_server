// ==========================================================================
//  ClimaxDetector.cs — Продающий/покупающий климакс
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.Client.Clusters.Analytics.Detectors
{
    internal sealed class ClimaxDetector : ISignalDetector
    {
        public string Name => "Climax";
        public bool Enabled { get; set; }

        public double MinTop3Share { get; set; }
        public double VolumeMultiplier { get; set; }
        public double MinDensityMultiplier { get; set; }
        public double EdgePosComTop { get; set; }
        public double EdgePosComBottom { get; set; }
        public int AverageWindow { get; set; }
        public int CooldownBars { get; set; }

        DateTime _lastEmittedAt = DateTime.MinValue;
        int _barsSinceLastEmit = int.MaxValue;

        public ClimaxDetector()
        {
            Enabled = true;
            MinTop3Share = 0.40;
            VolumeMultiplier = 1.0;
            MinDensityMultiplier = 1.3;
            EdgePosComTop = 0.75;
            EdgePosComBottom = 0.25;
            AverageWindow = 5;
            CooldownBars = 3;
        }

        public Signal Evaluate(ClusterHistory history)
        {
            var s = history.Last(0);
            if (s == null) return null;

            if (_barsSinceLastEmit != int.MaxValue) _barsSinceLastEmit++;
            if (s.Volume == 0 || s.Range <= 0) return null;
            if (s.Top3Share < MinTop3Share) return null;

            bool selling = s.PosCom <= EdgePosComBottom;
            bool buying = s.PosCom >= EdgePosComTop;
            if (!selling && !buying) return null;

            double avgVol = history.AverageVolumeBefore(AverageWindow);
            if (avgVol > 0 && s.Volume < avgVol * VolumeMultiplier) return null;

            double avgRange = history.AverageRangeBefore(AverageWindow);
            double density = (double)s.Volume / Math.Max(1, s.Range);
            double avgDensity = avgRange > 0 ? (avgVol / avgRange) : 0;

            if (avgDensity > 0 && density < avgDensity * MinDensityMultiplier) return null;

            if (s.Source.DateTime == _lastEmittedAt) return null;
            if (_barsSinceLastEmit < CooldownBars) return null;

            _lastEmittedAt = s.Source.DateTime;
            _barsSinceLastEmit = 0;

            double top3Score = Math.Min(1.0, (s.Top3Share - MinTop3Share) / Math.Max(1e-6, 1.0 - MinTop3Share));
            double densScore = avgDensity > 0
                ? Math.Min(1.0, (density / avgDensity - MinDensityMultiplier) / MinDensityMultiplier)
                : 0.3;
            double strength = 0.6 * top3Score + 0.4 * Math.Max(0, densScore);

            return new Signal
            {
                Time = s.Source.DateTime,
                Kind = selling ? SignalKind.SellingClimax : SignalKind.BuyingClimax,
                Direction = selling ? SignalDirection.Up : SignalDirection.Down,
                Price = s.ComPrice,
                Strength = strength,
                Message = FormatMessage(s, selling, density, avgDensity),
                Details = FormatDetails(s, density, avgDensity, avgVol)
            };
        }

        static string FormatMessage(ClusterStats s, bool selling, double density, double avgDensity)
        {
            string dir = selling ? "продаж" : "покупок";
            string react = selling ? "возможен откат вверх" : "возможен откат вниз";
            double densityRatio = avgDensity > 0 ? density / avgDensity : 0;

            return string.Format(CultureInfo.InvariantCulture,
                "Климакс {0}: {1}% объёма в зоне {2}-{3} (x{4:F1} плотности), COM {5} — {6}",
                dir, (int)Math.Round(s.Top3Share * 100),
                s.Top3From, s.Top3To, densityRatio, s.ComPrice, react);
        }

        static string FormatDetails(ClusterStats s, double density, double avgDensity, double avgVol)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "com={0} top3Share={1:F2} top3={2}-{3} vol={4} avgVol={5:F0} density={6:F1} avgDensity={7:F1} posCom={8:F2} shape={9}",
                s.ComPrice, s.Top3Share, s.Top3From, s.Top3To,
                s.Volume, avgVol, density, avgDensity, s.PosCom, s.Shape);
        }
    }
}
