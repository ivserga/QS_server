// ==========================================================================
//    Highlighter.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ==========================================================================

using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace QScalp.View
{
  sealed class Highlighter : FrameworkElement, IVScrollable
  {
    // **********************************************************************

    class Highlight : DrawingVisual
    {
      public Highlight(Brush brush, Pen pen, double width)
      {
        Redraw(brush, pen, width);
      }

      public void Redraw(Brush brush, Pen pen, double width)
      {
        using(DrawingContext dc = RenderOpen())
          dc.DrawRectangle(brush, pen, new Rect(
            -pen.Thickness / 2, pen.Thickness / 2,
            width + pen.Thickness, cfg.QuoteHeight - pen.Thickness));
      }
    }

    // **********************************************************************

    ViewManager vmgr;
    IInputElement mouseOwner;

    double width;

    Highlight mouse;

    ContainerVisual levelsVisual;
    Dictionary<int, Highlight> levelsList;

    // **********************************************************************

    VisualCollection children;

    protected override int VisualChildrenCount { get { return children.Count; } }
    protected override Visual GetVisualChild(int index) { return children[index]; }

    // **********************************************************************

    public Highlighter(ViewManager vmgr, IInputElement mouseOwner)
    {
      this.vmgr = vmgr;
      this.mouseOwner = mouseOwner;

      levelsVisual = new ContainerVisual();
      levelsList = new Dictionary<int, Highlight>();

      children = new VisualCollection(this);
      children.Add(levelsVisual);

      vmgr.RegisterObject(this);
    }

    // **********************************************************************

    public void SetWidth(double width)
    {
      this.width = width;
      Rebuild();
    }

    // **********************************************************************

    public void ShowMouse()
    {
      if(mouse == null)
      {
        mouse = new Highlight(cfg.s.HlMouseBrush, cfg.s.HlMousePen, width);
        children.Add(mouse);

        vmgr.DisableCentering();
      }

      mouse.Offset = new Vector(0, vmgr.PriceY(
        vmgr.PriceFromY(Mouse.GetPosition(mouseOwner).Y)));
    }

    // **********************************************************************

    public void UpdateMouse()
    {
      if(mouse != null)
        mouse.Offset = new Vector(0, vmgr.PriceY(
          vmgr.PriceFromY(Mouse.GetPosition(mouseOwner).Y)));
    }

    // **********************************************************************

    public void HideMouse()
    {
      if(mouse != null)
      {
        children.Remove(mouse);
        mouse = null;

        vmgr.RestoreCentering();
      }
    }

    // **********************************************************************

    public void LockLevel()
    {
      Highlight hl;

      int price = vmgr.PriceFromY(Mouse.GetPosition(mouseOwner).Y);

      if(levelsList.TryGetValue(price, out hl))
      {
        levelsList.Remove(price);
        levelsVisual.Children.Remove(hl);
      }
      else
      {
        hl = new Highlight(cfg.s.HlLevelBrush, cfg.s.HlLevelPen, width);
        hl.Offset = new Vector(0, vmgr.PriceOffset(price));

        levelsList.Add(price, hl);
        levelsVisual.Children.Add(hl);
      }
    }

    // **********************************************************************

    public void Clear()
    {
      levelsList.Clear();
      levelsVisual.Children.Clear();
    }

    // **********************************************************************

    public void Rebuild()
    {
      HideMouse();

      foreach(KeyValuePair<int, Highlight> kvp in levelsList)
      {
        kvp.Value.Redraw(cfg.s.HlLevelBrush, cfg.s.HlLevelPen, width);
        kvp.Value.Offset = new Vector(0, vmgr.PriceOffset(kvp.Key));
      }

      UpdateOffset();
    }

    // **********************************************************************

    public void UpdateOffset()
    {
      levelsVisual.Offset = new Vector(0, vmgr.BaseY);
      UpdateMouse();
    }

    // **********************************************************************
  }
}
