// =========================================================================
//   StockElement.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =========================================================================

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using QScalp.View.StockSpace;

namespace QScalp.View
{
  sealed class StockElement : FrameworkElement
  {
    // **********************************************************************

    ViewManager vmgr;

    VStock stock;
    Separator separator;
    VSelector selector;

    // **********************************************************************

    public int SelectedPrice { get { return selector.Price; } }

    // **********************************************************************

    VisualCollection children;

    protected override int VisualChildrenCount { get { return children.Count; } }
    protected override Visual GetVisualChild(int index) { return children[index]; }

    // **********************************************************************

    public StockElement(ViewManager vmgr)
    {
      this.vmgr = vmgr;

      stock = new VStock(vmgr);
      separator = new Separator();
      selector = new VSelector(vmgr, this);

      children = new VisualCollection(this);
      children.Add(stock);
      children.Add(separator);
      children.Add(selector);

      Cursor = Cursors.Cross;
      ClipToBounds = true;

      UpdateWidth();
    }

    // **********************************************************************

    public void PutQuotes(Quote[] quotes) { stock.PutQuotes(quotes); }

    public void ClearQuotes() { stock.Clear(); }

    // **********************************************************************

    public void UpdateWidth()
    {
      if(cfg.u.VQuotePriceWidth < 0)
        cfg.u.VQuotePriceWidth = 0;

      if(cfg.u.VQuoteVolumeWidth < 0)
        cfg.u.VQuoteVolumeWidth = 0;

      Width = StockQuote.Width;
    }

    // **********************************************************************

    public void Rebuild()
    {
      UpdateWidth();

      stock.Rebuild();
      separator.Redraw(vmgr.Height);
      selector.Rebuild();
    }

    // **********************************************************************

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
      base.OnRenderSizeChanged(sizeInfo);

      if(sizeInfo.WidthChanged)
      {
        stock.Rebuild();
        selector.Rebuild();
      }
      else
        stock.UpdateOffset();

      separator.Redraw(vmgr.Height);

      if(!vmgr.DataExist)
        vmgr.CenterSpread();
    }

    // **********************************************************************

    protected override void OnMouseMove(MouseEventArgs e)
    {
      selector.UpdateOffset();
      base.OnMouseMove(e);
    }

    // **********************************************************************

    protected override void OnMouseEnter(MouseEventArgs e)
    {
      if(vmgr.DataExist)
        selector.Show();

      base.OnMouseEnter(e);
    }

    // **********************************************************************

    protected override void OnMouseLeave(MouseEventArgs e)
    {
      selector.Hide();
      base.OnMouseLeave(e);
    }

    // **********************************************************************
  }
}
