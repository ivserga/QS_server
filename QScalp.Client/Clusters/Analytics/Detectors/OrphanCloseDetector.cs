// ==========================================================================
//  OrphanCloseDetector.cs — «Сиротское» закрытие далеко от области торговли
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.Client.Clusters.Analytics.Detectors
{
    internal sealed class OrphanCloseDetector : ISignalDetector
    {
        public string Name => "OrphanClose";
        public bool Enabled { get; set; }

        public int MinRangeTicks { get; set; }
        public double MinGapShare { get; set; }
        public int AverageWindow { get; set; }
        public double MinVolumeRatio { get; set; }
        public double MinStrength { get; set; }
        public int CooldownBars { get; set; }

        DateTime _lastEmittedAt = DateTime.MinValue;
        int _barsSinceLastEmit = int.MaxValue;

        public OrphanCloseDetector()
        {
            Enabled = true;
            MinRangeTicks = 10;
            MinGapShare = 0.45;
            AverageWindow = 10;
            MinVolumeRatio = 0.80;
            MinStrength = 0.50;
            CooldownBars = 3;
        }

        public Signal Evaluate(ClusterHistory history)
        {
            if (history == null) return null;
            if (_barsSinceLastEmit != int.MaxValue) _barsSinceLastEmit++;
            if (_barsSinceLastEmit < CooldownBars) return null;

            var last = history.Last(0);
            if (last == null) return null;
            if (last.PriceStep <= 0) return null;
            if (last.Volume <= 0) return null;

            int range = last.MaxPrice - last.MinPrice;
            if (range < MinRangeTicks) return null;

            int gap = last.ClosePrice - last.ComPrice;
            double gapShare = Math.Abs((double)gap) / range;
            if (gapShare < MinGapShare) return null;

            bool closeBelowCom = gap < 0;
            bool closeAboveCom = gap > 0;
            if (!closeBelowCom && !closeAboveCom) return null;

            bool outsideTop3 = closeBelowCom
                ? last.ClosePrice < last.Top3From
                : last.ClosePrice > last.Top3To;
            if (!outsideTop3) return null;

            double avgVol = history.AverageVolumeBefore(AverageWindow);
            if (avgVol > 0 && last.Volume < avgVol * MinVolumeRatio) return null;

            double gapScore = Math.Min(1.0, gapShare);

            int wickAtClose = closeBelowCom
                ? Math.Max(0, Math.Min(last.OpenPrice, last.ClosePrice) - last.MinPrice)
                : Math.Max(0, last.MaxPrice - Math.Max(last.OpenPrice, last.ClosePrice));

            double wickShare = (double)wickAtClose / range;
            double wickScore = Math.Max(0, 1.0 - wickShare * 4.0);

            double volScore = avgVol > 0
                ? Math.Min(1.0, ((double)last.Volume / avgVol - MinVolumeRatio) /
                                 Math.Max(1e-6, 1.0 - MinVolumeRatio))
                : 0.3;

            double strength = 0.50 * gapScore + 0.30 * wickScore + 0.20 * Math.Max(0, volScore);
            if (strength < MinStrength) return null;

            if (last.Source.DateTime == _lastEmittedAt) return null;
            _lastEmittedAt = last.Source.DateTime;
            _barsSinceLastEmit = 0;

            var dir = closeBelowCom ? SignalDirection.Up : SignalDirection.Down;
            var kind = closeBelowCom ? SignalKind.OrphanCloseUp : SignalKind.OrphanCloseDown;

            return new Signal
            {
                Time = last.Source.DateTime,
                Kind = kind,
                Direction = dir,
                Price = last.ComPrice,
                Strength = strength,
                Message = FormatMessage(last, gap, gapShare, closeBelowCom),
                Details = FormatDetails(last, gap, gapShare, wickShare, avgVol, strength)
            };
        }

        static string FormatMessage(ClusterStats last, int gap, double gapShare, bool closeBelowCom)
        {
            string side = closeBelowCom ? "вверх" : "вниз";
            int gapPct = (int)Math.Round(gapShare * 100);
            return string.Format(CultureInfo.InvariantCulture,
                "Сиротское закрытие: close {0} в {1} тиках от COM {2} ({3}% диапазона) — ожидание возврата {4}",
                last.ClosePrice, Math.Abs(gap), last.ComPrice, gapPct, side);
        }

        static string FormatDetails(ClusterStats last, int gap, double gapShare,
            double wickShare, double avgVol, double strength)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "close={0} com={1} gap={2} gapShare={3:F2} top3=[{4}..{5}] posCom={6:F2} range={7} wickShareAtClose={8:F2} vol={9} avgVol={10:F0} strength={11:F2}",
                last.ClosePrice, last.ComPrice, gap, gapShare,
                last.Top3From, last.Top3To, last.PosCom,
                last.MaxPrice - last.MinPrice, wickShare,
                last.Volume, avgVol, strength);
        }
    }
}
