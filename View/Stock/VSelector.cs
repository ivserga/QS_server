// ========================================================================
//    VSelector.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ========================================================================

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace QScalp.View.StockSpace
{
  class VSelector : DrawingVisual, IVScrollable
  {
    // **********************************************************************

    ViewManager vmgr;
    IInputElement mouseOwner;

    bool isActive;

    // **********************************************************************

    public int Price { get; protected set; }

    // **********************************************************************

    public VSelector(ViewManager vmgr, IInputElement mouseOwner)
    {
      this.vmgr = vmgr;
      this.mouseOwner = mouseOwner;

      vmgr.RegisterObject(this);
    }

    // **********************************************************************

    public void Show()
    {
      using(DrawingContext dc = RenderOpen())
        dc.DrawRectangle(null, cfg.s.SelectorPen, new Rect(
          cfg.s.SelectorPen.Thickness / 2,
          cfg.s.SelectorPen.Thickness / 2,
          StockQuote.Width - cfg.s.SelectorPen.Thickness,
          cfg.QuoteHeight - cfg.s.SelectorPen.Thickness));

      if(!isActive)
      {
        vmgr.DisableCentering();
        isActive = true;
      }
    }

    // **********************************************************************

    public void Hide()
    {
      if(isActive)
      {
        using(DrawingContext dc = RenderOpen()) { }
        vmgr.RestoreCentering();

        Price = 0;
        isActive = false;
      }
    }

    // **********************************************************************

    public void UpdateOffset()
    {
      if(isActive)
      {
        Price = vmgr.PriceFromY(Mouse.GetPosition(mouseOwner).Y);
        Offset = new Vector(0, vmgr.PriceY(Price));
      }
    }

    // **********************************************************************

    public void Rebuild()
    {
      if(isActive)
      {
        Price = vmgr.PriceFromY(Mouse.GetPosition(mouseOwner).Y);
        Offset = new Vector(0, vmgr.PriceY(Price));

        Show();
      }
    }

    // **********************************************************************
  }
}
