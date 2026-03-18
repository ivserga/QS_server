// ==========================================================================
//    ViewManager.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace QScalp.View
{
  // ************************************************************************
  // *                             Interfaces                               *
  // ************************************************************************

  interface IVObsoletable { bool Obsolete { get; } void Refresh(); }
  interface IVScrollable { void UpdateOffset(); }


  // ************************************************************************
  // *                            ViewManager                               *
  // ************************************************************************

  class ViewManager
  {
    // **********************************************************************

    FrameworkElement owner;

    int acDsblCount;
    bool acLastState;
    double acOffset;

    DispatcherTimer refreshing;

    List<IVObsoletable> obsoletables;
    List<IVScrollable> scrollables;

    // **********************************************************************

    public DataQueue<Message> MsgQueue { get; protected set; }
    public DataQueue<Spread> SpreadsQueue { get; protected set; }
    public TradesQueue TradesQueue { get; protected set; }
    public OrdersList OrdersList { get; protected set; }

    public int Ask { get; protected set; }
    public int Bid { get; protected set; }

    public bool DataExist { get { return Ask > 0; } }

    public void SetSpread(Spread s) { Ask = s.Ask; Bid = s.Bid; }

    public bool AutoCentering { get; set; }

    public double BaseY { get; protected set; }
    public double Height { get { return owner.ActualHeight; } }
    public double Width { get { return owner.ActualWidth; } }

    // **********************************************************************

    public ViewManager(FrameworkElement owner)
    {
      this.owner = owner;

      obsoletables = new List<IVObsoletable>();
      scrollables = new List<IVScrollable>();

      MsgQueue = new DataQueue<Message>();
      SpreadsQueue = new DataQueue<Spread>();
      TradesQueue = new TradesQueue();
      OrdersList = new OrdersList();

      AutoCentering = true;

      refreshing = new DispatcherTimer();
      refreshing.Interval = cfg.RefreshInterval;
      refreshing.Tick += new EventHandler(RefreshTick);

      refreshing.Start();
    }

    // **********************************************************************

    public void RegisterObject(Visual obj)
    {
      if(obj is IVObsoletable)
        obsoletables.Add((IVObsoletable)obj);

      if(obj is IVScrollable)
        scrollables.Add((IVScrollable)obj);
    }

    // **********************************************************************

    public void UnregisterObject(Visual obj)
    {
      if(obj is IVObsoletable)
        obsoletables.Remove((IVObsoletable)obj);

      if(obj is IVScrollable)
        scrollables.Remove((IVScrollable)obj);
    }

    // **********************************************************************

    void RefreshTick(object sender, EventArgs e)
    {
      // ------------------------------------------------------------

      if(AutoCentering)
      {
        double askY = PriceY(Ask);
        double bidY = PriceY(Bid);

        if((askY < Height * cfg.s.CenteringStart)
          ^ (bidY + cfg.QuoteHeight > Height * (1 - cfg.s.CenteringStart)))
        {
          acOffset = (Height - cfg.QuoteHeight - askY - bidY) / 2;

          if(Math.Abs(acOffset) > Height * 2)
            CenterSpread();
        }

        if(acOffset != 0)
        {
          double offset = acOffset / cfg.s.CenteringDiv;

          if(Math.Abs(offset) < cfg.s.CenteringMin)
            if(Math.Abs(acOffset) < cfg.s.CenteringMin)
              offset = acOffset;
            else
              offset = Math.Sign(acOffset) * cfg.s.CenteringMin;

          acOffset -= offset;
          Scroll(offset);
        }
      }

      // ------------------------------------------------------------

      if(MsgQueue.Length > 0)
        MsgQueue.UpdateHandlers();

      if(SpreadsQueue.Length > 0)
        SpreadsQueue.UpdateHandlers();

      if(TradesQueue.DataExist)
        TradesQueue.UpdateHandlers();

      if(OrdersList.QueueLength > 0)
        OrdersList.UpdateHandlers();

      // ------------------------------------------------------------

      for(int i = 0; i < obsoletables.Count; i++)
        if(obsoletables[i].Obsolete)
          obsoletables[i].Refresh();

      // ------------------------------------------------------------
    }

    // **********************************************************************

    public double PriceY(int price)
    {
      return BaseY - price * cfg.QuoteHeight / cfg.u.PriceStep;
    }

    // **********************************************************************

    public double PriceOffset(int price)
    {
      return -price * cfg.QuoteHeight / cfg.u.PriceStep;
    }

    // **********************************************************************

    public int PriceFromY(double y)
    {
      return (int)Math.Floor((BaseY - 1 - y) / cfg.QuoteHeight + 1) * cfg.u.PriceStep;
    }

    // **********************************************************************

    public bool QVisible(int price)
    {
      double y = PriceY(price);
      return y + cfg.QuoteHeight > 0 && y < Height;
    }

    // **********************************************************************

    public void Scroll(double offset)
    {
      BaseY += Math.Round(offset);

      for(int i = 0; i < scrollables.Count; i++)
        scrollables[i].UpdateOffset();
    }

    // **********************************************************************

    public void CenterSpread()
    {
      acOffset = 0;

      Scroll((cfg.QuoteHeight * ((Ask + Bid) / cfg.u.PriceStep - 1)
        + Height) / 2 - BaseY);
    }

    // **********************************************************************

    /// <summary>
    /// Сбрасывает состояние данных (Ask/Bid) для загрузки новых
    /// </summary>
    public void ResetDataState()
    {
      Ask = 0;
      Bid = 0;
    }

    // **********************************************************************

    /// <summary>
    /// Очищает все очереди данных (для перезагрузки за другую дату)
    /// </summary>
    public void ClearQueues()
    {
      // Очищаем очереди чтобы старые данные не обрабатывались
      TradesQueue.Clear();
      SpreadsQueue.Clear();
    }

    // **********************************************************************

    public void DisableCentering()
    {
      if(acDsblCount++ == 0)
        acLastState = AutoCentering;

      AutoCentering = false;
    }

    // **********************************************************************

    public void RestoreCentering()
    {
      if(--acDsblCount == 0)
        AutoCentering = acLastState;
    }

    // **********************************************************************
  }
}
