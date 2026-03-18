// ==========================================================================
//   VGraphSpreads.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace QScalp.View.GraphSpace
{
  class VGraphSpreads : DrawingVisual, IVObsoletable, IVScrollable, IDataHandler<Spread>
  {
    // **********************************************************************

    LinkedList<Spread> data;
    int dataCountLimit;

    DispatcherTimer ticking;

    ViewManager vmgr;

    double halfQuoteHeight;
    double width;

    double halfAskThickness;
    double halfBidThickness;

    // **********************************************************************

    public bool Obsolete { get; protected set; }

    // **********************************************************************

    public VGraphSpreads(ViewManager vmgr)
    {
      this.vmgr = vmgr;

      data = new LinkedList<Spread>();

      ticking = new DispatcherTimer();
      ticking.Tick += new EventHandler(TickingTick);
      ticking.Interval = new TimeSpan(0, 0, 0, 0, int.MaxValue);
    }

    // **********************************************************************

    public void SetWidth(double width)
    {
      this.width = width;
      Rebuild();
    }

    // **********************************************************************

    void TickingTick(object sender, EventArgs e)
    {
      while(data.Count > dataCountLimit)
        data.RemoveLast();

      data.AddFirst(new Spread(vmgr.Ask, vmgr.Bid));

      Redraw();
    }

    // **********************************************************************

    public void PutData(Spread spread)
    {
      ticking.Stop();

      while(data.Count > dataCountLimit)
        data.RemoveLast();

      data.AddFirst(spread);

      Obsolete = true;
    }

    // **********************************************************************

    public void Redraw()
    {
      Obsolete = false;

      using(DrawingContext dc = RenderOpen())
        if(data.Count > 1)
        {
          Spread s = data.First.Value;

          Point ap1 = new Point(width, vmgr.PriceOffset(s.Ask) + halfQuoteHeight);
          Point ap2 = ap1;

          Point bp1 = new Point(width, vmgr.PriceOffset(s.Bid) + halfQuoteHeight);
          Point bp2 = bp1;

          for(LinkedListNode<Spread> sn = data.First.Next; sn != null; sn = sn.Next)
          {
            bp2.X = ap2.X -= cfg.u.SpreadTickWidth;

            s = sn.Value;

            ap2.Y = vmgr.PriceOffset(s.Ask) + halfQuoteHeight;
            bp2.Y = vmgr.PriceOffset(s.Bid) + halfQuoteHeight;

            dc.DrawEllipse(cfg.s.AskGraphPen.Brush, null, ap1, halfAskThickness, halfAskThickness);
            dc.DrawEllipse(cfg.s.BidGraphPen.Brush, null, bp1, halfBidThickness, halfBidThickness);

            dc.DrawLine(cfg.s.AskGraphPen, ap1, ap2);
            dc.DrawLine(cfg.s.BidGraphPen, bp1, bp2);

            ap1 = ap2;
            bp1 = bp2;
          }
        }
    }

    // **********************************************************************

    public void Refresh()
    {
      Redraw();
      ticking.Start();
    }

    // **********************************************************************

    public void Rebuild()
    {
      vmgr.SpreadsQueue.UnregisterHandler(this);
      vmgr.UnregisterObject(this);

      if(cfg.u.SpreadTickWidth > 0)
        dataCountLimit = (int)Math.Ceiling(width / cfg.u.SpreadTickWidth) + 1;
      else
        dataCountLimit = 0;

      if(dataCountLimit > 0)
      {
        vmgr.RegisterObject(this);
        vmgr.SpreadsQueue.RegisterHandler(this);

        halfQuoteHeight = Math.Floor(cfg.QuoteHeight / 2);
        ticking.Interval = new TimeSpan(0, 0, 0, 0, cfg.u.SpreadsTickInterval);

        halfAskThickness = cfg.s.AskGraphPen.Thickness / 2;
        halfBidThickness = cfg.s.BidGraphPen.Thickness / 2;

        UpdateOffset();
      }
      else
      {
        ticking.Stop();
        ticking.Interval = new TimeSpan(0, 0, 0, 0, int.MaxValue);

        data.Clear();
      }

      Redraw();
    }

    // **********************************************************************

    public void UpdateOffset() { Offset = new Vector(0, vmgr.BaseY); }

    // **********************************************************************
  }
}
