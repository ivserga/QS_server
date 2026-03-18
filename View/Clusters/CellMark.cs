// =======================================================================
//    CellMark.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =======================================================================

using System.Windows;
using System.Windows.Media;

namespace QScalp.View.ClustersSpace
{
  class CellMark : DrawingVisual
  {
    // **********************************************************************

    readonly Brush brush;

    Point center;
    double rX, rY;

    bool isVisible, newState;

    // **********************************************************************

    public bool Updated { get { return isVisible != newState; } }
    public void SetState(bool visible) { newState = visible; }

    // **********************************************************************

    public CellMark(Brush brush)
    {
      this.brush = brush;
      this.newState = true;
    }

    // **********************************************************************

    public void Reinit(double x, double y, double rX, double rY)
    {
      this.center = new Point(x, y);
      this.rX = rX;
      this.rY = rY;

      Redraw();
    }

    // **********************************************************************

    public void Redraw()
    {
      using(DrawingContext dc = RenderOpen())
        if(isVisible = newState)
          dc.DrawEllipse(brush, cfg.s.ClusterMarkPen, center, rX, rY);
    }

    // **********************************************************************
  }
}
