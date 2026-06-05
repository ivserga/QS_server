// ==========================================================================
//  VReversalDetector.cs — V-разворот после абсорбции у низа/верха
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.Client.Clusters.Analytics.Detectors
{
    internal sealed class VReversalDetector : ISignalDetector
    {
        public string Name => "VReversal";
        public bool Enabled { get; set; }

        public int MinRun { get; set; }
        public double AbsorptionEdge { get; set; }
        public double AbsorptionTop3Share { get; set; }
        public double MinBodyRatio { get; set; }
        public double MinCenterOfMassShift { get; set; }

        DateTime _lastEmittedAt = DateTime.MinValue;

        public VReversalDetector()
        {
            Enabled = true;
            MinRun = 2;
            AbsorptionEdge = 0.35;
            AbsorptionTop3Share = 0.05;
            MinBodyRatio = 0.35;
            MinCenterOfMassShift = 0.15;
        }

        public Signal Evaluate(ClusterHistory history)
        {
            var curr = history.Last(0);
            var prev = history.Last(1);
            if (curr == null || prev == null) return null;
            if (curr.Range <= 0 || prev.Range <= 0 || curr.Volume == 0 || prev.Volume == 0) return null;

            var up = TryEvaluate(history, curr, prev, bearishRun: true);
            if (up != null) return Commit(up, curr.Source.DateTime);

            var down = TryEvaluate(history, curr, prev, bearishRun: false);
            if (down != null) return Commit(down, curr.Source.DateTime);

            return null;
        }

        Signal TryEvaluate(ClusterHistory history, ClusterStats curr, ClusterStats prev, bool bearishRun)
        {
            int run = bearishRun ? history.CountBearishRunFrom(1) : history.CountBullishRunFrom(1);
            if (run < MinRun) return null;

            double absorptionEdgeLow = AbsorptionEdge;
            double absorptionEdgeHigh = 1.0 - AbsorptionEdge;

            if (bearishRun)
            {
                if (prev.PosCom > absorptionEdgeLow) return null;
            }
            else
            {
                if (prev.PosCom < absorptionEdgeHigh) return null;
            }

            if (prev.Top3Share < AbsorptionTop3Share) return null;

            int body = curr.ClosePrice - curr.OpenPrice;
            if (bearishRun)
            {
                if (body <= 0) return null;
                if (curr.MinPrice < prev.MinPrice) return null;
            }
            else
            {
                if (body >= 0) return null;
                if (curr.MaxPrice > prev.MaxPrice) return null;
            }

            double bodyRatio = Math.Abs(body) / (double)curr.Range;
            if (bodyRatio < MinBodyRatio) return null;

            double com0 = curr.CenterOfMass;
            double com1 = prev.CenterOfMass;
            double reference = Math.Max(curr.Range, prev.Range);
            double shiftNorm = reference > 0 ? (com0 - com1) / reference : 0;

            if (bearishRun && shiftNorm < MinCenterOfMassShift) return null;
            if (!bearishRun && -shiftNorm < MinCenterOfMassShift) return null;

            double runScore = Math.Min(1.0, (run - MinRun + 1) / 3.0);
            double bodyScore = Math.Min(1.0, (bodyRatio - MinBodyRatio) / Math.Max(1e-6, 1.0 - MinBodyRatio));
            double edgeScore = bearishRun
                ? Math.Min(1.0, (absorptionEdgeLow - prev.PosCom) / Math.Max(1e-6, absorptionEdgeLow))
                : Math.Min(1.0, (prev.PosCom - absorptionEdgeHigh) / Math.Max(1e-6, 1.0 - absorptionEdgeHigh));

            double strength = 0.35 * runScore + 0.35 * bodyScore + 0.30 * Math.Max(0, edgeScore);

            return new Signal
            {
                Time = curr.Source.DateTime,
                Kind = bearishRun ? SignalKind.VReversalUp : SignalKind.VReversalDown,
                Direction = bearishRun ? SignalDirection.Up : SignalDirection.Down,
                Price = prev.ComPrice,
                Strength = strength,
                Message = FormatMessage(curr, prev, bearishRun, run, bodyRatio),
                Details = FormatDetails(curr, prev, bearishRun, run, bodyRatio, shiftNorm)
            };
        }

        Signal Commit(Signal s, DateTime barTime)
        {
            if (barTime == _lastEmittedAt) return null;
            _lastEmittedAt = barTime;
            return s;
        }

        static string FormatMessage(ClusterStats curr, ClusterStats prev,
            bool bearishRun, int run, double bodyRatio)
        {
            if (bearishRun)
                return string.Format(CultureInfo.InvariantCulture,
                    "V-разворот вверх: {0} медвежьих, абсорбция у {1}, разворот с телом {2}% диапазона, close {3}",
                    run, prev.ComPrice, (int)Math.Round(bodyRatio * 100), curr.ClosePrice);

            return string.Format(CultureInfo.InvariantCulture,
                "^-разворот вниз: {0} бычьих, абсорбция у {1}, разворот с телом {2}% диапазона, close {3}",
                run, prev.ComPrice, (int)Math.Round(bodyRatio * 100), curr.ClosePrice);
        }

        static string FormatDetails(ClusterStats curr, ClusterStats prev,
            bool bearishRun, int run, double bodyRatio, double shiftNorm)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "run={0} dir={1} prevCom={2} prevPosCom={3:F2} prevComShare={4:F2} body={5:F2} comShift={6:F2} close={7}",
                run, bearishRun ? "bearish->up" : "bullish->down",
                prev.ComPrice, prev.PosCom, prev.Top3Share,
                bodyRatio, shiftNorm, curr.ClosePrice);
        }
    }
}
