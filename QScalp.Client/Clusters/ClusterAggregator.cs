// ==========================================================================
//  ClusterAggregator.cs — Сборка кластеров из потока трейдов
// ==========================================================================
//  Headless-аналог QScalp.View.ClustersElement.PutTrade: принимает Trade,
//  решает по выбранной базе кластеризации (Time / Volume / Range / Ticks /
//  Delta), когда закрыть текущий кластер и открыть новый, и поднимает
//  событие OnClusterClosed для подписчиков (SignalBus и т. п.).
//
//  Не отображает ничего; не хранит историю — это задача SignalBus.History.
// ==========================================================================

using System;

using QScalp.Shared.Models;

namespace QScalp.Client.Clusters
{
    internal sealed class ClusterAggregator
    {
        private readonly ClusterBase _base;
        private readonly int _size;

        private Cluster _current;

        public event Action<Cluster> ClusterClosed;

        public Cluster Current => _current;

        // ********************************************************************

        public ClusterAggregator(ClusterBase clusterBase, int clusterSize)
        {
            _base = clusterBase;
            _size = Math.Max(1, clusterSize);

            _current = new Cluster(DateTime.MaxValue);
        }

        // ********************************************************************

        public void PutTrade(Trade trade)
        {
            if (trade.DateTime < _current.DateTime)
            {
                StartNewCluster(_base == ClusterBase.Time
                    ? AlignToTime(trade.DateTime)
                    : trade.DateTime);
            }

            switch (_base)
            {
                case ClusterBase.Time:
                {
                    long csTicks = (long)_size * TimeSpan.TicksPerSecond;
                    if (trade.DateTime.Ticks - _current.DateTime.Ticks >= csTicks)
                        StartNewCluster(new DateTime(trade.DateTime.Ticks / csTicks * csTicks));
                    break;
                }

                case ClusterBase.Volume:
                    while (_current.Volume + trade.Quantity > _size)
                        SplitByVolume(_current.Volume, ref trade);
                    break;

                case ClusterBase.Range:
                    if (_current.MaxPrice - trade.IntPrice > _size
                        || trade.IntPrice - _current.MinPrice > _size)
                        StartNewCluster(trade.DateTime);
                    break;

                case ClusterBase.Ticks:
                    if (_current.Ticks >= _size)
                        StartNewCluster(trade.DateTime);
                    break;

                case ClusterBase.Delta:
                    if (trade.Op == TradeOp.Buy)
                        while (_current.Delta + trade.Quantity > _size)
                            SplitByDelta(_current.Delta, ref trade);
                    else if (trade.Op == TradeOp.Sell)
                        while (trade.Quantity - _current.Delta > _size)
                            SplitByDelta(-_current.Delta, ref trade);
                    break;
            }

            _current.Add(trade);
        }

        // ********************************************************************

        private DateTime AlignToTime(DateTime dt)
        {
            long csTicks = (long)_size * TimeSpan.TicksPerSecond;
            return new DateTime(dt.Ticks / csTicks * csTicks);
        }

        // ********************************************************************

        private void SplitByVolume(int csize, ref Trade trade)
        {
            int space = _size - csize;
            if (space > 0)
            {
                int q = trade.Quantity;
                trade.Quantity = space;
                _current.Add(trade);
                trade.Quantity = q - space;
            }
            StartNewCluster(trade.DateTime);
        }

        private void SplitByDelta(int cdelta, ref Trade trade)
        {
            int space = _size - cdelta;
            if (space > 0)
            {
                int q = trade.Quantity;
                trade.Quantity = space;
                _current.Add(trade);
                trade.Quantity = q - space;
            }
            StartNewCluster(trade.DateTime);
        }

        // ********************************************************************

        private void StartNewCluster(DateTime dateTime)
        {
            // Поднимаем событие закрытия только если в предыдущем кластере были трейды
            // (на самом старте _current создан с DateTime.MaxValue и пуст).
            if (_current.DateTime != DateTime.MaxValue && _current.Volume > 0)
                ClusterClosed?.Invoke(_current);

            _current = new Cluster(dateTime);
        }
    }
}
