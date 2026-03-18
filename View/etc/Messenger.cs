// ========================================================================
//    Messenger.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ========================================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace QScalp.View
{
  sealed class Messenger : FrameworkElement, IDataHandler<Message>
  {
    // **********************************************************************

    readonly double dblMargin;
    double maxTextWidth;

    DrawingVisual body, text;
    List<FormattedText> messages;

    int count;

    SolidColorBrush bkgBrush;
    Pen bkgPen;

    DispatcherTimer hideTimer, fadeTimer;

    Point mousePosition;

    // **********************************************************************

    protected override int VisualChildrenCount { get { return 1; } }
    protected override Visual GetVisualChild(int index) { return body; }

    // **********************************************************************

    public Messenger(ViewManager vmgr)
    {
      dblMargin = cfg.s.MsgrTextMargin * 2;

      AddVisualChild(body = new DrawingVisual());
      body.Children.Add(text = new DrawingVisual());

      messages = new List<FormattedText>();

      bkgBrush = cfg.s.MsgrBodyBrush.Clone();
      bkgPen = cfg.s.MsgrBodyPen.Clone();

      hideTimer = new DispatcherTimer();
      hideTimer.Tick += new EventHandler(HideTimerTick);
      hideTimer.Interval = TimeSpan.FromMilliseconds(cfg.s.MsgrShowTime);

      fadeTimer = new DispatcherTimer();
      fadeTimer.Tick += new EventHandler(FadeTimerTick);
      fadeTimer.Interval = TimeSpan.FromMilliseconds(cfg.s.MsgrFadeTick);

      vmgr.MsgQueue.RegisterHandler(this);
    }

    // **********************************************************************

    void HideTimerTick(object sender, EventArgs e)
    {
      hideTimer.Stop();
      fadeTimer.Start();
    }

    // **********************************************************************

    void FadeTimerTick(object sender, EventArgs e)
    {
      if(Mouse.DirectlyOver != this)
      {
        body.Opacity -= cfg.s.MsgrFadeStep;

        if(body.Opacity <= 0)
          Clear();
      }
    }

    // **********************************************************************

    void Clear()
    {
      hideTimer.Stop();
      fadeTimer.Stop();

      using(DrawingContext dc = body.RenderOpen()) { }
      using(DrawingContext dc = text.RenderOpen()) { }

      body.Offset = new Vector();
      messages.Clear();

      if(Mouse.Captured == this)
        Mouse.Capture(null);
    }

    // **********************************************************************

    public void PutData(Message data)
    {
      hideTimer.Stop();
      fadeTimer.Stop();

      string header = data.DateTime.ToString("HH:mm:ss.fff #") + (++count);

      FormattedText ft = new FormattedText(
        header + "\n" + data.Text,
        cfg.BaseCulture,
        FlowDirection.LeftToRight,
        cfg.BaseFont,
        cfg.u.FontSize * cfg.s.MsgrFontSizeRatio,
        cfg.s.MsgrErrorBrush);

      //ft.SetFontTypeface(cfg.BoldFont, 0, header.Length);
      ft.SetForegroundBrush(cfg.s.MsgrHeaderBrush, 0, header.Length);

      messages.Add(ft);

      body.Opacity = 1;
      Redraw();

      hideTimer.Start();
    }

    // **********************************************************************

    public void SetWidth(double width)
    {
      maxTextWidth = width - dblMargin - dblMargin;

      if(messages.Count > 0)
        Redraw();
    }

    // **********************************************************************

    void Redraw()
    {
      Point p = new Point(dblMargin, dblMargin);

      double tw = 0;

      using(DrawingContext dc = text.RenderOpen())
        foreach(FormattedText msg in messages)
        {
          if(msg.MinWidth > maxTextWidth)
            msg.MaxTextWidth = msg.MinWidth;
          else
            msg.MaxTextWidth = maxTextWidth;

          if(msg.Width > tw)
            tw = msg.Width;

          dc.DrawText(msg, p);
          p.Y += Math.Ceiling(msg.Height + cfg.s.MsgrTextMargin);
        }

      using(DrawingContext dc = body.RenderOpen())
        dc.DrawRectangle(bkgBrush, bkgPen, new Rect(
          cfg.s.MsgrTextMargin,
          cfg.s.MsgrTextMargin,
          Math.Ceiling(tw + dblMargin),
          p.Y - cfg.s.MsgrTextMargin));
    }

    // **********************************************************************

    protected override void OnMouseEnter(MouseEventArgs e)
    {
      bkgBrush.Color = cfg.s.MsgrLockBrush.Color;
      bkgPen.Brush = cfg.s.MsgrLockPen.Brush;
      bkgPen.Thickness = cfg.s.MsgrLockPen.Thickness;

      body.Opacity = 1;

      e.Handled = true;
      base.OnMouseEnter(e);
    }

    // **********************************************************************

    protected override void OnMouseLeave(MouseEventArgs e)
    {
      bkgBrush.Color = cfg.s.MsgrBodyBrush.Color;
      bkgPen.Brush = cfg.s.MsgrBodyPen.Brush;
      bkgPen.Thickness = cfg.s.MsgrBodyPen.Thickness;

      e.Handled = true;
      base.OnMouseLeave(e);
    }

    // **********************************************************************

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
      if(e.ChangedButton == MouseButton.Left)
      {
        mousePosition = e.GetPosition(this);
        Mouse.Capture(this);
        Cursor = Cursors.Hand;
      }

      e.Handled = true;
      base.OnMouseDown(e);
    }

    // **********************************************************************

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
      switch(e.ChangedButton)
      {
        case MouseButton.Left:
          if(Mouse.Captured == this)
            Mouse.Capture(null);
          break;

        case MouseButton.Right:
          Clear();
          break;
      }

      e.Handled = true;
      base.OnMouseUp(e);
    }

    // **********************************************************************

    protected override void OnMouseMove(MouseEventArgs e)
    {
      if(Mouse.Captured == this)
      {
        Point p = e.GetPosition(this);
        body.Offset += p - mousePosition;
        mousePosition = p;

        e.Handled = true;
      }

      base.OnMouseMove(e);
    }

    // **********************************************************************

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
      Cursor = Cursors.Arrow;

      e.Handled = true;
      base.OnLostMouseCapture(e);
    }

    // **********************************************************************
  }
}
