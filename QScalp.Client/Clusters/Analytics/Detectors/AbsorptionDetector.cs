// ==========================================================================
//  AbsorptionDetector.cs — Абсорбция на границе диапазона
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.Client.Clusters.Analytics.Detectors
{
    internal sealed class AbsorptionDetector : ISignalDetector
    {
        public string Name => "Absorption";
        public bool Enabled { get; set; }

        public double MinTop3Share { get; set; }
        public double EdgeThreshold { get; set; }
        public double TailMaxShare { get; set; }
        public double VolumeMultiplier { get; set; }
        public int AverageWindow { get; set; }

        DateTime _lastEmittedAt = DateTime.MinValue;

        public AbsorptionDetector()
        {
            Enabled = true;
            MinTop3Share = 0.30;
            EdgeThreshold = 0.85;
            TailMaxShare = 0.05;
            VolumeMultiplier = 1.4;
            AverageWindow = 5;
        }

        public Signal Evaluate(ClusterHistory history)
        {
            var s = history.Last(0);
            if (s == null) return null;
            if (s.Volume == 0 || s.Range <= 0) return null;

            double avg = history.AverageVolumeBefore(AverageWindow);
            if (avg > 0 && s.Volume < avg * VolumeMultiplier) return null;

            bool atTop = s.PosCom >= EdgeThreshold;
            bool atBottom = s.PosCom <= (1.0 - EdgeThreshold);
            if (!atTop && !atBottom) return null;

            if (s.Top3Share < MinTop3Share) return null;

            double tailShare = s.ShareBeyondCom(atTop);
            if (tailShare > TailMaxShare) return null;

            if (atTop && s.ClosePrice > s.ComPrice) return null;
            if (atBottom && s.ClosePrice < s.ComPrice) return null;

            if (s.Source.DateTime == _lastEmittedAt) return null;
            _lastEmittedAt = s.Source.DateTime;

            double top3Score = Math.Min(1.0, (s.Top3Share - MinTop3Share) / Math.Max(1e-6, 1.0 - MinTop3Share));
            double tailScore = 1.0 - (tailShare / Math.Max(1e-6, TailMaxShare));
            double volScore = avg > 0 ? Math.Min(1.0, (s.Volume / avg - VolumeMultiplier) / VolumeMultiplier) : 0.3;
            double strength = 0.5 * top3Score + 0.3 * tailScore + 0.2 * Math.Max(0, volScore);

            return new Signal
            {
                Time = s.Source.DateTime,
                Kind = atTop ? SignalKind.AbsorptionSell : SignalKind.AbsorptionBuy,
                Direction = atTop ? SignalDirection.Down : SignalDirection.Up,
                Price = s.ComPrice,
                Strength = strength,
                Message = FormatMessage(s, atTop, tailShare),
                Details = FormatDetails(s, tailShare, avg)
            };
        }

        static string FormatMessage(ClusterStats s, bool atTop, double tailShare)
        {
            string side = atTop ? "продавца" : "покупателя";
            string dir = atTop ? "вниз" : "вверх";
            return string.Format(CultureInfo.InvariantCulture,
                "Абсорбция {0} на {1}: Top3 {2}%, за COM {3}%, ожидание отката {4}",
                side, s.ComPrice,
                (int)Math.Round(s.Top3Share * 100),
                (int)Math.Round(tailShare * 100), dir);
        }

        static string FormatDetails(ClusterStats s, double tailShare, double avg)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "com={0} top3Share={1:F2} posCom={2:F2} tail={3:F3} vol={4} avg={5:F0} close={6} shape={7}",
                s.ComPrice, s.Top3Share, s.PosCom, tailShare,
                s.Volume, avg, s.ClosePrice, s.Shape);
        }
    }
}
