// ======================================================================
//    VOrders.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// ======================================================================

using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace QScalp.View.InfoSpace
{
  class VOrders : ContainerVisual, IVScrollable, IOrdersHandler
  {
    // **********************************************************************

    struct MarkData
    {
      public int Price;
      public Brush Brush;
      public Pen Pen;

      public string Text;

      public MarkData(int price, Brush brush, Pen pen)
      {
        this.Price = price;
        this.Brush = brush;
        this.Pen = pen;
        this.Text = string.Empty;
      }
    }

    // **********************************************************************

    ViewManager vmgr;
    FrameworkElement infoElement;

    // **********************************************************************

    public VOrders(ViewManager vmgr, FrameworkElement infoElement)
    {
      this.vmgr = vmgr;
      this.infoElement = infoElement;

      vmgr.RegisterObject(this);
      vmgr.OrdersList.RegisterHandler(this);
    }

    // **********************************************************************

    public void OrdersUpdated(int price)
    {
      IList<OwnOrder> orders = vmgr.OrdersList[price];

      if(orders == null || orders.Count == 0)
      {
        foreach(InfoMark m in Children)
          if(price == ((MarkData)m.Tag).Price)
          {
            Children.Remove(m);
            return;
          }
      }
      else
      {
        // ----------------------------------------------------------

        MarkData md = new MarkData(price, cfg.s.StopOrderBrush, cfg.s.OrderPen);

        int active = 0;

        foreach(OwnOrder order in orders)
        {
          active += order.Active;

          if(order.Id < 0)
            md.Pen = cfg.s.StopOrderPen;
          else
            md.Brush = cfg.s.OrderBrush;
        }

        md.Text = active.ToString("N", cfg.BaseCulture);

        if(orders.Count > 1)
          md.Text += "/" + orders.Count;

        // ----------------------------------------------------------

        InfoMark cm = null;

        foreach(InfoMark m in Children)
          if(price == ((MarkData)m.Tag).Price)
          {
            cm = m;
            break;
          }

        if(cm == null)
        {
          cm = new InfoMark(infoElement);
          cm.Offset = new Vector(0, vmgr.PriceOffset(price));
          Children.Add(cm);
        }

        cm.Tag = md;
        cm.Draw(md.Brush, md.Pen, md.Text);

        // ----------------------------------------------------------
      }
    }

    // **********************************************************************

    public void Rebuild()
    {
      foreach(InfoMark m in Children)
      {
        MarkData md = (MarkData)m.Tag;

        m.Offset = new Vector(0, vmgr.PriceOffset(md.Price));
        m.Draw(md.Brush, md.Pen, md.Text);
      }
    }

    // **********************************************************************

    public void UpdateOffset() { Offset = new Vector(0, vmgr.BaseY); }

    // **********************************************************************
  }
}
