// =======================================================================
//    InfoMark.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =======================================================================

using System;
using System.Windows;
using System.Windows.Media;

namespace QScalp.View.InfoSpace
{
  class InfoMark : DrawingVisual
  {
    // **********************************************************************

    FrameworkElement infoElement;

    // **********************************************************************

    public object Tag { get; set; }

    // **********************************************************************

    public InfoMark(FrameworkElement infoElement) { this.infoElement = infoElement; }

    // **********************************************************************

    public void Draw(Brush brush, Pen pen, string text)
    {
      FormattedText ft = new FormattedText(
        text,
        cfg.BaseCulture,
        FlowDirection.LeftToRight,
        cfg.BaseFont,
        cfg.u.FontSize,
        cfg.s.InfoTextBrush);

      ft.TextAlignment = TextAlignment.Right;

      using(DrawingContext dc = RenderOpen())
      {
        // ----------------------------------------------------------

        double w = ft.Width + cfg.s.TextHMargin * 2 + cfg.QuoteHeight / 2;

        if(w > infoElement.ActualWidth)
        {
          infoElement.MinWidth = Math.Ceiling(w);
          return;
        }

        w = infoElement.ActualWidth;

        // ----------------------------------------------------------

        StreamGeometry sg = new StreamGeometry();
        double adj = pen.Thickness / 2;

        using(StreamGeometryContext sgc = sg.Open())
        {
          sgc.BeginFigure(new Point(adj, cfg.QuoteHeight / 2), true, true);
          sgc.PolyLineTo(new Point[] {
            new Point(cfg.QuoteHeight / 2, adj),
            new Point(w - adj, adj),
            new Point(w - adj, cfg.QuoteHeight - adj),
            new Point(cfg.QuoteHeight / 2, cfg.QuoteHeight - adj) },
            true, true);
        }

        sg.Freeze();

        // ----------------------------------------------------------

        dc.DrawGeometry(brush, pen, sg);

        dc.DrawText(ft, new Point(
          w - cfg.s.TextHMargin - pen.Thickness,
          cfg.QuoteHeight / 2 - cfg.TextTopOffset));

        // ----------------------------------------------------------
      }
    }

    // **********************************************************************
  }
}
