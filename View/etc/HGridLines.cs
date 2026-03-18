// =========================================================================
//    HGridLines.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// =========================================================================

using System;
using System.Windows;
using System.Windows.Media;

namespace QScalp.View
{
  class HGridLines : DrawingVisual, IVScrollable
  {
    // **********************************************************************

    ViewManager vmgr;

    double max1, inc1, adj1;
    double max2, inc2, adj2;

    bool drawGrid1;

    double width;

    // **********************************************************************

    public HGridLines(ViewManager vmgr, bool drawGrid1)
    {
      this.vmgr = vmgr;
      this.drawGrid1 = drawGrid1;

      vmgr.RegisterObject(this);
    }

    // **********************************************************************

    public void SetWidth(double width) { this.width = width; Rebuild(); }
    public void UpdateOffset() { DrawLines(); }

    // **********************************************************************

    public void Rebuild()
    {
      max1 = vmgr.Height + cfg.s.HGridLine1Pen.Thickness;
      inc1 = cfg.u.Grid1Step * cfg.QuoteHeight / cfg.u.PriceStep;
      adj1 = cfg.s.HGridLine1Pen.Thickness / 2;
      adj1 -= Math.Floor(adj1);

      max2 = vmgr.Height + cfg.s.HGridLine2Pen.Thickness;
      inc2 = cfg.u.Grid2Step * cfg.QuoteHeight / cfg.u.PriceStep;
      adj2 = cfg.s.HGridLine2Pen.Thickness / 2;
      adj2 -= Math.Floor(adj2);

      DrawLines();
    }

    // **********************************************************************

    void DrawLines()
    {
      double y;

      using(DrawingContext dc = RenderOpen())
      {
        if(drawGrid1)
        {
          y = Math.Floor((vmgr.BaseY + cfg.QuoteHeight) % inc1 - cfg.QuoteHeight / 2) + adj1;
          while(y < max1)
          {
            dc.DrawLine(cfg.s.HGridLine1Pen, new Point(0, y), new Point(width, y));
            y += inc1;
          }
        }

        y = Math.Floor((vmgr.BaseY + cfg.QuoteHeight) % inc2 - cfg.QuoteHeight / 2) + adj2;
        while(y < max2)
        {
          dc.DrawLine(cfg.s.HGridLine2Pen, new Point(0, y), new Point(width, y));
          y += inc2;
        }
      }
    }

    // **********************************************************************

    static DrawingVisual CreateDV(Pen pen, double w)
    {
      double y = pen.Thickness / 2;
      y += Math.Floor(cfg.QuoteHeight / 2) - Math.Floor(y);

      DrawingVisual dv = new DrawingVisual();

      using(DrawingContext dc = dv.RenderOpen())
        dc.DrawLine(pen, new Point(0, y), new Point(w, y));

      return dv;
    }

    // **********************************************************************

    public static void AddChildTo(VisualCollection target, int price, double w)
    {
      if(price % cfg.u.Grid2Step == 0)
        target.Add(CreateDV(cfg.s.HGridLine2Pen, w));
      else if(price % cfg.u.Grid1Step == 0)
        target.Add(CreateDV(cfg.s.HGridLine1Pen, w));
    }

    // **********************************************************************
  }
}
