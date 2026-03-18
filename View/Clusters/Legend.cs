// ======================================================================
//    Legend.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ======================================================================

using System;
using System.Windows;
using System.Windows.Media;
using System.Text;

namespace QScalp.View.ClustersSpace
{
  class Legend : DrawingVisual
  {
    // **********************************************************************

    ViewManager vmgr;
    Cluster cluster;

    DrawingVisual child;

    bool showVolume;
    bool showTicks;
    bool showDelta;
    bool showRange;
    bool showTime;

    Point textOrigin;
    double maxTextWidth;

    FormattedText ftDots;

    // **********************************************************************

    public Legend(ViewManager vmgr, Cluster cluster)
    {
      this.vmgr = vmgr;
      this.cluster = cluster;

      Children.Add(child = new DrawingVisual());

      Rebuild();
    }

    // **********************************************************************

    public void Rebuild()
    {
      showTime = (int)(cfg.u.ClusterLegend & ClusterBase.Time) > 0;

      int upperLines = 0;

      if(showVolume = (int)(cfg.u.ClusterLegend & ClusterBase.Volume) > 0) upperLines++;
      if(showTicks = (int)(cfg.u.ClusterLegend & ClusterBase.Ticks) > 0) upperLines++;
      if(showDelta = (int)(cfg.u.ClusterLegend & ClusterBase.Delta) > 0) upperLines++;
      if(showRange = (int)(cfg.u.ClusterLegend & ClusterBase.Range) > 0) upperLines++;

      StringBuilder sb = new StringBuilder(32);

      for(int i = 0; i < upperLines; i++)
        sb.AppendLine("...");

      ftDots = new FormattedText(
        sb.ToString(),
        cfg.BaseCulture,
        FlowDirection.LeftToRight,
        cfg.BaseFont,
        cfg.u.FontSize,
        cfg.s.ClusterTextBrush);

      Rect r1 = new Rect(cfg.s.ClusterHPadding, 0,
        cfg.u.ClusterWidth - cfg.s.ClusterHPadding * 2,
        Math.Ceiling(ftDots.Height));

      Rect r2 = new Rect(0, 0, cfg.u.ClusterWidth, r1.Height + 1);

      textOrigin = new Point(r1.X + cfg.s.TextHMargin, 0);
      maxTextWidth = r1.Width - cfg.s.TextHMargin * 2;

      using(DrawingContext dc = RenderOpen())
        if(maxTextWidth >= ftDots.Width)
        {
          if(upperLines > 0)
          {
            dc.DrawRectangle(cfg.s.BackBrush, null, r2);
            dc.DrawRectangle(cfg.s.ClusterLegendBrush, null, r1);
          }

          if(showTime)
          {
            FormattedText ft = new FormattedText(
              cluster.DateTime.ToString("HH:mm:ss"),
              cfg.BaseCulture,
              FlowDirection.LeftToRight,
              cfg.BaseFont,
              cfg.u.FontSize,
              cfg.s.ClusterTextBrush);

            if(ft.Width > maxTextWidth)
              ft = new FormattedText(
                cluster.DateTime.ToString("HH:mm"),
                cfg.BaseCulture,
                FlowDirection.LeftToRight,
                cfg.BaseFont,
                cfg.u.FontSize,
                cfg.s.ClusterTextBrush);

            if(maxTextWidth >= ft.Width)
            {
              r1.Height = Math.Ceiling(ft.Height);
              r1.Y = vmgr.Height - r1.Height;

              r2.Height = r1.Height + 1;
              r2.Y = vmgr.Height - r2.Height;

              dc.DrawRectangle(cfg.s.BackBrush, null, r2);
              dc.DrawRectangle(cfg.s.ClusterLegendBrush, null, r1);

              dc.DrawText(ft, new Point(r1.X + cfg.s.TextHMargin, r1.Y));
            }
          }
        }

      Redraw();
    }

    // **********************************************************************

    public void Redraw()
    {
      using(DrawingContext dc = child.RenderOpen())
        if(maxTextWidth >= ftDots.Width)
        {
          StringBuilder sb = new StringBuilder(48);

          if(showVolume)
            sb.AppendLine(cluster.Volume.ToString("N", cfg.BaseCulture));
          if(showTicks)
            sb.AppendLine(cluster.Ticks.ToString("N", cfg.BaseCulture));
          if(showDelta)
            sb.AppendLine(cluster.Delta.ToString("N", cfg.BaseCulture));
          if(showRange)
            sb.AppendLine(Price.GetString(cluster.MaxPrice - cluster.MinPrice));

          FormattedText ft = new FormattedText(
            sb.ToString(),
            cfg.BaseCulture,
            FlowDirection.LeftToRight,
            cfg.BaseFont,
            cfg.u.FontSize,
            cfg.s.ClusterTextBrush);

          if(maxTextWidth >= ft.Width)
          {
            ft.MaxTextWidth = maxTextWidth;
            ft.TextAlignment = TextAlignment.Right;
          }
          else
            ft = ftDots;

          dc.DrawText(ft, textOrigin);
        }
    }

    // **********************************************************************
  }
}
