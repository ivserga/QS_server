// ========================================================================
//    TradeBall.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ========================================================================

using System;
using System.Windows;
using System.Windows.Media;

namespace QScalp.View.GraphSpace
{
  class TradeBall : DrawingVisual
  {
    // **********************************************************************

    public readonly int Price;
    public readonly double Radius;

    // **********************************************************************

    public TradeBall(Trade trade)
    {
      this.Price = trade.IntPrice;

      // ------------------------------------------------------------

      FormattedText ft = null;

      if(trade.Quantity < cfg.u.TradeVolume2)
        Radius = cfg.s.TradeBallRadius;
      else if(trade.Quantity < cfg.u.TradeVolume3)
        Radius = cfg.s.TradeBallRadius * 2;
      else
      {
        Radius = cfg.s.TradeBallRadius * 3;

        ft = new FormattedText(
          Math.Round(trade.Quantity / cfg.u.TradeVolume3Div).ToString(),
          cfg.BaseCulture,
          FlowDirection.LeftToRight,
          cfg.BoldFont,
          cfg.u.FontSize,
          cfg.s.TradeTextBrush);

        double tr = Math.Sqrt(Math.Pow(ft.Width + cfg.s.TextHMargin * 2, 2)
          + Math.Pow(ft.Extent + cfg.s.TextVMargin * 2, 2)) / 2;

        if(Radius < tr)
          Radius = tr;

        ft.TextAlignment = TextAlignment.Center;
      }

      // ------------------------------------------------------------

      Brush brush;

      switch(trade.Op)
      {
        case TradeOp.Buy:
          brush = cfg.s.TradeBuyBrush;
          break;
        case TradeOp.Sell:
          brush = cfg.s.TradeSellBrush;
          break;
        default:
          brush = cfg.s.BackBrush;
          break;
      }

      // ------------------------------------------------------------

      using(DrawingContext dc = RenderOpen())
      {
        dc.DrawEllipse(brush, cfg.s.TradeArcPen, new Point(), Radius, Radius);

        if(ft != null)
          dc.DrawText(ft, new Point(0, -cfg.TextTopOffset));
      }

      // ------------------------------------------------------------
    }

    // **********************************************************************
  }
}
