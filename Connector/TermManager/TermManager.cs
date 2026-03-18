// ==========================================================================
//    TermManager.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Text;

using QScalp.ObjItems;
using QScalp.QuikIO;

namespace QScalp.Connector
{
  // ************************************************************************
  // *                         Types & interfaces                           *
  // ************************************************************************

  enum TermConnection { None, Partial, Full, Emulation }

  // ************************************************************************

  interface ITerminal
  {
    string Name { get; }
    void Connect();
    void Disconnect();
    string SendBuyOrder(int price, int quantity, out int tid);
    string SendSellOrder(int price, int quantity, out int tid);
    string KillOrder(long oid);
    void PutLastPrice(int price);
  }

  // ************************************************************************

  class LoopbackTerminal : ITerminal
  {
    public string Name
    {
      get { throw new NotImplementedException(); }
    }

    public void Connect()
    {
      throw new NotImplementedException();
    }

    public void Disconnect()
    {
      throw new NotImplementedException();
    }

    public string SendBuyOrder(int price, int quantity, out int tid)
    {
      throw new NotImplementedException();
    }

    public string SendSellOrder(int price, int quantity, out int tid)
    {
      throw new NotImplementedException();
    }

    public string KillOrder(long oid)
    {
      throw new NotImplementedException();
    }

    public void PutLastPrice(int price)
    {
      throw new NotImplementedException();
    }
  }

  // ************************************************************************
  // *                             TermManager                              *
  // ************************************************************************

  class TermManager
  {
    // **********************************************************************

    public enum TStatus { Execute, Cancel, Canceled }

    // ----------------------------------------------------------------------

    public class Transaction
    {
      public TStatus Status;
      public int TId;
      public long OId;
      public int Price;

      public OwnAction Action;

      public Transaction(OwnAction action)
      {
        this.Status = TStatus.Execute;
        this.Action = action;
      }
    }

    // **********************************************************************

    IDataReceiver dataReceiver;
    ITerminal terminal;

    TermLog log;

    StopOrders stopOrders;

    bool connectionUpdated;
    string connectionText;

    LinkedList<Transaction> tlist;
    bool tlistUpdated;
    StringBuilder tlistText;

    // **********************************************************************

    public int AskPrice { get; protected set; }
    public int BidPrice { get; protected set; }

    public Position Position { get; protected set; }

    // **********************************************************************

    public bool ConnectionUpdated
    {
      get { return connectionUpdated && !(connectionUpdated = false); }
    }

    public string ConnectionText
    {
      get { return connectionText; }
      private set { connectionText = value; connectionUpdated = true; }
    }

    public TermConnection Connected { get; private set; }

    // ----------------------------------------------------------------------

    public bool QueueUpdated
    {
      get { return tlistUpdated && !(tlistUpdated = false); }
    }

    public int QueueLength { get { return tlist.Count; } }

    // **********************************************************************

    public TermManager(IDataReceiver dataReceiver)
    {
      this.dataReceiver = dataReceiver;

      terminal = new LoopbackTerminal();
      log = new TermLog(this, dataReceiver);
      stopOrders = new StopOrders(this, dataReceiver);

      Position = new Position(this, dataReceiver);

      ConnectionText = string.Empty;


      tlist = new LinkedList<Transaction>();
      tlistText = new StringBuilder(128);

      tlistUpdated = true;
    }

    // **********************************************************************
    // *                             Соединение                             *
    // **********************************************************************

    public void Connect()
    {
      if(cfg.u.TermEmulation)
        terminal = new Emulator(this);
      else
        terminal = new QuikTerminal(this);

      log.Open();
      log.Put("Connect;" + cfg.FullProgName + ";" + terminal.Name);

      terminal.Connect();
    }

    // **********************************************************************

    public void Disconnect()
    {
      terminal.Disconnect();

      log.Put("Disconnect");
      log.Close();
    }

    // **********************************************************************

    public void ConnectionUpdate(TermConnection cs, string text)
    {
      Connected = cs;
      ConnectionText = text;

      log.Put("ConnectionUpdate;" + cs + ";" + text);
    }

    // **********************************************************************
    // *                               Сделки                               *
    // **********************************************************************

    public void PutOwnTrade(OwnTrade trade)
    {
      log.Put("PutOwnTrade;;o" + trade.OId + ";p" + trade.Price + ";q" + trade.Quantity);
      Position.PutOwnTrade(trade);
    }

    // **********************************************************************
    // *                             Стоп-заявки                            *
    // **********************************************************************

    public void StopOrderRemoved(int id, bool activated)
    {
      log.Put("StopOrderRemoved;;" + id + ";" + activated);
      Position.StopOrderRemoved(id, activated);
    }

    // **********************************************************************
    // *                               Заявки                               *
    // **********************************************************************

    void ShowError(int tid, string op, int q, int p, string error)
    {
      dataReceiver.PutMessage(new Message("Транзакция T" + tid + ", "
        + op + " " + q.ToString(cfg.BaseCulture) + " * " + Price.GetString(p)
        + "\nотвергнута торговой системой:\n" + error.Trim()));
    }

    // **********************************************************************

    public void ActionReply(int tid, long oid, string error)
    {
      log.Put("ActionReply;t" + tid + ";o" + oid + ";" + error);

      lock(tlist)
        for(LinkedListNode<Transaction> node = tlist.First; node != null; node = node.Next)
          if(node.Value.TId == tid)
          {
            if(error == null)
              node.Value.OId = oid;
            else
            {
              ShowError(tid, node.Value.Action.Operation.ToString(),
                node.Value.Action.Quantity, node.Value.Price, error);
              tlist.Remove(node);
            }

            ProcessTList();
            return;
          }
    }

    // **********************************************************************

    public void OrderUpdate(long oid, int active, int filled)
    {
      log.Put("OrderUpdate;;o" + oid + ";a" + active + ";f" + filled);

      lock(tlist)
        for(LinkedListNode<Transaction> node = tlist.First; node != null; node = node.Next)
          if(node.Value.OId == oid)
          {
            Transaction t = node.Value;

            if(active == 0)
            {
              Position.ByOrders += filled;

              tlist.Remove(node);
              ProcessTList();
            }

            dataReceiver.PutOwnOrder(new OwnOrder(oid, t.Price, active, filled));

            return;
          }
    }

    // **********************************************************************
    // *                        Управление заявками                         *
    // **********************************************************************

    void KillOrder(long oid)
    {
      string error = terminal.KillOrder(oid);

      log.Put("KillOrder;;o" + oid + ";" + error);

      if(error != null)
        ShowError(0, "Kill", 0, 0, error);
    }

    // **********************************************************************
    // *                     Обработка очереди операций                     *
    // **********************************************************************

    void CreateBuyOrder(Transaction t, int quantity)
    {
      switch(t.Action.Quote)
      {
        case BaseQuote.Absolute:
          t.Price = t.Action.Value;
          break;

        case BaseQuote.Counter:
          t.Price = Price.Floor(AskPrice + t.Action.Value);
          break;

        case BaseQuote.Similar:
          t.Price = Price.Floor(BidPrice + t.Action.Value);
          break;

        default:
          return;
      }

      string error = terminal.SendBuyOrder(t.Price, quantity, out t.TId);

      log.Put("CreateBuyOrder;" + t.Action.Operation + ";q" + t.Action.Quantity
        + ";v" + t.Action.Value + ";" + t.Action.Quote + ";tid"
        + t.TId + ";" + error);

      if(error != null)
        ShowError(t.TId, "Buy", quantity, t.Price, error);
    }

    // ----------------------------------------------------------------------

    void CreateSellOrder(Transaction t, int quantity)
    {
      switch(t.Action.Quote)
      {
        case BaseQuote.Absolute:
          t.Price = t.Action.Value;
          break;

        case BaseQuote.Counter:
          t.Price = Price.Ceil(BidPrice - t.Action.Value);
          break;

        case BaseQuote.Similar:
          t.Price = Price.Ceil(AskPrice - t.Action.Value);
          break;

        default:
          return;
      }

      string error = terminal.SendSellOrder(t.Price, quantity, out t.TId);

      log.Put("CreateSellOrder;" + t.Action.Operation + ";q" + t.Action.Quantity
        + ";v" + t.Action.Value + ";" + t.Action.Quote + ";tid"
        + t.TId + ";" + error);

      if(error != null)
        ShowError(t.TId, "Sell", quantity, t.Price, error);
    }

    // **********************************************************************

    void ProcessTList()
    {
      LinkedListNode<Transaction> next = tlist.First;
      LinkedListNode<Transaction> curr;

      while(next != null)
      {
        curr = next;
        next = next.Next;

        switch(curr.Value.Status)
        {
          // --------------------------------------------------------

          case TStatus.Cancel:
            if(curr.Value.TId == 0)
            {
              tlist.Remove(curr);
              curr.Value.Status = TStatus.Canceled;
            }
            else if(curr.Value.OId > 0)
            {
              KillOrder(curr.Value.OId);
              curr.Value.Status = TStatus.Canceled;
            }
            break;

          // --------------------------------------------------------

          case TStatus.Canceled:
            break;

          // --------------------------------------------------------

          case TStatus.Execute:
            switch(curr.Value.Action.Operation)
            {
              // -----------------------------

              case TradeOp.Buy:
                if(curr.Value.TId == 0)
                {
                  CreateBuyOrder(curr.Value, curr.Value.Action.Quantity);

                  if(curr.Value.TId == 0)
                    tlist.Remove(curr);
                }

                break;

              // -----------------------------

              case TradeOp.Sell:
                if(curr.Value.TId == 0)
                {
                  CreateSellOrder(curr.Value, curr.Value.Action.Quantity);

                  if(curr.Value.TId == 0)
                    tlist.Remove(curr);
                }

                break;

              // -----------------------------

              case TradeOp.Upsize:
                if(curr.Value.TId == 0)
                {
                  if(Position.ByOrders > 0)
                    CreateBuyOrder(curr.Value, curr.Value.Action.Quantity);
                  else if(Position.ByOrders < 0)
                    CreateSellOrder(curr.Value, curr.Value.Action.Quantity);

                  if(curr.Value.TId == 0 || Position.ByOrders == 0)
                    tlist.Remove(curr);
                }

                break;

              // -----------------------------

              case TradeOp.Downsize:
                if(curr.Value.TId == 0)
                {
                  if(Position.ByOrders > 0)
                    CreateSellOrder(curr.Value, curr.Value.Action.Quantity);
                  else if(Position.ByOrders < 0)
                    CreateBuyOrder(curr.Value, curr.Value.Action.Quantity);

                  if(curr.Value.TId == 0 || Position.ByOrders == 0)
                    tlist.Remove(curr);
                }

                break;

              // -----------------------------

              case TradeOp.Close:
                if(curr.Value.TId == 0)
                {
                  if(Position.ByOrders > 0)
                    CreateSellOrder(curr.Value, Position.ByOrders);
                  else if(Position.ByOrders < 0)
                    CreateBuyOrder(curr.Value, -Position.ByOrders);

                  if(curr.Value.TId == 0 || Position.ByOrders == 0)
                    tlist.Remove(curr);
                }

                break;

              // -----------------------------

              case TradeOp.Reverse:
                if(curr.Value.TId == 0)
                {
                  if(Position.ByOrders > 0)
                    CreateSellOrder(curr.Value, Position.ByOrders * 2);
                  else if(Position.ByOrders < 0)
                    CreateBuyOrder(curr.Value, -Position.ByOrders * 2);

                  if(curr.Value.TId == 0 || Position.ByOrders == 0)
                    tlist.Remove(curr);
                }

                break;


              // -----------------------------

              case TradeOp.Wait:
                if(curr == tlist.First)
                  tlist.Remove(curr);
                else
                  next = null;

                break;

              // -----------------------------
            }
            break;

          // --------------------------------------------------------
        }
      }

      tlistUpdated = true;
    }

    // **********************************************************************

    public string QueueText
    {
      get
      {
        tlistText.Length = 0;

        if(tlist.Count > 0)
          lock(tlist)
          {
            tlistText.Append("Очередь операций:");

            foreach(Transaction t in tlist)
            {
              tlistText.AppendLine();
              tlistText.Append(TradeOpItem.ToString(t.Action.Operation));

              if(t.Action.Quantity > 0)
              {
                tlistText.Append(" ");
                tlistText.Append(t.Action.Quantity);

                if(t.Price > 0)
                {
                  tlistText.Append(" * ");
                  tlistText.Append(Price.GetString(t.Price));
                }
              }
              else if(t.Price > 0)
              {
                tlistText.Append(" @ ");
                tlistText.Append(Price.GetString(t.Price));
              }

              switch(t.Status)
              {
                case TStatus.Cancel:
                  tlistText.Append(" - отмена");
                  break;
                case TStatus.Canceled:
                  tlistText.Append(" (отменено)");
                  break;
              }
            }
          }
        else
          tlistText.Append("Очередь операций пуста");

        return tlistText.ToString();
      }
    }

    // **********************************************************************
    // *                        Пользовательские ф-ции                      *
    // **********************************************************************

    public Transaction ExecAction(OwnAction action)
    {
      Transaction nt = null;

      if(AskPrice > 0 && Connected != TermConnection.None)
      {
        log.Put("ExecAction;" + action.Operation + ";q" + action.Quantity
          + ";v" + action.Value + ";" + action.Quote);

        lock(tlist)
        {
          if(action.Operation == TradeOp.Cancel)
          {
            if(action.Quote == BaseQuote.Absolute)
            {
              foreach(Transaction t in tlist)
                if(t.Price == action.Value && t.Status != TStatus.Canceled)
                  t.Status = TStatus.Cancel;

              stopOrders.KillOrder(0, action.Value);
            }
            else
            {
              foreach(Transaction t in tlist)
                if(t.Status != TStatus.Canceled)
                  t.Status = TStatus.Cancel;

              stopOrders.Clear();
            }
          }
          else
          {
            if(action.Quantity < 0)
              action.Quantity = cfg.u.WorkSize * action.Quantity / -100;

            tlist.AddLast(nt = new Transaction(action));
          }

          ProcessTList();
        }
      }

      return nt;
    }

    // **********************************************************************

    public void CancelAction(Transaction t)
    {
      log.Put("CancelAction;" + t.Action.Operation + ";q" + t.Action.Quantity
        + ";v" + t.Action.Value + ";" + t.Action.Quote + ";tid"
        + t.TId + ";OId" + t.OId + ";" + t.Status);

      lock(tlist)
        if(t.Status != TStatus.Canceled)
        {
          t.Status = TStatus.Cancel;
          ProcessTList();
        }
    }

    // **********************************************************************

    public int CreateStopOrder(int stopPrice, int execPrice, int quantity)
    {
      int id = stopOrders.CreateOrder(stopPrice, execPrice, quantity);

      log.Put("CreateStopOrder;;" + id + ";q" + quantity
        + ";p" + stopPrice + ";ep" + execPrice);

      return id;
    }

    // **********************************************************************

    public void KillStopOrder(int id)
    {
      log.Put("KillStopOrder;;" + id);
      stopOrders.KillOrder(id, 0);
    }

    // **********************************************************************

    public void DropState()
    {
      AskPrice = 0;
      BidPrice = 0;

      lock(tlist)
      {
        Position.Clear();
        tlist.Clear();
        ProcessTList();
        stopOrders.Clear();
      }

      log.Put("DropState");
    }

    // **********************************************************************

    public void PutSpread(Spread s) { AskPrice = s.Ask; BidPrice = s.Bid; }

    // **********************************************************************

    public void PutLastPrice(int price)
    {
      stopOrders.PutLastPrice(price);
      terminal.PutLastPrice(price);
      Position.PutLastPrice(price);
    }

    // **********************************************************************
  }
}
