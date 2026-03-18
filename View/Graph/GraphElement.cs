// =========================================================================
//   GraphElement.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// =========================================================================

using System.Windows;
using System.Windows.Media;

using QScalp.View.GraphSpace;

namespace QScalp.View
{
  sealed class GraphElement : FrameworkElement
  {
    // **********************************************************************

    HGridLines hGrid;

    VGraphSpreads spreads;
    VGraphGuide guide;
    VGraphTrades trades;
    VGraphTone tone;

    // **********************************************************************

    VisualCollection children;

    protected override int VisualChildrenCount { get { return children.Count; } }
    protected override Visual GetVisualChild(int index) { return children[index]; }

    // **********************************************************************

    public GraphElement(ViewManager vmgr)
    {
      ClipToBounds = true;

      hGrid = new HGridLines(vmgr, true);

      spreads = new VGraphSpreads(vmgr);
      guide = new VGraphGuide(vmgr);
      trades = new VGraphTrades(vmgr);
      tone = new VGraphTone(vmgr);

      children = new VisualCollection(this);
      children.Add(hGrid);
      children.Add(spreads);
      children.Add(guide);
      children.Add(trades);
      children.Add(tone);
    }

    // **********************************************************************

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
      base.OnRenderSizeChanged(sizeInfo);

      hGrid.SetWidth(ActualWidth);

      if(sizeInfo.WidthChanged)
      {
        spreads.SetWidth(ActualWidth);
        guide.SetWidth(ActualWidth);
        trades.SetWidth(ActualWidth);
      }

      if(sizeInfo.HeightChanged)
        tone.UpdateHeight();
    }

    // **********************************************************************

    public void Rebuild()
    {
      hGrid.Rebuild();
      spreads.Rebuild();
      guide.Rebuild();
      trades.Rebuild();
      tone.Rebuild();
    }

    // **********************************************************************

    public void ClearGuide() { guide.Clear(); }

    // **********************************************************************
  }
}
