// =======================================================================
//    CellBody.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =======================================================================

using System;
using System.Windows;
using System.Windows.Media;

namespace QScalp.View.ClustersSpace
{
  class CellBody : DrawingVisual
  {
    // **********************************************************************

    Rect rect;

    int buyVolume, sellVolume;

    public int BuyVolume { get { return buyVolume; } }
    public int SellVolume { get { return sellVolume; } }

    Rect fillRect, fillRect2;
    Brush buyBrush, sellBrush;

    Point textOrigin, textOrigin2, sepPoint0, sepPoint1;
    double maxTextWidth;

    // **********************************************************************

    public bool Updated { get; protected set; }

    public void AddBuy(int volume) { buyVolume += volume; Updated = true; }
    public void AddSell(int volume) { sellVolume += volume; Updated = true; }

    // **********************************************************************

    public void Reinit(Rect rect)
    {
      this.rect = fillRect = fillRect2 = rect;

      buyBrush = cfg.s.ClusterBDeltaBrush.Clone();
      sellBrush = cfg.s.ClusterSDeltaBrush.Clone();

      textOrigin = new Point(
        rect.X + cfg.s.TextHMargin,
        rect.Y + rect.Height / 2 - cfg.TextTopOffset);

      if(cfg.u.ClusterView == ClusterView.Separate)
      {
        double w2 = rect.Width / 2 - rect.Height / 5;
        double x2 = rect.X + rect.Width - w2;

        sepPoint0 = new Point(rect.X + w2, rect.Y + rect.Height);
        sepPoint1 = new Point(x2, rect.Y);

        maxTextWidth = w2 - cfg.s.TextHMargin * 3;

        textOrigin2 = new Point(
          x2 + cfg.s.TextHMargin,
          rect.Y + rect.Height / 2 - cfg.TextTopOffset);
      }
      else
        maxTextWidth = rect.Width - cfg.s.TextHMargin * 2;

      Redraw();
    }

    // **********************************************************************

    public void Redraw()
    {
      Updated = false;

      int sum = buyVolume + sellVolume;
      int delta = buyVolume - sellVolume;

      using(DrawingContext dc = RenderOpen())
      {
        // ----------------------------------------------------------

        if(sum < cfg.u.ClusterFillVolume1)
          fillRect.Width = Math.Round(rect.Width * sum / cfg.u.ClusterFillVolume1);
        else
          fillRect.Width = rect.Width;

        switch(cfg.u.ClusterFill)
        {
          // ----------------------------------------------

          case ClusterFill.Double:
            dc.DrawRectangle(cfg.s.ClusterFillBrush1, null, fillRect);

            if(sum > cfg.u.ClusterFillVolume1)
            {
              if(sum >= cfg.u.ClusterFillVolume2)
                fillRect.Width = rect.Width;
              else
                fillRect.Width = Math.Round(rect.Width * (sum - cfg.u.ClusterFillVolume1)
                  / (cfg.u.ClusterFillVolume2 - cfg.u.ClusterFillVolume1));

              dc.DrawRectangle(cfg.s.ClusterFillBrush2, null, fillRect);
            }

            break;

          // ----------------------------------------------

          case ClusterFill.SingleDelta:
            Brush cb = delta > 0 ? buyBrush : sellBrush;
            cb.Opacity = (double)Math.Abs(delta) / cfg.u.ClusterOpacityDelta;

            dc.DrawRectangle(cb, null, fillRect);

            break;

          // ----------------------------------------------

          case ClusterFill.SingleBalance:
            fillRect2.Width = fillRect.Width * sellVolume / sum;
            fillRect.Width -= fillRect2.Width;
            fillRect2.X = fillRect.X + fillRect.Width;

            dc.DrawRectangle(cfg.s.ClusterBBalanceBrush, null, fillRect);
            dc.DrawRectangle(cfg.s.ClusterSBalanceBrush, null, fillRect2);

            break;

          // ----------------------------------------------
        }

        // ----------------------------------------------------------

        if(maxTextWidth >= cfg.TextMinWidth)
          switch(cfg.u.ClusterView)
          {
            // --------------------------------------------

            case ClusterView.Summary:
              if(sum > cfg.u.ClusterValueFilter)
              {
                FormattedText ft = new FormattedText(
                  sum.ToString("N", cfg.BaseCulture),
                  cfg.BaseCulture,
                  FlowDirection.LeftToRight,
                  cfg.BaseFont,
                  cfg.u.FontSize,
                  cfg.s.ClusterTextBrush);

                ft.MaxTextWidth = maxTextWidth;
                ft.MaxLineCount = 1;

                dc.DrawText(ft, textOrigin);
              }

              break;

            // --------------------------------------------

            case ClusterView.Separate:
              dc.DrawLine(cfg.s.ClusterCellPen, sepPoint0, sepPoint1);

              if(buyVolume > cfg.u.ClusterValueFilter)
              {
                FormattedText ft = new FormattedText(
                  buyVolume.ToString("N", cfg.BaseCulture),
                  cfg.BaseCulture,
                  FlowDirection.LeftToRight,
                  cfg.BaseFont,
                  cfg.u.FontSize,
                  cfg.s.ClusterTextBrush);

                ft.MaxTextWidth = maxTextWidth;
                ft.MaxLineCount = 1;
                ft.TextAlignment = TextAlignment.Right;

                dc.DrawText(ft, textOrigin);
              }

              if(sellVolume > cfg.u.ClusterValueFilter)
              {
                FormattedText ft = new FormattedText(
                  sellVolume.ToString("N", cfg.BaseCulture),
                  cfg.BaseCulture,
                  FlowDirection.LeftToRight,
                  cfg.BaseFont,
                  cfg.u.FontSize,
                  cfg.s.ClusterTextBrush);

                ft.MaxTextWidth = maxTextWidth;
                ft.MaxLineCount = 1;
                dc.DrawText(ft, textOrigin2);
              }

              break;

            // --------------------------------------------

            case ClusterView.Delta:
              if(Math.Abs(delta) > cfg.u.ClusterValueFilter)
              {
                FormattedText ft = new FormattedText(
                  delta.ToString("N", cfg.BaseCulture),
                  cfg.BaseCulture,
                  FlowDirection.LeftToRight,
                  cfg.BaseFont,
                  cfg.u.FontSize,
                  cfg.s.ClusterTextBrush);

                ft.MaxTextWidth = maxTextWidth;
                ft.MaxLineCount = 1;
                dc.DrawText(ft, textOrigin);
              }

              break;

            // --------------------------------------------
          }

        // ----------------------------------------------------------
      }
    }

    // **********************************************************************
  }
}
