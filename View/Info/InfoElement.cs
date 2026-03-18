// ==========================================================================
//    InfoElement.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// ==========================================================================

using System;
using System.Windows;
using System.Windows.Media;

using QScalp.View.InfoSpace;

namespace QScalp.View
{
  sealed class InfoElement : FrameworkElement
  {
    // **********************************************************************

    HGridLines hGrid;
    VPosition position;
    VOrders orders;

    // **********************************************************************

    VisualCollection children;

    protected override int VisualChildrenCount { get { return children.Count; } }
    protected override Visual GetVisualChild(int index) { return children[index]; }

    // **********************************************************************

    public InfoElement(ViewManager vmgr)
    {
      ClipToBounds = true;

      hGrid = new HGridLines(vmgr, false);
      position = new VPosition(vmgr, this);
      orders = new VOrders(vmgr, this);

      children = new VisualCollection(this);
      children.Add(hGrid);
      children.Add(position);
      children.Add(orders);

      MinWidth = Math.Ceiling(cfg.QuoteHeight * cfg.s.VInfoWidthRatio);
    }

    // **********************************************************************

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
      hGrid.SetWidth(ActualWidth);

      if(sizeInfo.WidthChanged)
      {
        position.Rebuild();
        orders.Rebuild();
      }

      base.OnRenderSizeChanged(sizeInfo);
    }

    // **********************************************************************

    public void PutPosition(int quantity, int price)
    {
      position.PutPosition(quantity, price);
    }

    // **********************************************************************

    public void Rebuild()
    {
      MinWidth = Math.Ceiling(cfg.QuoteHeight * cfg.s.VInfoWidthRatio);

      hGrid.Rebuild();
      position.Rebuild();
      orders.Rebuild();
    }

    // **********************************************************************
  }
}
