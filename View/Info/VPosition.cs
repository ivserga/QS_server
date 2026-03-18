// ========================================================================
//    VPosition.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// ========================================================================

using System;
using System.Windows;
using System.Windows.Media;

namespace QScalp.View.InfoSpace
{
  class VPosition : ContainerVisual, IVObsoletable, IVScrollable, IDataHandler<Spread>
  {
    // **********************************************************************

    ViewManager vmgr;
    FrameworkElement infoElement;

    int quantity;
    int price;

    InfoMark pMark;
    InfoMark rMark;
    DrawingVisual mConnector;

    // **********************************************************************

    public bool Obsolete { get; protected set; }

    // **********************************************************************

    public VPosition(ViewManager vmgr, FrameworkElement infoElement)
    {
      this.vmgr = vmgr;
      this.infoElement = infoElement;

      vmgr.RegisterObject(this);
      vmgr.SpreadsQueue.RegisterHandler(this);
    }

    // **********************************************************************

    public void PutPosition(int quantity, int price)
    {
      this.quantity = quantity;
      this.price = price;

      Obsolete = true;
    }

    // **********************************************************************

    public void Refresh()
    {
      Obsolete = false;

      if(quantity == 0)
      {
        Children.Clear();

        pMark = null;
        rMark = null;
        mConnector = null;
      }
      else
      {
        // ----------------------------------------------------------

        double pY = Math.Round(vmgr.PriceOffset(price));

        double y1, y2, rY;
        string rText, pText;
        Brush rb;

        // ----------------------------------------------------------

        if(quantity > 0)
        {
          pText = "L " + quantity.ToString("N", cfg.BaseCulture);
          rText = Price.GetString(vmgr.Bid - price);

          rY = vmgr.PriceOffset(vmgr.Bid);

          if(price < vmgr.Bid)
          {
            rb = cfg.s.ResultPBrush;
            y1 = rY;
            y2 = pY - cfg.s.ResultPen.Thickness;
          }
          else
          {
            rb = cfg.s.ResultLBrush;
            y1 = pY + cfg.s.ResultPen.Thickness;
            y2 = rY;
          }
        }
        else
        {
          pText = "S " + (-quantity).ToString("N", cfg.BaseCulture);
          rText = Price.GetString(price - vmgr.Ask);

          rY = vmgr.PriceOffset(vmgr.Ask);

          if(price > vmgr.Ask)
          {
            rb = cfg.s.ResultPBrush;
            y1 = pY + cfg.s.ResultPen.Thickness;
            y2 = rY;
          }
          else
          {
            rb = cfg.s.ResultLBrush;
            y1 = rY;
            y2 = pY - cfg.s.ResultPen.Thickness;
          }
        }

        // ----------------------------------------------------------

        if(rMark == null)
        {
          rMark = new InfoMark(infoElement);
          Children.Add(rMark);
        }

        if(pMark == null)
        {
          pMark = new InfoMark(infoElement);
          Children.Add(pMark);
        }

        if(mConnector == null)
        {
          mConnector = new DrawingVisual();
          Children.Add(mConnector);
        }

        // ----------------------------------------------------------

        rMark.Offset = new Vector(0, rY);
        pMark.Offset = new Vector(0, pY);

        rMark.Draw(rb, cfg.s.ResultPen, rText);
        pMark.Draw(cfg.s.PositionBrush, cfg.s.PositionPen, pText);

        using(DrawingContext dc = mConnector.RenderOpen())
        {
          double adj = cfg.s.ResultPen.Thickness / 2;

          double cx1 = infoElement.ActualWidth - cfg.QuoteHeight;
          double cy1 = y1 + cfg.QuoteHeight;
          double cx2 = infoElement.ActualWidth;
          double cy2 = y2 + cfg.s.ResultPen.Thickness;

          if(cy2 > cy1)
          {
            cy1 -= cfg.s.ResultPen.Thickness;

            dc.DrawRectangle(rb, null, new Rect(
              cx1 + cfg.s.ResultPen.Thickness,
              cy1,
              cfg.QuoteHeight - cfg.s.ResultPen.Thickness * 2,
              cy2 - cy1));

            cx1 += adj;
            cx2 -= adj;

            dc.DrawLine(cfg.s.ResultPen, new Point(cx1, cy1), new Point(cx1, cy2));
            dc.DrawLine(cfg.s.ResultPen, new Point(cx2, cy1), new Point(cx2, cy2));
          }
        }

        // ----------------------------------------------------------
      }
    }

    // **********************************************************************

    public void PutData(Spread data) { if(quantity != 0) Obsolete = true; }
    public void Rebuild() { Refresh(); }
    public void UpdateOffset() { Offset = new Vector(0, vmgr.BaseY); }

    // **********************************************************************
  }
}
