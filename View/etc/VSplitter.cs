// ========================================================================
//    VSplitter.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// ========================================================================

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace QScalp.View
{
  sealed class VSplitter : FrameworkElement
  {
    // **********************************************************************

    public delegate void DragDoneDelegate(double delta);

    // **********************************************************************

    DragDoneDelegate dragDone;

    DrawingVisual bkg, line;
    Pen pen;

    // **********************************************************************

    VisualCollection children;

    protected override int VisualChildrenCount { get { return children.Count; } }
    protected override Visual GetVisualChild(int index) { return children[index]; }

    // **********************************************************************

    public VSplitter(Pen pen, DragDoneDelegate dragDone)
    {
      this.pen = pen;
      this.dragDone = dragDone;

      children = new VisualCollection(this);

      bkg = new DrawingVisual();
      children.Add(bkg);

      Cursor = Cursors.SizeWE;

      Width = pen.Thickness;
      Height = double.NaN;
    }

    // **********************************************************************

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
      using(DrawingContext dc = bkg.RenderOpen())
        dc.DrawRectangle(pen.Brush, null, new Rect(0, 0, ActualWidth, ActualHeight));

      base.OnRenderSizeChanged(sizeInfo);
    }

    // **********************************************************************

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
      Mouse.Capture(this);

      line = new DrawingVisual();
      children.Add(line);

      using(DrawingContext dc = line.RenderOpen())
        dc.DrawLine(cfg.s.VDragLinePen, new Point(), new Point(0, ActualHeight));

      e.Handled = true;
      base.OnMouseLeftButtonDown(e);
    }

    // **********************************************************************

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
      if(Mouse.Captured == this)
      {
        Mouse.Capture(null);
        e.Handled = true;
      }

      base.OnMouseLeftButtonUp(e);
    }

    // **********************************************************************

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
      children.Remove(line);
      line = null;

      if(dragDone != null)
        dragDone(e.GetPosition(this).X);

      base.OnLostMouseCapture(e);
    }

    // **********************************************************************

    protected override void OnMouseMove(MouseEventArgs e)
    {
      if(IsMouseCaptured)
        line.Offset = new Vector(e.GetPosition(this).X, 0);

      base.OnMouseMove(e);
    }

    // **********************************************************************
  }
}
