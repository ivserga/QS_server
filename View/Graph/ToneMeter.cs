// ========================================================================
//    ToneMeter.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ========================================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace QScalp.View.GraphSpace
{
  class ToneMeter : DrawingVisual, IVObsoletable, ITradesHandler
  {
    // **********************************************************************

    public struct Summand
    {
      readonly DateTime dateTime;
      readonly TradeOp op;
      readonly int volume;
      readonly ToneMeter toneMeter;

      public Summand(DateTime dateTime, TradeOp op, int volume, ToneMeter toneMeter)
      {
        this.dateTime = dateTime;
        this.op = op;
        this.volume = volume;
        this.toneMeter = toneMeter;
      }

      public bool TryExpire(DateTime now)
      {
        if(now - dateTime >= toneMeter.interval)
        {
          switch(op)
          {
            case TradeOp.Buy: toneMeter.buyVolume -= volume; break;
            case TradeOp.Sell: toneMeter.sellVolume -= volume; break;
          }

          toneMeter.Obsolete = true;

          return true;
        }
        else
          return false;
      }
    }

    // **********************************************************************

    ViewManager vmgr;
    LinkedList<Summand> summands;

    double buyVolume;
    double sellVolume;

    TimeSpan interval;
    double fillVolume;

    double mX, bmY, smY, mW, mH;

    DrawingVisual meter;

    // **********************************************************************

    public bool Obsolete { get; protected set; }

    // **********************************************************************

    public ToneMeter(ViewManager vmgr, LinkedList<Summand> summands, int interval, int fillVolume)
    {
      this.vmgr = vmgr;
      this.summands = summands;

      this.interval = new TimeSpan(0, 0, 0, 0, interval);
      this.fillVolume = fillVolume;

      meter = new DrawingVisual();
      Children.Add(meter);

      Redraw();
    }

    // **********************************************************************

    public void PutTrade(Trade trade, int count)
    {
      switch(trade.Op)
      {
        case TradeOp.Buy: buyVolume += trade.Quantity; break;
        case TradeOp.Sell: sellVolume += trade.Quantity; break;
      }

      summands.AddLast(new Summand(DateTime.UtcNow, trade.Op, trade.Quantity, this));

      Obsolete = true;
    }

    // **********************************************************************

    public void Refresh()
    {
      Obsolete = false;

      // ------------------------------------------------------------

      double b = buyVolume / fillVolume;
      double s = sellVolume / fillVolume;

      double bmOverload;
      double smOverload;

      if(b > 1)
      {
        bmOverload = b - 1;
        b = 1;
      }
      else
        bmOverload = 0;

      if(s > 1)
      {
        smOverload = s - 1;
        s = 1;
      }
      else
        smOverload = 0;

      // ------------------------------------------------------------

      using(DrawingContext dc = meter.RenderOpen())
      {
        // ------------------------------------------------

        double bmh = mH * b;
        dc.DrawRectangle(cfg.s.ToneBullBrush, null, new Rect(mX, bmY + mH - bmh, mW, bmh));

        if(bmOverload > 0)
        {
          double dm = cfg.s.ToneOverloadMargin * 2;

          double x = mX + cfg.s.ToneOverloadMargin;
          double y = bmY + cfg.s.ToneOverloadMargin;
          double w = mW - dm;

          while(bmOverload > 0)
          {
            double h = w * (bmOverload > 1 ? 1 : bmOverload);

            if(y + h >= bmY + mH && (h = bmY + mH - y) <= 0)
              break;

            dc.DrawRectangle(cfg.s.ToneBullOvrBrush, null, new Rect(x, y, w, h));

            y += mW - cfg.s.ToneOverloadMargin;
            bmOverload--;
          }
        }

        // ------------------------------------------------

        dc.DrawRectangle(cfg.s.ToneBearBrush, null, new Rect(mX, smY, mW, mH * s));

        if(smOverload > 0)
        {
          double dm = cfg.s.ToneOverloadMargin * 2;

          double x = mX + cfg.s.ToneOverloadMargin;
          double y = smY + mH - mW + cfg.s.ToneOverloadMargin;
          double w = mW - dm;

          while(smOverload > 0)
          {
            double h = w * (smOverload > 1 ? 1 : smOverload);

            if(y + w - h < smY && (h = y + w - smY) < 0)
              break;

            dc.DrawRectangle(cfg.s.ToneBearOvrBrush, null, new Rect(x, y + w - h, w, h));

            y -= mW - cfg.s.ToneOverloadMargin;
            smOverload--;
          }
        }

        // ------------------------------------------------
      }

      // ------------------------------------------------------------
    }

    // **********************************************************************

    public void Redraw()
    {
      double hbt = cfg.s.ToneBorderPen.Thickness / 2;
      double dbt = cfg.s.ToneBorderPen.Thickness * 2;

      mX = cfg.s.ToneBorderPen.Thickness;
      mW = cfg.QuoteHeight - dbt;

      mH = Math.Floor(vmgr.Height / 2) - cfg.QuoteHeight - dbt;

      if(mH > cfg.u.ToneMeterHeight)
        mH = cfg.u.ToneMeterHeight;
      else if(mH < 0)
        mH = 0;

      bmY = cfg.QuoteHeight + cfg.s.ToneBorderPen.Thickness;
      smY = vmgr.Height - cfg.QuoteHeight - mH - cfg.s.ToneBorderPen.Thickness;

      using(DrawingContext dc = RenderOpen())
      {
        Rect bkg = new Rect(
          hbt,
          cfg.QuoteHeight + hbt,
          cfg.QuoteHeight - cfg.s.ToneBorderPen.Thickness,
          mH + cfg.s.ToneBorderPen.Thickness);

        dc.DrawRectangle(cfg.s.ToneBkgBrush, cfg.s.ToneBorderPen, bkg);

        bkg.Y = smY - hbt;
        dc.DrawRectangle(cfg.s.ToneBkgBrush, cfg.s.ToneBorderPen, bkg);
      }

      Refresh();
    }

    // **********************************************************************
  }
}
