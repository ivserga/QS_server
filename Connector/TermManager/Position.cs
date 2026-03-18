// =======================================================================
//    Position.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =======================================================================

using System;
using QScalp.TradeLogSpace;

namespace QScalp.Connector
{
  class Position
  {
    // **********************************************************************

    IDataReceiver dataReceiver;
    TermManager tmgr;

    int quantity;
    int pricesum;

    int byOrders;
    bool byOrdersUpdated;

    int stopOrderId;
    int stopOrderPrice;

    TermManager.Transaction takeProfit;

    bool stopActivated, stopCanceled;
    bool takeActivated, takeCanceled;

    // **********************************************************************

    public TradeLog TradeLog { get; protected set; }

    // **********************************************************************

    public Position(TermManager tmgr, IDataReceiver dataReceiver)
    {
      this.tmgr = tmgr;
      this.dataReceiver = dataReceiver;

      TradeLog = new TradeLog();

      byOrdersUpdated = true;
    }

    // **********************************************************************

    public void PutOwnTrade(OwnTrade trade)
    {
      // ------------------------------------------------------------

      int nq = this.quantity + trade.Quantity;

      if(Math.Sign(nq) != Math.Sign(this.quantity))
      {
        // ------------------------------------------------

        if(this.quantity != 0)
          TradeLog.AddClose(trade.DateTime, -this.quantity,
            trade.Price, trade.Price * this.quantity - this.pricesum);

        if(nq != 0)
          TradeLog.AddOpen(trade.DateTime, nq, trade.Price);

        this.pricesum = trade.Price * nq;

        // ------------------------------------------------

        if(stopOrderId != 0)
        {
          tmgr.KillStopOrder(stopOrderId);
          stopOrderId = 0;
        }

        if(takeProfit != null)
        {
          tmgr.CancelAction(takeProfit);
          takeProfit = null;
        }

        stopActivated = false;
        stopCanceled = false;
        takeActivated = false;
        takeCanceled = false;

        // ------------------------------------------------
      }
      else
      {
        if(Math.Sign(trade.Quantity) == Math.Sign(this.quantity))
          TradeLog.AddOpen(trade.DateTime, trade.Quantity, trade.Price);
        else
          TradeLog.AddClose(trade.DateTime, trade.Quantity, trade.Price);

        this.pricesum += trade.Price * trade.Quantity;
      }

      this.quantity = nq;

      // ------------------------------------------------------------

      if(quantity == 0)
        dataReceiver.PutPosition(0, 0);
      else
      {
        // ------------------------------------------------

        if(stopOrderId != 0)
        {
          int id = stopOrderId;
          stopOrderId = 0;
          tmgr.KillStopOrder(id);
        }

        if(takeProfit != null)
        {
          if(takeProfit.Status == TermManager.TStatus.Canceled)
          {
            takeCanceled = true;
            takeProfit = null;
          }
          else if(takeProfit.OId == trade.OId)
            takeActivated = true;
          else if(!takeActivated)
          {
            tmgr.CancelAction(takeProfit);
            takeProfit = null;
          }
        }

        // ------------------------------------------------

        int price = pricesum / quantity;

        // ------------------------------------------------

        if(cfg.u.AutoStopOffset != 0 && !(stopActivated || stopCanceled))
          if(quantity > 0)
          {
            if(!takeActivated)
              stopOrderPrice = QScalp.Price.Ceil(price - cfg.u.AutoStopOffset);

            stopOrderId = tmgr.CreateStopOrder(
              stopOrderPrice,
              stopOrderPrice - cfg.u.AutoStopSlippage,
              -quantity);
          }
          else
          {
            if(!takeActivated)
              stopOrderPrice = QScalp.Price.Floor(price + cfg.u.AutoStopOffset);

            stopOrderId = tmgr.CreateStopOrder(
              stopOrderPrice,
              stopOrderPrice + cfg.u.AutoStopSlippage,
              -quantity);
          }

        // ------------------------------------------------

        if(cfg.u.AutoTakeOffset != 0 && !(takeActivated || takeCanceled || stopActivated))
          if(quantity > 0)
          {
            takeProfit = tmgr.ExecAction(new OwnAction(
              TradeOp.Sell,
              BaseQuote.Absolute,
              QScalp.Price.Ceil(price + cfg.u.AutoTakeOffset),
              quantity));
          }
          else
          {
            takeProfit = tmgr.ExecAction(new OwnAction(
              TradeOp.Buy,
              BaseQuote.Absolute,
              QScalp.Price.Floor(price - cfg.u.AutoTakeOffset),
              -quantity));
          }

        // ------------------------------------------------

        dataReceiver.PutPosition(quantity, price);
      }

      // ------------------------------------------------------------
    }

    // **********************************************************************

    public void StopOrderRemoved(int id, bool activated)
    {
      if(id == stopOrderId)
      {
        if(activated)
        {
          stopActivated = true;

          if(takeProfit != null)
          {
            tmgr.CancelAction(takeProfit);
            takeProfit = null;
          }
        }
        else
          stopCanceled = true;
      }
    }

    // **********************************************************************

    public void PutLastPrice(int price)
    {
      if(stopOrderId != 0 && cfg.u.AutoStopTrail)
      {
        if(quantity > 0)
        {
          int newStop = QScalp.Price.Ceil(price - cfg.u.AutoStopOffset);

          if(newStop > stopOrderPrice)
          {
            tmgr.KillStopOrder(stopOrderId);
            stopOrderPrice = newStop;
            stopOrderId = tmgr.CreateStopOrder(
              stopOrderPrice,
              stopOrderPrice - cfg.u.AutoStopSlippage,
              -quantity);
          }
        }
        else
        {
          int newStop = QScalp.Price.Floor(price + cfg.u.AutoStopOffset);

          if(newStop < stopOrderPrice)
          {
            tmgr.KillStopOrder(stopOrderId);
            stopOrderPrice = newStop;
            stopOrderId = tmgr.CreateStopOrder(
              stopOrderPrice,
              stopOrderPrice + cfg.u.AutoStopSlippage,
              -quantity);
          }
        }
      }
    }

    // **********************************************************************

    public bool ByOrdersUpdated
    {
      get { return byOrdersUpdated && !(byOrdersUpdated = false); }
    }

    // **********************************************************************

    public int ByOrders
    {
      get { return byOrders; }
      set { byOrders = value; byOrdersUpdated = true; }
    }

    // **********************************************************************

    public void Clear()
    {
      quantity = 0;
      pricesum = 0;

      ByOrders = 0;

      TradeLog.Clear();

      dataReceiver.PutPosition(quantity, 0);
    }

    // **********************************************************************
  }
}
