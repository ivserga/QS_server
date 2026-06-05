// ==========================================================================
//  ClusterStats.cs — Метрики распределения объёма по ценам внутри кластера
// ==========================================================================
//  Headless-порт оригинала из QScalp.View.ClustersSpace.Analytics.ClusterStats:
//  логика 1-в-1, источник объёмов — Cluster.GetCellVolume(price), без WPF.
// ==========================================================================

using System;
using System.Collections.Generic;

namespace QScalp.Client.Clusters.Analytics
{
    internal enum ProfileShape
    {
        Unknown, Balanced, TopHeavy, BottomHeavy, Thin, Trending
    }

    internal struct LvnRange
    {
        public int From;
        public int To;
        public long Volume;

        public LvnRange(int from, int to, long volume)
        {
            From = from; To = to; Volume = volume;
        }
    }

    internal sealed class ClusterStats
    {
        public Cluster Source { get; private set; }
        public int PriceStep { get; private set; }

        public int Volume { get; private set; }
        public int Ticks { get; private set; }
        public int Delta { get; private set; }

        public int OpenPrice { get; private set; }
        public int ClosePrice { get; private set; }
        public int MinPrice { get; private set; }
        public int MaxPrice { get; private set; }

        public int Range => MaxPrice - MinPrice;

        public int VAH { get; private set; }
        public int VAL { get; private set; }
        public double VaActualShare { get; private set; }

        public double CenterOfMass { get; private set; }
        public double PosCom { get; private set; }
        public int ComPrice { get; private set; }
        public double StdDev { get; private set; }
        public double Skewness { get; private set; }
        public double Kurtosis { get; private set; }

        public double Top3Share { get; private set; }
        public int Top3From { get; private set; }
        public int Top3To { get; private set; }

        public ProfileShape Shape { get; private set; }
        public IList<LvnRange> Lvn { get; private set; }

        const double VaShareTarget = 0.70;

        ClusterStats() { }

        // ********************************************************************

        public static ClusterStats Compute(Cluster c, int priceStep)
        {
            if (c == null || c.Volume == 0 || priceStep <= 0)
                return null;
            if (c.MinPrice == int.MaxValue || c.MaxPrice < c.MinPrice)
                return null;

            int min = c.MinPrice;
            int max = c.MaxPrice;
            int range = max - min;

            int nLevels = range / priceStep + 1;
            if (nLevels <= 0) return null;

            long[] vols = new long[nLevels];
            long total = 0;
            int vaSeedIdx = 0;
            long vaSeedVol = 0;

            for (int i = 0; i < nLevels; i++)
            {
                int price = min + i * priceStep;
                long v = c.GetCellVolume(price);
                vols[i] = v;
                total += v;
                if (v > vaSeedVol) { vaSeedVol = v; vaSeedIdx = i; }
            }

            if (total <= 0) return null;

            var s = new ClusterStats
            {
                Source = c,
                PriceStep = priceStep,
                Volume = c.Volume,
                Ticks = c.Ticks,
                Delta = c.Delta,
                OpenPrice = c.OpenPrice,
                ClosePrice = c.ClosePrice,
                MinPrice = min,
                MaxPrice = max
            };

            // Value Area
            long accum = vaSeedVol;
            int lo = vaSeedIdx;
            int hi = vaSeedIdx;

            while (accum < total * VaShareTarget && (lo > 0 || hi < nLevels - 1))
            {
                long leftPair = 0;
                long rightPair = 0;

                if (lo > 0) leftPair = vols[lo - 1] + (lo > 1 ? vols[lo - 2] : 0);
                if (hi < nLevels - 1) rightPair = vols[hi + 1] + (hi < nLevels - 2 ? vols[hi + 2] : 0);

                if (lo == 0) hi = Math.Min(hi + 2, nLevels - 1);
                else if (hi == nLevels - 1) lo = Math.Max(lo - 2, 0);
                else if (rightPair >= leftPair) hi = Math.Min(hi + 2, nLevels - 1);
                else lo = Math.Max(lo - 2, 0);

                accum = 0;
                for (int k = lo; k <= hi; k++) accum += vols[k];
            }

            s.VAL = min + lo * priceStep;
            s.VAH = min + hi * priceStep;
            s.VaActualShare = (double)accum / total;

            // Моменты распределения
            double sumPv = 0;
            for (int i = 0; i < nLevels; i++)
                sumPv += (double)vols[i] * (min + i * priceStep);
            double mean = sumPv / total;
            s.CenterOfMass = mean;
            s.PosCom = range > 0 ? (mean - min) / range : 0.5;
            s.ComPrice = NearestTickPrice(mean, priceStep, min, max);

            double m2 = 0, m3 = 0, m4 = 0;
            for (int i = 0; i < nLevels; i++)
            {
                if (vols[i] == 0) continue;
                double d = (min + i * priceStep) - mean;
                double w = (double)vols[i] / total;
                double d2 = d * d;
                m2 += w * d2;
                m3 += w * d2 * d;
                m4 += w * d2 * d2;
            }

            double std = m2 > 0 ? Math.Sqrt(m2) : 0;
            s.StdDev = std;
            s.Skewness = (std > 0) ? m3 / (std * std * std) : 0;
            s.Kurtosis = (m2 > 0) ? m4 / (m2 * m2) : 0;

            // Top-3
            long top3Vol = 0;
            int top3From = min;
            int top3To = min;

            if (nLevels >= 3)
            {
                long win = vols[0] + vols[1] + vols[2];
                top3Vol = win;
                top3From = min;
                top3To = min + 2 * priceStep;

                for (int i = 3; i < nLevels; i++)
                {
                    win += vols[i] - vols[i - 3];
                    if (win > top3Vol)
                    {
                        top3Vol = win;
                        top3From = min + (i - 2) * priceStep;
                        top3To = min + i * priceStep;
                    }
                }
            }
            else
            {
                for (int i = 0; i < nLevels; i++) top3Vol += vols[i];
                top3From = min;
                top3To = max;
            }

            s.Top3From = top3From;
            s.Top3To = top3To;
            s.Top3Share = (double)top3Vol / total;

            // LVN
            double avgPerLevel = (double)total / nLevels;
            double lvnThreshold = avgPerLevel * 0.25;

            var lvn = new List<LvnRange>();
            int runStart = -1;
            long runVol = 0;

            for (int i = 0; i < nLevels; i++)
            {
                if (vols[i] < lvnThreshold)
                {
                    if (runStart < 0) { runStart = i; runVol = 0; }
                    runVol += vols[i];
                }
                else
                {
                    if (runStart >= 0 && i - runStart >= 2)
                        lvn.Add(new LvnRange(min + runStart * priceStep, min + (i - 1) * priceStep, runVol));
                    runStart = -1;
                }
            }

            if (runStart >= 0 && nLevels - runStart >= 2)
                lvn.Add(new LvnRange(min + runStart * priceStep, min + (nLevels - 1) * priceStep, runVol));

            s.Lvn = lvn;
            s.Shape = ClassifyShape(s, range);

            return s;
        }

        // ********************************************************************

        public long VolumeBeyondCom(bool above)
        {
            long v = 0;
            if (above)
            {
                for (int p = ComPrice + PriceStep; p <= MaxPrice; p += PriceStep)
                    v += Source.GetCellVolume(p);
            }
            else
            {
                for (int p = MinPrice; p <= ComPrice - PriceStep; p += PriceStep)
                    v += Source.GetCellVolume(p);
            }
            return v;
        }

        public double ShareBeyondCom(bool above)
        {
            return Volume > 0 ? (double)VolumeBeyondCom(above) / Volume : 0.0;
        }

        static int NearestTickPrice(double price, int step, int min, int max)
        {
            if (step <= 0) return min;
            int idx = (int)Math.Round((price - min) / (double)step);
            int nLevels = (max - min) / step + 1;
            if (idx < 0) idx = 0;
            if (idx >= nLevels) idx = nLevels - 1;
            return min + idx * step;
        }

        // ********************************************************************

        public long VolumeInQuarter(bool bottom)
        {
            if (Range <= 0) return 0;
            int quarter = Math.Max(PriceStep, Range / 4);
            int lo, hi;
            if (bottom) { lo = MinPrice; hi = MinPrice + quarter; }
            else { lo = MaxPrice - quarter; hi = MaxPrice; }

            long v = 0;
            for (int p = lo; p <= hi; p += PriceStep)
                v += Source.GetCellVolume(p);
            return v;
        }

        public double ShareInQuarter(bool bottom)
        {
            return Volume > 0 ? (double)VolumeInQuarter(bottom) / Volume : 0.0;
        }

        // ********************************************************************

        static ProfileShape ClassifyShape(ClusterStats s, int range)
        {
            if (range <= 0 || s.Volume == 0) return ProfileShape.Unknown;
            if (s.Top3Share >= 0.55) return ProfileShape.Thin;

            double posMass = (s.CenterOfMass - s.MinPrice) / range;
            if (posMass <= 0.20 || posMass >= 0.80) return ProfileShape.Trending;

            if (s.Skewness <= -0.4) return ProfileShape.TopHeavy;
            if (s.Skewness >= 0.4) return ProfileShape.BottomHeavy;
            return ProfileShape.Balanced;
        }
    }
}
