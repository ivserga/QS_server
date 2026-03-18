// =========================================================================
//   VGraphTrades.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =========================================================================

using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace QScalp.View.GraphSpace
{
  class VGraphTrades : ContainerVisual, IVObsoletable, IVScrollable, ITradesHandler
  {
    // **********************************************************************

    DispatcherTimer ticking;

    ViewManager vmgr;

    double width;
    int maxTrades;

    // **********************************************************************

    public bool Obsolete { get; protected set; }

    // **********************************************************************

    public VGraphTrades(ViewManager vmgr)
    {
      this.vmgr = vmgr;

      ticking = new DispatcherTimer();
      ticking.Tick += new EventHandler(TickingTick);

      vmgr.RegisterObject(this);
    }

    // **********************************************************************

    public void SetWidth(double width)
    {
      LeftShift(this.width - width);

      this.width = width;
      Rebuild();
    }

    // **********************************************************************

    public void PutTrade(Trade trade, int count)
    {
      if(trade.Quantity >= cfg.u.TradeVolume1 && count <= maxTrades)
      {
        ticking.Stop();

        TradeBall tb = new TradeBall(trade);

        tb.Offset = new Vector(
          width - tb.Radius + cfg.s.TradeBallRadius,
          vmgr.PriceOffset(tb.Price) + cfg.QuoteHeight / 2);

        LeftShift(tb.Radius * 2 - cfg.s.TradeBallRadius);
        Children.Add(tb);

        Obsolete = true;
      }
    }

    // **********************************************************************

    void LeftShift(double offset)
    {
      int removeCount = 0;

      foreach(TradeBall tb in Children)
        if(tb.Offset.X + tb.Radius > 0)
          tb.Offset -= new Vector(offset, 0);
        else
          removeCount++;

      Children.RemoveRange(0, removeCount);
    }

    // **********************************************************************

    void TickingTick(object sender, EventArgs e)
    {
      LeftShift(cfg.s.TradeBallRadius);
    }

    // **********************************************************************

    public void Refresh()
    {
      Obsolete = false;
      ticking.Start();
    }

    // **********************************************************************

    public void Rebuild()
    {
      vmgr.TradesQueue.UnregisterHandler(this);
      vmgr.TradesQueue.RegisterHandler(this, cfg.u.SecCode + cfg.u.ClassCode);

      maxTrades = (int)Math.Ceiling(width / cfg.s.TradeBallRadius);
      ticking.Interval = new TimeSpan(0, 0, 0, 0, cfg.u.TradesTickInterval);

      foreach(TradeBall tb in Children)
        tb.Offset = new Vector(
          tb.Offset.X,
          vmgr.PriceOffset(tb.Price) + cfg.QuoteHeight / 2);
    }

    // **********************************************************************

    public void UpdateOffset() { Offset = new Vector(0, vmgr.BaseY); }

    // **********************************************************************
  }
}
