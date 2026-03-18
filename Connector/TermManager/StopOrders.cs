// =========================================================================
//    StopOrders.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =========================================================================

using System.Collections.Generic;

namespace QScalp.Connector
{
  class StopOrders
  {
    // **********************************************************************

    struct StopOrder
    {
      public readonly int Id;
      public readonly int StopPrice;
      public readonly int ExecPrice;
      public readonly int Quantity;

      public StopOrder(int id, int stopPrice, int execPrice, int quantity)
      {
        this.Id = id;
        this.StopPrice = stopPrice;
        this.ExecPrice = execPrice;
        this.Quantity = quantity;
      }
    }

    // **********************************************************************

    IDataReceiver dataReceiver;
    TermManager tmgr;

    static int lastId;

    List<StopOrder> orders;

    // **********************************************************************

    public StopOrders(TermManager tmgr, IDataReceiver dataReceiver)
    {
      this.tmgr = tmgr;
      this.dataReceiver = dataReceiver;

      orders = new List<StopOrder>();
    }

    // **********************************************************************

    public int CreateOrder(int stopPrice, int execPrice, int quantity)
    {
      StopOrder order = new StopOrder(--lastId, stopPrice, execPrice, quantity);

      lock(orders)
      {
        orders.Add(order);

        dataReceiver.PutOwnOrder(new OwnOrder(
          order.Id,
          order.StopPrice,
          order.Quantity,
          0));
      }

      return order.Id;
    }

    // **********************************************************************

    public void KillOrder(int id, int price)
    {
      lock(orders)
      {
        int i = 0;

        while(i < orders.Count)
        {
          StopOrder order = orders[i];

          if(order.Id == id || order.StopPrice == price)
          {
            orders.RemoveAt(i);
            tmgr.StopOrderRemoved(order.Id, false);
            dataReceiver.PutOwnOrder(new OwnOrder(order.Id, order.StopPrice, 0, 0));
          }
          else
            i++;
        }
      }
    }

    // **********************************************************************

    public void PutLastPrice(int price)
    {
      if(orders.Count > 0)
        lock(orders)
        {
          int i = 0;

          while(i < orders.Count)
          {
            StopOrder order = orders[i];

            if(order.Quantity > 0)
            {
              if(price >= order.StopPrice)
              {
                tmgr.ExecAction(new OwnAction(TradeOp.Buy, BaseQuote.Absolute, order.ExecPrice, order.Quantity));

                orders.RemoveAt(i);
                tmgr.StopOrderRemoved(order.Id, true);
                dataReceiver.PutOwnOrder(new OwnOrder(order.Id, order.StopPrice, 0, order.Quantity));
              }
              else
                i++;
            }
            else
            {
              if(price <= order.StopPrice)
              {
                tmgr.ExecAction(new OwnAction(TradeOp.Sell, BaseQuote.Absolute, order.ExecPrice, -order.Quantity));

                orders.RemoveAt(i);
                tmgr.StopOrderRemoved(order.Id, true);
                dataReceiver.PutOwnOrder(new OwnOrder(order.Id, order.StopPrice, 0, order.Quantity));
              }
              else
                i++;
            }
          }
        }
    }

    // **********************************************************************

    public void Clear()
    {
      if(orders.Count > 0)
        lock(orders)
        {
          for(int i = 0; i < orders.Count; i++)
          {
            StopOrder order = orders[i];
            tmgr.StopOrderRemoved(order.Id, false);
            dataReceiver.PutOwnOrder(new OwnOrder(order.Id, order.StopPrice, 0, 0));
          }

          orders.Clear();
        }
    }

    // **********************************************************************
  }
}
