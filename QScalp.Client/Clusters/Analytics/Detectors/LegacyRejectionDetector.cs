// ==========================================================================
//  LegacyRejectionDetector.cs — Порт ClusterAnalyzer.AnalyzeRejection
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.Client.Clusters.Analytics.Detectors
{
    internal sealed class LegacyRejectionDetector : ISignalDetector
    {
        public string Name => "LegacyRejection";
        public bool Enabled { get; set; }

        public double RejectionCellRatioThreshold { get; set; }
        public int RejectionMinTouches { get; set; }

        DateTime _lastEmittedAt = DateTime.MinValue;

        public LegacyRejectionDetector()
        {
            Enabled = true;
            RejectionCellRatioThreshold = 0.10;
            RejectionMinTouches = 2;
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

            if (c3.Volume == 0) return null;

            long volAtMax = c3.GetCellVolume(c3.MaxPrice);
            long volAtMin = c3.GetCellVolume(c3.MinPrice);

            double ratioMax = (double)volAtMax / c3.Volume;
            double ratioMin = (double)volAtMin / c3.Volume;

            int resistanceTouches = 1;
            if (c1.MaxPrice == c3.MaxPrice) resistanceTouches++;
            if (c2.MaxPrice == c3.MaxPrice) resistanceTouches++;

            int supportTouches = 1;
            if (c1.MinPrice == c3.MinPrice) supportTouches++;
            if (c2.MinPrice == c3.MinPrice) supportTouches++;

            SignalKind kind = SignalKind.None;
            SignalDirection dir = SignalDirection.None;
            int level = 0;
            int touches = 0;
            long volAtLevel = 0;
            bool resistance = false;

            if (ratioMax >= RejectionCellRatioThreshold
                && c3.ClosePrice < c3.MaxPrice
                && resistanceTouches >= RejectionMinTouches)
            {
                kind = SignalKind.ResistanceRejection;
                dir = SignalDirection.Down;
                level = c3.MaxPrice;
                touches = resistanceTouches;
                volAtLevel = volAtMax;
                resistance = true;
            }
            else if (ratioMin >= RejectionCellRatioThreshold
                && c3.ClosePrice > c3.MinPrice
                && supportTouches >= RejectionMinTouches)
            {
                kind = SignalKind.SupportRejection;
                dir = SignalDirection.Up;
                level = c3.MinPrice;
                touches = supportTouches;
                volAtLevel = volAtMin;
            }
            else
                return null;

            if (c3.DateTime == _lastEmittedAt) return null;
            _lastEmittedAt = c3.DateTime;

            int pct = c3.Volume > 0 ? (int)(100.0 * volAtLevel / c3.Volume) : 0;
            string type = resistance ? "Сопротивление" : "Поддержка";

            string msg = string.Format(CultureInfo.InvariantCulture,
                "{0} на {1}: {2}% объёма ({3}) на уровне, касаний: {4}, закрытие {5}",
                type, level, pct, volAtLevel, touches, c3.ClosePrice);

            return new Signal
            {
                Time = c3.DateTime,
                Kind = kind,
                Direction = dir,
                Price = level,
                Strength = Math.Min(1.0, pct / 30.0),
                Message = msg,
                Details = string.Format(CultureInfo.InvariantCulture,
                    "level={0} touches={1} volAtLevel={2} ratio={3:F3} close={4}",
                    level, touches, volAtLevel,
                    resistance ? ratioMax : ratioMin,
                    c3.ClosePrice)
            };
        }
    }
}
