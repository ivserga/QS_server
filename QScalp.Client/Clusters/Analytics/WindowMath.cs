// ==========================================================================
//  WindowMath.cs — Общие формулы по окну ClusterStats (slope, доли, средние)
// ==========================================================================

using System;
using System.Collections.Generic;

namespace QScalp.Client.Clusters.Analytics
{
    internal static class WindowMath
    {
        public static double SlopePctClose(ClusterHistory history, int from, int count)
        {
            if (count < 2 || history == null) return 0;
            double[] ys = new double[count];
            for (int i = 0; i < count; i++)
            {
                var s = history.Last(from + count - 1 - i);
                if (s == null) return 0;
                ys[i] = s.ClosePrice;
            }
            return SlopePct(ys);
        }

        public static double SlopePctVolume(ClusterHistory history, int from, int count)
        {
            if (count < 2 || history == null) return 0;
            double[] ys = new double[count];
            for (int i = 0; i < count; i++)
            {
                var s = history.Last(from + count - 1 - i);
                if (s == null) return 0;
                ys[i] = s.Volume;
            }
            return SlopePct(ys);
        }

        public static int HighestHigh(ClusterHistory history, int from, int count)
        {
            int hi = int.MinValue;
            for (int i = 0; i < count; i++)
            {
                var s = history.Last(from + i);
                if (s == null) break;
                if (s.MaxPrice > hi) hi = s.MaxPrice;
            }
            return hi;
        }

        public static int LowestLow(ClusterHistory history, int from, int count)
        {
            int lo = int.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var s = history.Last(from + i);
                if (s == null) break;
                if (s.MinPrice < lo) lo = s.MinPrice;
            }
            return lo;
        }

        public static bool TwoHalvesHighs(ClusterHistory history, int count,
            out int firstHalfHigh, out int secondHalfHigh)
        {
            firstHalfHigh = secondHalfHigh = 0;
            if (count < 4 || history.Count < count) return false;

            int half = count / 2;
            int hi1 = int.MinValue, hi2 = int.MinValue;
            for (int i = 0; i < half; i++)
            {
                var s = history.Last(count - 1 - i);
                if (s != null && s.MaxPrice > hi1) hi1 = s.MaxPrice;
            }
            for (int i = 0; i < count - half; i++)
            {
                var s = history.Last(i);
                if (s != null && s.MaxPrice > hi2) hi2 = s.MaxPrice;
            }
            firstHalfHigh = hi1;
            secondHalfHigh = hi2;
            return true;
        }

        public static bool TwoHalvesLows(ClusterHistory history, int count,
            out int firstHalfLow, out int secondHalfLow)
        {
            firstHalfLow = secondHalfLow = 0;
            if (count < 4 || history.Count < count) return false;

            int half = count / 2;
            int lo1 = int.MaxValue, lo2 = int.MaxValue;
            for (int i = 0; i < half; i++)
            {
                var s = history.Last(count - 1 - i);
                if (s != null && s.MinPrice < lo1) lo1 = s.MinPrice;
            }
            for (int i = 0; i < count - half; i++)
            {
                var s = history.Last(i);
                if (s != null && s.MinPrice < lo2) lo2 = s.MinPrice;
            }
            firstHalfLow = lo1;
            secondHalfLow = lo2;
            return true;
        }

        public static double SharePosComBelow(ClusterHistory history, int count, double threshold)
        {
            int below = 0, total = 0;
            for (int i = 0; i < count; i++)
            {
                var s = history.Last(i);
                if (s == null) break;
                total++;
                if (s.PosCom < threshold) below++;
            }
            return total > 0 ? (double)below / total : 0;
        }

        public static double SharePosComAbove(ClusterHistory history, int count, double threshold)
        {
            int above = 0, total = 0;
            for (int i = 0; i < count; i++)
            {
                var s = history.Last(i);
                if (s == null) break;
                total++;
                if (s.PosCom > threshold) above++;
            }
            return total > 0 ? (double)above / total : 0;
        }

        public static double AverageSkewness(ClusterHistory history, int count)
        {
            double sum = 0; int n = 0;
            for (int i = 0; i < count; i++)
            {
                var s = history.Last(i);
                if (s == null) break;
                sum += s.Skewness;
                n++;
            }
            return n > 0 ? sum / n : 0;
        }

        public static int HighestHighExcludingLast(ClusterHistory history, int count, int skipFromEnd)
        {
            int hi = int.MinValue;
            for (int i = skipFromEnd; i < skipFromEnd + count; i++)
            {
                var s = history.Last(i);
                if (s == null) break;
                if (s.MaxPrice > hi) hi = s.MaxPrice;
            }
            return hi;
        }

        public static int LowestLowExcludingLast(ClusterHistory history, int count, int skipFromEnd)
        {
            int lo = int.MaxValue;
            for (int i = skipFromEnd; i < skipFromEnd + count; i++)
            {
                var s = history.Last(i);
                if (s == null) break;
                if (s.MinPrice < lo) lo = s.MinPrice;
            }
            return lo;
        }

        public static List<int> FindLocalPeakIndices(ClusterHistory history, int count, int leftRight)
        {
            var result = new List<int>();
            if (history == null || count <= 0 || leftRight <= 0) return result;

            int max = Math.Min(count, history.Count) - leftRight;
            for (int i = leftRight; i < max; i++)
            {
                var s = history.Last(i);
                if (s == null) continue;

                bool peak = true;
                for (int k = 1; k <= leftRight; k++)
                {
                    var l = history.Last(i - k);
                    var r = history.Last(i + k);
                    if (l == null || r == null) { peak = false; break; }
                    if (s.MaxPrice <= l.MaxPrice || s.MaxPrice <= r.MaxPrice) { peak = false; break; }
                }
                if (peak) result.Add(i);
            }

            result.Sort((a, b) => b.CompareTo(a));
            return result;
        }

        public static List<int> FindLocalTroughIndices(ClusterHistory history, int count, int leftRight)
        {
            var result = new List<int>();
            if (history == null || count <= 0 || leftRight <= 0) return result;

            int max = Math.Min(count, history.Count) - leftRight;
            for (int i = leftRight; i < max; i++)
            {
                var s = history.Last(i);
                if (s == null) continue;

                bool trough = true;
                for (int k = 1; k <= leftRight; k++)
                {
                    var l = history.Last(i - k);
                    var r = history.Last(i + k);
                    if (l == null || r == null) { trough = false; break; }
                    if (s.MinPrice >= l.MinPrice || s.MinPrice >= r.MinPrice) { trough = false; break; }
                }
                if (trough) result.Add(i);
            }

            result.Sort((a, b) => b.CompareTo(a));
            return result;
        }

        public static int LowestLowBetween(ClusterHistory history, int fromOlder, int toNewer)
        {
            int lo = int.MaxValue;
            for (int i = toNewer; i <= fromOlder; i++)
            {
                var s = history.Last(i);
                if (s == null) continue;
                if (s.MinPrice < lo) lo = s.MinPrice;
            }
            return lo;
        }

        public static int HighestHighBetween(ClusterHistory history, int fromOlder, int toNewer)
        {
            int hi = int.MinValue;
            for (int i = toNewer; i <= fromOlder; i++)
            {
                var s = history.Last(i);
                if (s == null) continue;
                if (s.MaxPrice > hi) hi = s.MaxPrice;
            }
            return hi;
        }

        public static double VolumeCenter(ClusterStats s)
        {
            if (s == null) return 0;
            double top3Mid = (s.Top3From + s.Top3To) * 0.5;
            return (s.CenterOfMass + top3Mid) * 0.5;
        }

        static double SlopePct(double[] ys)
        {
            int n = ys.Length;
            if (n < 2) return 0;

            double mean = 0;
            for (int i = 0; i < n; i++) mean += ys[i];
            mean /= n;
            if (mean <= 0) return 0;

            double xMean = (n - 1) / 2.0;
            double num = 0, den = 0;
            for (int i = 0; i < n; i++)
            {
                double dx = i - xMean;
                num += dx * (ys[i] - mean);
                den += dx * dx;
            }
            if (den == 0) return 0;
            double v = (num / den) / mean;
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
            return v;
        }
    }
}
