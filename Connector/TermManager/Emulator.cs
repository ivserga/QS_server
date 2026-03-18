// =======================================================================
//    Emulator.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =======================================================================

using System;
using System.Collections.Generic;
using System.Threading;

namespace QScalp.Connector
{
  class Emulator : ITerminal
  {
    // **********************************************************************

    class Order
    {
      public int Id;
      public int Price;
      public int Quantity;
      public DateTime ExecAfter;
      public DateTime KillAfter;
      public int Executed;
    }

    // ----------------------------------------------------------------------

    enum ReplyTypes { Unknown, Action, Order, Trade }

    // ----------------------------------------------------------------------

    struct ReplyData
    {
      public readonly ReplyTypes Type;

      public readonly int Id;
      public readonly string Error;

      public readonly int Active;
      public readonly int Filled;

      public readonly int Price;

      public ReplyData(int id, string error)
      {
        this.Type = ReplyTypes.Action;

        this.Id = id;
        this.Error = error;
        this.Active = 0;
        this.Filled = 0;
        this.Price = 0;
      }

      public ReplyData(ReplyTypes type, int id, int active, int filled, int price)
      {
        this.Type = type;

        this.Id = id;
        this.Error = null;

        this.Active = active;
        this.Filled = filled;

        this.Price = price;
      }
    }

    // **********************************************************************

    const string NotRunningStr = "Эмулятор не запущен";

    TermManager mgr;

    int lastId;

    bool isConnected;
    Thread pThread;

    Random rnd;

    List<Order> olist;
    Queue<ReplyData> replies;

    // **********************************************************************

    public string Name { get { return "Эмулятор"; } }

    // **********************************************************************

    public Emulator(TermManager mgr)
    {
      this.mgr = mgr;

      rnd = new Random();

      olist = new List<Order>();
      replies = new Queue<ReplyData>();
    }

    // **********************************************************************

    public void Connect()
    {
      if(!isConnected)
      {
        isConnected = true;

        pThread = new Thread(Process);
        pThread.Name = Name;
        pThread.Start();
      }

      mgr.ConnectionUpdate(TermConnection.Emulation, "Режим эмуляции исполнения заявок");
    }

    // **********************************************************************

    public void Disconnect()
    {
      if(isConnected)
        isConnected = false;

      mgr.ConnectionUpdate(TermConnection.None, "Эмулятор остановлен");
    }

    // **********************************************************************

    void Process()
    {
      // ------------------------------------------------------------

      while(isConnected)
      {
        // ------------------------------------------------

        if(olist.Count > 0 && mgr.AskPrice > 0)
          lock(olist)
          {
            int i = 0;

            while(i < olist.Count)
            {
              Order o = olist[i];

              if(o.Executed > 0)
              {
                lock(replies)
                {
                  replies.Enqueue(new ReplyData(ReplyTypes.Order, o.Id, 0, o.Quantity, 0));
                  replies.Enqueue(new ReplyData(ReplyTypes.Trade, o.Id, 0, o.Quantity, o.Executed));
                }

                olist.RemoveAt(i);
                continue;
              }

              DateTime now = DateTime.UtcNow;

              if(o.ExecAfter < now
                && ((o.Quantity > 0 && o.Price >= mgr.AskPrice)
                || (o.Quantity < 0 && o.Price <= mgr.BidPrice)))
              {
                lock(replies)
                {
                  replies.Enqueue(new ReplyData(ReplyTypes.Order, o.Id, 0, o.Quantity, 0));
                  replies.Enqueue(new ReplyData(ReplyTypes.Trade, o.Id, 0, o.Quantity,
                    o.Quantity > 0 ? mgr.AskPrice : mgr.BidPrice));
                }

                olist.RemoveAt(i);
                continue;
              }

              if(o.KillAfter < now)
              {
                lock(replies)
                  replies.Enqueue(new ReplyData(ReplyTypes.Order, o.Id, 0, 0, 0));

                olist.RemoveAt(i);
                continue;
              }

              i++;
            }
          }

        // ------------------------------------------------

        while(replies.Count > 0)
        {
          ReplyData rd;

          lock(replies)
            rd = replies.Dequeue();

          switch(rd.Type)
          {
            case ReplyTypes.Action:
              mgr.ActionReply(rd.Id, rd.Id, rd.Error);
              break;

            case ReplyTypes.Order:
              mgr.OrderUpdate(rd.Id, rd.Active, rd.Filled);
              break;

            case ReplyTypes.Trade:
              mgr.PutOwnTrade(new OwnTrade(DateTime.Now, rd.Id, rd.Price, rd.Filled));
              break;
          }
        }

        // ------------------------------------------------

        Thread.Sleep(cfg.s.EmulatorTickInterval);
      }

      // ------------------------------------------------------------ ?

      lock(olist)
        olist.Clear();

      lock(replies)
        replies.Clear();

      // ------------------------------------------------------------
    }

    // **********************************************************************

    public void PutLastPrice(int price)
    {
      if(olist.Count > 0)
        lock(olist)
          for(int i = 0; i < olist.Count; i++)
          {
            Order o = olist[i];

            if(o.ExecAfter < DateTime.UtcNow
              && o.Executed == 0
              && ((o.Quantity > 0 && o.Price >= price)
              || (o.Quantity < 0 && o.Price <= price)))
            {
              o.Executed = price;
            }
          }
    }

    // **********************************************************************

    string SendOrder(int price, int quantity, out int tid)
    {
      if(isConnected)
      {
        tid = ++this.lastId;

        lock(olist)
        {
          int pLong = mgr.Position.ByOrders;
          int pShort = mgr.Position.ByOrders;

          if(quantity > 0)
            pLong += quantity;
          else
            pShort += quantity;

          for(int i = 0; i < olist.Count; i++)
            if(olist[i].Quantity > 0)
              pLong += olist[i].Quantity;
            else
              pShort += olist[i].Quantity;

          if(pLong > cfg.u.EmulatorLimit || -pShort > cfg.u.EmulatorLimit)
            lock(replies)
              replies.Enqueue(new ReplyData(tid, "Максимальный размер позиции = "
                + cfg.u.EmulatorLimit.ToString("N", cfg.BaseCulture)));
          else
          {
            Order order = new Order();
            order.Id = tid;
            order.Price = price;
            order.Quantity = quantity;
            order.ExecAfter = DateTime.UtcNow.Add(new TimeSpan(0, 0, 0, 0,
              rnd.Next(cfg.u.EmulatorDelayMin, cfg.u.EmulatorDelayMax)));
            order.KillAfter = DateTime.MaxValue;

            olist.Add(order);

            lock(replies)
            {
              replies.Enqueue(new ReplyData(tid, null));
              replies.Enqueue(new ReplyData(ReplyTypes.Order, order.Id, order.Quantity, 0, 0));
            }
          }
        }

        return null;
      }
      else
      {
        tid = 0;
        return NotRunningStr;
      }
    }

    // **********************************************************************

    public string SendBuyOrder(int price, int quantity, out int tid)
    {
      return SendOrder(price, quantity, out tid);
    }

    // **********************************************************************

    public string SendSellOrder(int price, int quantity, out int tid)
    {
      return SendOrder(price, -quantity, out tid);
    }

    // **********************************************************************

    public string KillOrder(long oid)
    {
      if(isConnected)
      {
        lock(olist)
          for(int i = 0; i < olist.Count; i++)
            if(olist[i].Id == oid)
            {
              olist[i].KillAfter = DateTime.UtcNow.Add(
                new TimeSpan(0, 0, 0, 0, cfg.u.EmulatorDelayMin));

              return null;
            }

        return "Снимаемая заявка №" + oid + " не найдена.";
      }
      else
        return NotRunningStr;
    }

    // **********************************************************************
  }
}
