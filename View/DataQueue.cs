// ========================================================================
//    DataQueue.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// ========================================================================

using System.Collections.Generic;

namespace QScalp.View
{
  // ************************************************************************
  // *                              DataQueue                               *
  // ************************************************************************

  interface IDataHandler<T> { void PutData(T data); }

  // ************************************************************************

  class DataQueue<T>
  {
    // --------------------------------------------------------------

    Queue<T> queue;
    List<IDataHandler<T>> handlers;

    // --------------------------------------------------------------

    public DataQueue()
    {
      queue = new Queue<T>();
      handlers = new List<IDataHandler<T>>();
    }

    // --------------------------------------------------------------

    public void RegisterHandler(IDataHandler<T> handler)
    {
      handlers.Add(handler);
    }

    // --------------------------------------------------------------

    public void UnregisterHandler(IDataHandler<T> handler)
    {
      handlers.Remove(handler);
    }

    // --------------------------------------------------------------

    public void Enqueue(T data)
    {
      lock(queue)
        queue.Enqueue(data);
    }

    // --------------------------------------------------------------

    public void UpdateHandlers()
    {
      lock(queue)
        while(queue.Count > 0)
        {
          T data = queue.Dequeue();

          for(int i = 0; i < handlers.Count; i++)
            handlers[i].PutData(data);
        }
    }

    // --------------------------------------------------------------

    public int Length { get { return queue.Count; } }

    // --------------------------------------------------------------

    public void Clear()
    {
      lock(queue)
        queue.Clear();
    }

    // --------------------------------------------------------------
  }


  // ************************************************************************
  // *                             TradesQueue                              *
  // ************************************************************************

  interface ITradesHandler { void PutTrade(Trade trade, int count); }

  // ************************************************************************

  class TradesQueue
  {
    // --------------------------------------------------------------

    struct TradeBinding
    {
      public LinkedList<Trade> Queue;
      public List<ITradesHandler> Handlers;
    }

    // --------------------------------------------------------------

    Dictionary<string, TradeBinding> tbList;

    // --------------------------------------------------------------

    public bool DataExist { get; protected set; }

    // --------------------------------------------------------------

    public TradesQueue() { tbList = new Dictionary<string, TradeBinding>(); }

    // --------------------------------------------------------------

    public void RegisterHandler(ITradesHandler handler, string skey)
    {
      lock(tbList)
      {
        TradeBinding tb;

        if(!tbList.TryGetValue(skey, out tb))
        {
          tb.Queue = new LinkedList<Trade>();
          tb.Handlers = new List<ITradesHandler>();

          tbList.Add(skey, tb);
        }

        tb.Handlers.Add(handler);
      }
    }

    // --------------------------------------------------------------

    public void UnregisterHandler(ITradesHandler handler)
    {
      lock(tbList)
      {
        List<string> keysToRemove = new List<string>();

        foreach(KeyValuePair<string, TradeBinding> kvp in tbList)
        {
          kvp.Value.Handlers.Remove(handler);

          if(kvp.Value.Handlers.Count == 0)
            keysToRemove.Add(kvp.Key);
        }

        foreach(string key in keysToRemove)
          tbList.Remove(key);
      }
    }

    // --------------------------------------------------------------

    public void Enqueue(string skey, Trade trade)
    {
      TradeBinding tb;

      lock(tbList)
        if(tbList.TryGetValue(skey, out tb))
        {
          tb.Queue.AddLast(trade);
          DataExist = true;
        }
    }

    // --------------------------------------------------------------

    public void UpdateHandlers()
    {
      DataExist = false;

      lock(tbList)
      {
        foreach(TradeBinding tb in tbList.Values)
          while(tb.Queue.Count > 0)
          {
            for(int i = 0; i < tb.Handlers.Count; i++)
              tb.Handlers[i].PutTrade(tb.Queue.First.Value, tb.Queue.Count);

            tb.Queue.RemoveFirst();
          }
      }
    }

    // --------------------------------------------------------------

    public void Clear()
    {
      lock(tbList)
      {
        foreach(TradeBinding tb in tbList.Values)
          tb.Queue.Clear();
        
        DataExist = false;
      }
    }

    // --------------------------------------------------------------
  }


  // ************************************************************************
  // *                              OrdersList                              *
  // ************************************************************************

  interface IOrdersHandler { void OrdersUpdated(int price); }

  // ************************************************************************

  class OrdersList
  {
    // --------------------------------------------------------------

    Dictionary<int, List<OwnOrder>> list;

    Queue<OwnOrder> queue;
    List<IOrdersHandler> handlers;

    // --------------------------------------------------------------

    public OrdersList()
    {
      list = new Dictionary<int, List<OwnOrder>>();

      queue = new Queue<OwnOrder>();
      handlers = new List<IOrdersHandler>();
    }

    // --------------------------------------------------------------

    public void RegisterHandler(IOrdersHandler handler)
    {
      handlers.Add(handler);
    }

    // --------------------------------------------------------------

    public void Enqueue(OwnOrder order)
    {
      lock(queue)
        queue.Enqueue(order);
    }

    // --------------------------------------------------------------

    public void UpdateHandlers()
    {
      lock(queue)
      {
        while(queue.Count > 0)
        {
          OwnOrder order = queue.Dequeue();
          List<OwnOrder> orders;

          if(list.TryGetValue(order.Price, out orders))
          {
            int i = 0;

            while(i < orders.Count && orders[i].Id != order.Id) i++;

            if(i < orders.Count)
            {
              if(order.Active == 0)
              {
                if(orders.Count == 1)
                  list.Remove(order.Price);
                else
                  orders.RemoveAt(i);
              }
              else
                orders[i] = order;
            }
            else if(order.Active != 0)
              orders.Add(order);
          }
          else if(order.Active != 0)
          {
            orders = new List<OwnOrder>();
            orders.Add(order);
            list.Add(order.Price, orders);
          }

          for(int i = 0; i < handlers.Count; i++)
            handlers[i].OrdersUpdated(order.Price);
        }
      }
    }

    // --------------------------------------------------------------

    public int QueueLength { get { return queue.Count; } }
    public bool Contains(int price) { return list.ContainsKey(price); }

    // --------------------------------------------------------------

    public IList<OwnOrder> this[int price]
    {
      get
      {
        List<OwnOrder> orders;

        if(list.TryGetValue(price, out orders))
          return orders.AsReadOnly();
        else
          return null;
      }
    }

    // --------------------------------------------------------------

    public void Clear()
    {
      lock(queue)
      {
        int[] keys = new int[list.Keys.Count];
        list.Keys.CopyTo(keys, 0);

        queue.Clear();
        list.Clear();

        foreach(int price in keys)
          for(int i = 0; i < handlers.Count; i++)
            handlers[i].OrdersUpdated(price);
      }
    }

    // --------------------------------------------------------------
  }

  // ************************************************************************
}
