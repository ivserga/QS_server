// ==========================================================================
//  Cluster.cs — Headless-кластер для анализа без отрисовки.
// ==========================================================================
//  Повторяет публичный API оригинального QScalp.View.ClustersSpace.Cluster
//  (поля Volume/Ticks/Delta/OpenPrice/ClosePrice/MinPrice/MaxPrice, методы
//  Add(Trade), GetCellVolume(price), GetVolumeDistribution(...)) — этого
//  достаточно, чтобы код в Analytics/* и ClusterAnalyzer.cs работал поверх
//  него без изменений.
//
//  Отличия от WPF-версии:
//    • не наследуется от ContainerVisual;
//    • не вызывает SetMarks (это рисование «связок» между ячейками);
//    • cells — Dictionary<int, CCell>, где CCell — простой data-class.
// ==========================================================================

using System;
using System.Collections.Generic;

using QScalp.Shared.Models;

namespace QScalp.Client.Clusters
{
    internal sealed class Cluster
    {
        public DateTime DateTime { get; }

        public int Volume { get; private set; }
        public int Ticks { get; private set; }
        public int Delta { get; private set; }
        public int MinPrice { get; private set; }
        public int MaxPrice { get; private set; }

        public int OpenPrice => _firstPrice;
        public int ClosePrice => _lastPrice;

        // ********************************************************************

        private readonly Dictionary<int, CCell> _cells = new Dictionary<int, CCell>();
        private int _firstPrice;
        private int _lastPrice;

        // ********************************************************************

        public Cluster(DateTime dateTime)
        {
            DateTime = dateTime;
            MinPrice = int.MaxValue;
        }

        // ********************************************************************

        public void Add(Trade trade)
        {
            if (!_cells.TryGetValue(trade.IntPrice, out var cell))
            {
                if (_cells.Count == 0)
                    _lastPrice = _firstPrice = trade.IntPrice;

                cell = new CCell();
                _cells.Add(trade.IntPrice, cell);
            }

            if (trade.Op == TradeOp.Buy)
            {
                cell.AddBuy(trade.Quantity);
                Delta += trade.Quantity;
            }
            else if (trade.Op == TradeOp.Sell)
            {
                cell.AddSell(trade.Quantity);
                Delta -= trade.Quantity;
            }
            else
            {
                cell.AddNeutral(trade.Quantity);
            }

            Volume += trade.Quantity;
            Ticks++;

            if (trade.IntPrice < MinPrice) MinPrice = trade.IntPrice;
            if (trade.IntPrice > MaxPrice) MaxPrice = trade.IntPrice;

            _lastPrice = trade.IntPrice;
        }

        // ********************************************************************

        public void GetVolumeDistribution(out long volumeAbove, out long volumeBelow)
        {
            GetVolumeDistribution(_lastPrice, out volumeAbove, out volumeBelow);
        }

        public void GetVolumeDistribution(int refPrice, out long volumeAbove, out long volumeBelow)
        {
            volumeAbove = 0;
            volumeBelow = 0;

            foreach (var kvp in _cells)
            {
                long cellVol = kvp.Value.BuyVolume + kvp.Value.SellVolume + kvp.Value.NeutralVolume;

                if (kvp.Key > refPrice) volumeAbove += cellVol;
                else if (kvp.Key < refPrice) volumeBelow += cellVol;
            }
        }

        // ********************************************************************

        public long GetCellVolume(int price)
        {
            return _cells.TryGetValue(price, out var cell)
                ? cell.BuyVolume + cell.SellVolume + cell.NeutralVolume
                : 0;
        }

        // ********************************************************************

        public IList<ClusterPriceLevel> GetPriceLevels(int priceStep)
        {
            var prices = new List<int>(_cells.Keys);
            prices.Sort();
            prices.Reverse();

            var levels = new List<ClusterPriceLevel>(prices.Count);
            foreach (int price in prices)
            {
                var cell = _cells[price];
                levels.Add(new ClusterPriceLevel(
                    price,
                    cell.SellVolume,
                    cell.BuyVolume,
                    cell.NeutralVolume,
                    priceStep));
            }

            return levels;
        }
    }
}
