// ==========================================================================
//    VGraphGuide.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace QScalp.View.GraphSpace
{
  class VGraphGuide : DrawingVisual, IVObsoletable
  {
    // **********************************************************************

    class SourceHandler : ITradesHandler
    {
      // --------------------------------------------------

      bool initialized;
      double value;

      readonly VGraphGuide owner;

      readonly double step;
      readonly double wsrc, k1, k2;

      // --------------------------------------------------

      public SourceHandler(double step, double wnew, double wsrc, VGraphGuide owner)
      {
        this.step = step;
        this.wsrc = wsrc;
        this.owner = owner;

        k1 = wnew * wsrc / step;
        k2 = 1 - wnew;
      }

      // --------------------------------------------------

      public void PutTrade(Trade trade, int count)
      {
        if(initialized)
          owner.Update(value - (value = k1 * trade.RawPrice + k2 * value));
        else
        {
          value = trade.RawPrice * wsrc / step;
          initialized = true;
        }
      }

      // --------------------------------------------------
    }

    // **********************************************************************

    LinkedList<double> guide;
    int guideCountLimit;

    double tickHeight;
    double halfThickness;

    TimeSpan interval;

    double value;
    DateTime lastTick;

    SourceHandler[] handlers;

    ViewManager vmgr;

    double width;

    double guideBaseY;
    double gcOffset;

    bool updated;

    // **********************************************************************

    public VGraphGuide(ViewManager vmgr)
    {
      this.vmgr = vmgr;

      guide = new LinkedList<double>();
      handlers = new SourceHandler[0];
    }

    // **********************************************************************

    void Update(double ndelta)
    {
      DateTime now = DateTime.UtcNow;

      if(now - lastTick >= interval)
      {
        while(guide.Count > guideCountLimit)
          guide.RemoveLast();

        guide.AddFirst(value);
        value -= ndelta;

        lastTick = now;
        updated = true;
      }
      else if(ndelta != 0)
      {
        value -= ndelta;
        updated = true;
      }
    }

    // **********************************************************************

    public void SetWidth(double width)
    {
      this.width = width;
      Rebuild();
    }

    // **********************************************************************

    public void Rebuild()
    {
      // --------------------------------------------------

      foreach(SourceHandler h in handlers)
        vmgr.TradesQueue.UnregisterHandler(h);

      vmgr.UnregisterObject(this);

      // --------------------------------------------------

      guideCountLimit = (int)Math.Ceiling(width / cfg.u.GuideTickWidth);

      tickHeight = -cfg.u.GuideTickHeight;
      halfThickness = cfg.s.GuideGraphPen.Thickness / 2;

      interval = new TimeSpan(0, 0, 0, 0, cfg.u.GuideTickInterval);

      handlers = new SourceHandler[cfg.u.GuideSources.Length];

      // --------------------------------------------------

      if(handlers.Length > 0)
      {
        vmgr.RegisterObject(this);

        for(int i = 0; i < handlers.Length; i++)
        {
          handlers[i] = new SourceHandler(
            cfg.u.GuideSources[i].PriceStep,
            cfg.u.GuideSources[i].Wnew,
            cfg.u.GuideSources[i].Wsrc,
            this);

          vmgr.TradesQueue.RegisterHandler(handlers[i],
            cfg.u.GuideSources[i].SecCode + cfg.u.GuideSources[i].ClassCode);
        }
      }
      else
        Clear();

      // --------------------------------------------------

      Redraw();
    }

    // **********************************************************************

    public void Clear()
    {
      guide.Clear();
      value = 0;
      lastTick = new DateTime();
      gcOffset = 0;

      updated = true;
    }

    // **********************************************************************

    void Redraw()
    {
      updated = false;

      using(DrawingContext dc = RenderOpen())
      {
        Point p1 = new Point(width, value * tickHeight);
        Point p2 = p1;

        for(LinkedListNode<double> gn = guide.First; gn != null; gn = gn.Next)
        {
          p2.X -= cfg.u.GuideTickWidth;
          p2.Y = gn.Value * tickHeight;

          dc.DrawEllipse(cfg.s.GuideGraphPen.Brush, null, p1, halfThickness, halfThickness);
          dc.DrawLine(cfg.s.GuideGraphPen, p1, p2);

          p1 = p2;
        }
      }

      //using(DrawingContext dc = RenderOpen())
      //{
      //  Point p = new Point(width, value * tickHeight);

      //  StreamGeometry sg = new StreamGeometry();
      //  using(StreamGeometryContext sgc = sg.Open())
      //  {
      //    sgc.BeginFigure(p, false, false);
      //    for(LinkedListNode<double> gn = guide.First; gn != null; gn = gn.Next)
      //    {
      //      p.X -= cfg.u.GuideTickWidth;
      //      p.Y = gn.Value * tickHeight;

      //      sgc.LineTo(p, true, true);
      //    }
      //  }
      //  sg.Freeze();

      //  dc.DrawGeometry(null, cfg.s.GuideGraphPen, sg);
      //}
    }

    // **********************************************************************

    public bool Obsolete { get { return true; } }

    // **********************************************************************

    public void Refresh()
    {
      // ------------------------------------------------------------

      double gY = guideBaseY + value * tickHeight;

      if((gY < vmgr.Height * cfg.s.GuideCenteringStart)
        ^ (gY > vmgr.Height * (1 - cfg.s.GuideCenteringStart)))
      {
        gcOffset = vmgr.Height / 2 - gY;
        gcOffset += Math.Sign(gcOffset) * vmgr.Height * cfg.s.GuideCenteringShift;

        if(Math.Abs(gcOffset) > vmgr.Height * 2)
        {
          guideBaseY += gcOffset;
          gcOffset = 0;

          Offset = new Vector(0, guideBaseY);
        }
      }

      // ------------------------------------------------------------

      if(gcOffset != 0)
      {
        double offset = gcOffset / cfg.s.GuideCenteringDiv;

        if(Math.Abs(offset) < cfg.s.GuideCenteringMin)
          if(Math.Abs(gcOffset) < cfg.s.GuideCenteringMin)
            offset = gcOffset;
          else
            offset = Math.Sign(gcOffset) * cfg.s.GuideCenteringMin;

        guideBaseY += offset;
        gcOffset -= offset;

        Offset = new Vector(0, guideBaseY);
      }

      // ------------------------------------------------------------

      if(updated)
        Redraw();

      // ------------------------------------------------------------
    }

    // **********************************************************************
  }
}
