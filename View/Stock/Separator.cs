// ========================================================================
//    Separator.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// ========================================================================

using System;
using System.Windows;
using System.Windows.Media;

namespace QScalp.View.StockSpace
{
  class Separator : DrawingVisual
  {
    // **********************************************************************

    public void Redraw(double height)
    {
      double x = cfg.u.VQuoteVolumeWidth + cfg.s.VSplitter2Pen.Thickness / 2
        - Math.Floor(cfg.s.VSplitter2Pen.Thickness / 2);

      using(DrawingContext dc = RenderOpen())
        dc.DrawLine(cfg.s.VSplitter2Pen, new Point(x, 0), new Point(x, height));
    }

    // **********************************************************************
  }
}
