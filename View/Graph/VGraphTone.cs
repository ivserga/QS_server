// =========================================================================
//    VGraphTone.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// =========================================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace QScalp.View.GraphSpace
{
  class VGraphTone : ContainerVisual
  {
    // **********************************************************************

    ViewManager vmgr;

    LinkedList<ToneMeter.Summand> summands;
    DispatcherTimer decreaser;

    // **********************************************************************

    public VGraphTone(ViewManager vmgr)
    {
      this.vmgr = vmgr;

      summands = new LinkedList<ToneMeter.Summand>();

      decreaser = new DispatcherTimer();
      decreaser.Tick += new EventHandler(DecreaserTick);
      decreaser.Interval = new TimeSpan(0, 0, 0, 0, cfg.s.ToneDecreaseInterval);

      Rebuild();
    }

    // **********************************************************************

    public void UpdateHeight()
    {
      foreach(ToneMeter tm in Children)
        tm.Redraw();
    }

    // **********************************************************************

    void DecreaserTick(object sender, EventArgs e)
    {
      DateTime now = DateTime.UtcNow;

      while(summands.Count > 0 && summands.First.Value.TryExpire(now))
        summands.RemoveFirst();
    }

    // **********************************************************************

    public void Rebuild()
    {
      // --------------------------------------------------

      decreaser.Stop();

      foreach(ToneMeter tm in Children)
      {
        vmgr.TradesQueue.UnregisterHandler(tm);
        vmgr.UnregisterObject(tm);
      }

      Children.Clear();

      // --------------------------------------------------

      double x = cfg.QuoteHeight;

      for(int i = 0; i < cfg.u.ToneSources.Length; i++)
      {
        ToneMeter tm = new ToneMeter(vmgr, summands,
          cfg.u.ToneSources[i].Interval, cfg.u.ToneSources[i].FillVolume);

        tm.Offset = new Vector(x, 0);

        vmgr.RegisterObject(tm);
        vmgr.TradesQueue.RegisterHandler(tm,
          cfg.u.ToneSources[i].SecCode + cfg.u.ToneSources[i].ClassCode);

        Children.Add(tm);

        x += Math.Round(cfg.QuoteHeight * cfg.s.ToneMeterPlaceRatio);
      }

      if(Children.Count > 0)
        decreaser.Start();

      // --------------------------------------------------
    }

    // **********************************************************************
  }
}
