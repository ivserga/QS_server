// ==========================================================================
//  ClusterHistory.cs — Кэш последних закрытых кластеров и их метрик
// ==========================================================================

using System;
using System.Collections.Generic;

namespace QScalp.Client.Clusters.Analytics
{
    internal sealed class ClusterHistory
    {
        readonly int _capacity;
        readonly LinkedList<ClusterStats> _items;

        public int Count => _items.Count;
        public int Capacity => _capacity;

        public ClusterHistory(int capacity)
        {
            _capacity = capacity > 0 ? capacity : 64;
            _items = new LinkedList<ClusterStats>();
        }

        public void Add(ClusterStats stats)
        {
            if (stats == null) return;
            _items.AddLast(stats);
            while (_items.Count > _capacity) _items.RemoveFirst();
        }

        public ClusterStats Last(int indexFromEnd)
        {
            if (indexFromEnd < 0 || indexFromEnd >= _items.Count) return null;
            var node = _items.Last;
            for (int i = 0; i < indexFromEnd; i++) node = node.Previous;
            return node.Value;
        }

        public double AverageVolumeBefore(int n)
        {
            if (_items.Count < n + 1) return 0;
            double sum = 0; int cnt = 0;
            var node = _items.Last.Previous;
            while (node != null && cnt < n)
            {
                sum += node.Value.Volume;
                node = node.Previous;
                cnt++;
            }
            return cnt > 0 ? sum / cnt : 0;
        }

        public double AverageRangeBefore(int n)
        {
            if (_items.Count < n + 1) return 0;
            double sum = 0; int cnt = 0;
            var node = _items.Last.Previous;
            while (node != null && cnt < n)
            {
                sum += node.Value.Range;
                node = node.Previous;
                cnt++;
            }
            return cnt > 0 ? sum / cnt : 0;
        }

        public int CountBearishRunFrom(int indexFromEnd)
        {
            int n = 0;
            for (int i = indexFromEnd; i < _items.Count; i++)
            {
                var s = Last(i);
                if (s == null) break;
                if (s.ClosePrice < s.OpenPrice) n++;
                else break;
            }
            return n;
        }

        public int CountBullishRunFrom(int indexFromEnd)
        {
            int n = 0;
            for (int i = indexFromEnd; i < _items.Count; i++)
            {
                var s = Last(i);
                if (s == null) break;
                if (s.ClosePrice > s.OpenPrice) n++;
                else break;
            }
            return n;
        }

        public DateTime LastTime
        {
            get
            {
                var s = Last(0);
                return s != null ? s.Source.DateTime : DateTime.MinValue;
            }
        }
    }
}
