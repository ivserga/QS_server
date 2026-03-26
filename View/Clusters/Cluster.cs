// ======================================================================
//    Cluster.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ======================================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace QScalp.View.ClustersSpace
{
  class Cluster : ContainerVisual
  {
    // **********************************************************************

    public readonly DateTime DateTime;

    public int Volume { get; protected set; }
    public int Ticks { get; protected set; }
    public int Delta { get; protected set; }
    public int MinPrice { get; protected set; }
    public int MaxPrice { get; protected set; }

    public int OpenPrice { get { return firstPrice; } }
    public int ClosePrice { get { return lastPrice; } }

    // **********************************************************************

    Dictionary<int, CCell> cells;
    int firstPrice, lastPrice;

    ViewManager vmgr;

    // **********************************************************************

    public Cluster(ViewManager vmgr, DateTime dateTime)
    {
      this.vmgr = vmgr;
      this.DateTime = dateTime;

      MinPrice = int.MaxValue;

      cells = new Dictionary<int, CCell>();
    }

    // **********************************************************************

    public void Add(Trade trade)
    {
      CCell cell;

      // ------------------------------------------------------------

      if(!cells.TryGetValue(trade.IntPrice, out cell))
      {
        if(cells.Count == 0)
        {
          lastPrice = firstPrice = trade.IntPrice;
          cell = new CCell(cfg.s.ClusterOpenBrush);
        }
        else if(trade.IntPrice > firstPrice)
          cell = new CCell(cfg.s.ClusterUpBrush);
        else
          cell = new CCell(cfg.s.ClusterDownBrush);

        cell.Offset = new Vector(0, vmgr.PriceOffset(trade.IntPrice));

        cells.Add(trade.IntPrice, cell);
        Children.Add(cell);
      }

      // ------------------------------------------------------------

      if(trade.Op == TradeOp.Sell)
      {
        cell.AddSell(trade.Quantity);
        Delta -= trade.Quantity;
      }
      else
      {
        cell.AddBuy(trade.Quantity);
        Delta += trade.Quantity;
      }

      Volume += trade.Quantity;
      Ticks++;

      if(trade.IntPrice < MinPrice)
        MinPrice = trade.IntPrice;

      if(trade.IntPrice > MaxPrice)
        MaxPrice = trade.IntPrice;

      // ------------------------------------------------------------

      if(trade.IntPrice > lastPrice)
      {
        if(lastPrice < firstPrice)
        {
          if(trade.IntPrice < firstPrice)
            SetMarks(lastPrice, trade.IntPrice - cfg.u.PriceStep, false);
          else
          {
            SetMarks(lastPrice, firstPrice - cfg.u.PriceStep, false);
            SetMarks(firstPrice + cfg.u.PriceStep, trade.IntPrice, true);
          }
        }
        else
          SetMarks(lastPrice + cfg.u.PriceStep, trade.IntPrice, true);
      }
      else if(trade.IntPrice < lastPrice)
      {
        if(lastPrice > firstPrice)
        {
          if(trade.IntPrice > firstPrice)
            SetMarks(trade.IntPrice + cfg.u.PriceStep, lastPrice, false);
          else
          {
            SetMarks(firstPrice + cfg.u.PriceStep, lastPrice, false);
            SetMarks(trade.IntPrice, firstPrice - cfg.u.PriceStep, true);
          }
        }
        else
          SetMarks(trade.IntPrice, lastPrice - cfg.u.PriceStep, true);
      }

      lastPrice = trade.IntPrice;

      // ------------------------------------------------------------
    }

    // **********************************************************************

    void SetMarks(int p1, int p2, bool state)
    {
      CCell cell;

      for(int p = p1; p <= p2; p += cfg.u.PriceStep)
        if(cells.TryGetValue(p, out cell))
          cell.SetMark(state);
    }

    // **********************************************************************

    public void GetVolumeDistribution(out long volumeAbove, out long volumeBelow)
    {
      GetVolumeDistribution(lastPrice, out volumeAbove, out volumeBelow);
    }

    // **********************************************************************

    public void GetVolumeDistribution(int refPrice, out long volumeAbove, out long volumeBelow)
    {
      volumeAbove = 0;
      volumeBelow = 0;

      foreach(KeyValuePair<int, CCell> kvp in cells)
      {
        long cellVol = kvp.Value.BuyVolume + kvp.Value.SellVolume;

        if(kvp.Key > refPrice)
          volumeAbove += cellVol;
        else if(kvp.Key < refPrice)
          volumeBelow += cellVol;
      }
    }

    // **********************************************************************

    public long GetCellVolume(int price)
    {
      CCell cell;
      if(cells.TryGetValue(price, out cell))
        return cell.BuyVolume + cell.SellVolume;
      return 0;
    }

    // **********************************************************************

    public void Redraw()
    {
      foreach(CCell cell in cells.Values)
        if(cell.Updated)
          cell.Redraw();
    }

    // **********************************************************************

    public void Rebuild()
    {
      foreach(KeyValuePair<int, CCell> kvp in cells)
      {
        kvp.Value.Rebuild();
        kvp.Value.Offset = new Vector(0, vmgr.PriceOffset(kvp.Key));
      }
    }

    // **********************************************************************
  }
}
