// ====================================================================
//    CCell.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ====================================================================

using System.Windows;
using System.Windows.Media;

namespace QScalp.View.ClustersSpace
{
  class CCell : DrawingVisual
  {
    // **********************************************************************

    CellBody body;
    CellMark mark;

    // **********************************************************************

    public bool Updated { get; protected set; }

    public int BuyVolume { get { return body.BuyVolume; } }
    public int SellVolume { get { return body.SellVolume; } }

    public void AddBuy(int volume) { body.AddBuy(volume); Updated = true; }
    public void AddSell(int volume) { body.AddSell(volume); Updated = true; }
    public void SetMark(bool visible) { mark.SetState(visible); Updated = true; }

    // **********************************************************************

    public CCell(Brush markBrush)
    {
      body = new CellBody();
      mark = new CellMark(markBrush);

      Children.Add(body);
      Children.Add(mark);

      Rebuild();
    }

    // **********************************************************************

    public void Redraw()
    {
      Updated = false;

      if(body.Updated)
        body.Redraw();

      if(mark.Updated)
        mark.Redraw();
    }

    // **********************************************************************

    public static double MinWidth
    {
      get
      {
        return (cfg.s.ClusterHPadding + cfg.s.ClusterCellPen.Thickness) * 2;
      }
    }

    // **********************************************************************

    public void Rebuild()
    {
      Updated = false;

      double rH = cfg.QuoteHeight - cfg.s.ClusterVPadding * 2;

      Rect rect = new Rect(
        cfg.s.ClusterHPadding + cfg.s.ClusterCellPen.Thickness / 2,
        cfg.s.ClusterVPadding + cfg.s.ClusterCellPen.Thickness / 2,
        cfg.u.ClusterWidth - cfg.s.ClusterHPadding * 2 - cfg.s.ClusterCellPen.Thickness,
        rH - cfg.s.ClusterCellPen.Thickness);

      using(DrawingContext dc = RenderOpen())
        dc.DrawRectangle(cfg.s.ClusterCellBrush, cfg.s.ClusterCellPen, rect);

      body.Reinit(new Rect(
        cfg.s.ClusterHPadding + cfg.s.ClusterCellPen.Thickness,
        cfg.s.ClusterVPadding + cfg.s.ClusterCellPen.Thickness,
        rect.Width - cfg.s.ClusterCellPen.Thickness,
        rect.Height - cfg.s.ClusterCellPen.Thickness));

      mark.Reinit(
        rect.X + rect.Width * cfg.s.ClusterMarkPlace + cfg.s.ClusterMarkShift,
        cfg.QuoteHeight / 2,
        rH * cfg.s.ClusterMarkXRatio - cfg.s.ClusterMarkPen.Thickness / 2,
        rH * cfg.s.ClusterMarkYRatio - cfg.s.ClusterMarkPen.Thickness / 2);
    }

    // **********************************************************************
  }
}
