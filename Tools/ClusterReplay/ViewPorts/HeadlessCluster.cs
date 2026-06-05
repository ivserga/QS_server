// Headless Cluster (View API) for ClusterAnalyzer replay.

using System;
using System.Collections.Generic;

namespace QScalp.View.ClustersSpace
{
  sealed class Cluster
  {
    struct Cell
    {
      public long BuyVolume;
      public long SellVolume;
      public long NeutralVolume;
      public long Total => BuyVolume + SellVolume + NeutralVolume;
    }

    readonly Dictionary<int, Cell> _cells = new Dictionary<int, Cell>();
    int _firstPrice;
    int _lastPrice;

    public DateTime DateTime { get; }
    public int Volume { get; private set; }
    public int Ticks { get; private set; }
    public int Delta { get; private set; }
    public int MinPrice { get; private set; } = int.MaxValue;
    public int MaxPrice { get; private set; }

    public int OpenPrice => _firstPrice;
    public int ClosePrice => _lastPrice;

    public Cluster(DateTime dateTime) => DateTime = dateTime;

    public void AddLevel(int price, int bid, int ask, int inside)
    {
      if (!_cells.TryGetValue(price, out var cell))
      {
        if (_cells.Count == 0)
          _lastPrice = _firstPrice = price;
        _cells[price] = cell;
      }

      if (bid > 0)
      {
        cell.SellVolume += bid;
        Delta -= bid;
      }

      if (ask > 0)
      {
        cell.BuyVolume += ask;
        Delta += ask;
      }

      if (inside > 0)
        cell.NeutralVolume += inside;

      _cells[price] = cell;

      int qty = bid + ask + inside;
      Volume += qty;
      if (qty > 0)
        Ticks++;

      if (price < MinPrice) MinPrice = price;
      if (price > MaxPrice) MaxPrice = price;
      _lastPrice = price;
    }

    public void GetVolumeDistribution(out long volumeAbove, out long volumeBelow)
      => GetVolumeDistribution(_lastPrice, out volumeAbove, out volumeBelow);

    public void GetVolumeDistribution(int refPrice, out long volumeAbove, out long volumeBelow)
    {
      volumeAbove = 0;
      volumeBelow = 0;

      foreach (var kv in _cells)
      {
        long v = kv.Value.Total;
        if (kv.Key > refPrice) volumeAbove += v;
        else if (kv.Key < refPrice) volumeBelow += v;
      }
    }

    public long GetCellVolume(int price)
      => _cells.TryGetValue(price, out var cell) ? cell.Total : 0;

    public IList<ClusterPriceLevel> GetPriceLevels()
    {
      var prices = new List<int>(_cells.Keys);
      prices.Sort();
      prices.Reverse();

      var levels = new List<ClusterPriceLevel>(prices.Count);
      foreach (int price in prices)
      {
        var cell = _cells[price];
        levels.Add(new ClusterPriceLevel(price, (int)cell.SellVolume, (int)cell.BuyVolume, (int)cell.NeutralVolume));
      }

      return levels;
    }
  }
}
