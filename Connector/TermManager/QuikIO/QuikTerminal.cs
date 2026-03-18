// =========================================================================
//   QuikTerminal.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =========================================================================

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using QScalp.Connector;
using Trans2QuikAPI;

namespace QScalp.QuikIO
{
  class QuikTerminal : ITerminal
  {
    // **********************************************************************

    public static int ClientCodeMaxLength { get { return 18 - cfg.FullProgName.Length; } }

    // **********************************************************************

    const string NotConnectedStr = "Нет соединения.";

    TermManager mgr;

    static int transId;

    int error;
    StringBuilder msg;

    bool working;
    bool connected;

    Timer connecting;

    // **********************************************************************

    public string Name { get { return "QUIK"; } }
    public void PutLastPrice(int price) { }

    // **********************************************************************

    public QuikTerminal(TermManager mgr)
    {
      this.mgr = mgr;

      msg = new StringBuilder(256);
      connecting = new Timer(TryConnect);
    }

    // **********************************************************************
    // *                             Соединение                             *
    // **********************************************************************

    void TryConnect(Object state)
    {
      if(working)
      {
        try
        {
          if(
            Trans2Quik.SET_CONNECTION_STATUS_CALLBACK(StatusCallback, out error, msg, msg.Capacity) != Trans2Quik.Result.SUCCESS
            ||
            Trans2Quik.SET_TRANSACTIONS_REPLY_CALLBACK(TransactionReplyCallback, out error, msg, msg.Capacity) != Trans2Quik.Result.SUCCESS
            )
          {
            mgr.ConnectionUpdate(TermConnection.None, msg.ToString());
            return;
          }

          Trans2Quik.Result result = Trans2Quik.CONNECT(cfg.u.QuikFolder, out error, msg, msg.Capacity);
          if(result != Trans2Quik.Result.SUCCESS && result != Trans2Quik.Result.ALREADY_CONNECTED_TO_QUIK)
          {
            mgr.ConnectionUpdate(TermConnection.None, msg.ToString());
            return;
          }
        }
        catch(Exception e)
        {
          mgr.ConnectionUpdate(TermConnection.None, e.Message);
          return;
        }

        connected = true;

        if(Trans2Quik.UNSUBSCRIBE_ORDERS() != Trans2Quik.Result.SUCCESS
          || Trans2Quik.UNSUBSCRIBE_TRADES() != Trans2Quik.Result.SUCCESS
          || Trans2Quik.SUBSCRIBE_ORDERS(cfg.u.ClassCode, cfg.u.SecCode) != Trans2Quik.Result.SUCCESS
          || Trans2Quik.START_ORDERS(OrderStatusCallback) != Trans2Quik.Result.SUCCESS
          || Trans2Quik.SUBSCRIBE_TRADES(cfg.u.ClassCode, cfg.u.SecCode) != Trans2Quik.Result.SUCCESS
          || Trans2Quik.START_TRADES(TradeStatusCallback) != Trans2Quik.Result.SUCCESS)
        {
          mgr.ConnectionUpdate(TermConnection.Partial, "Соединение установлено не полностью");
          return;
        }

        mgr.ConnectionUpdate(TermConnection.Full, "Соединение с сервером QUIK установлено");
      }

      connecting.Change(Timeout.Infinite, Timeout.Infinite);
    }

    // **********************************************************************

    public void Connect()
    {
      working = true;
      connecting.Change(0, cfg.QuikTryConnectInterval);
    }

    // **********************************************************************

    public void Disconnect()
    {
      working = false;
      connecting.Change(Timeout.Infinite, Timeout.Infinite);

      if(connected)
        Trans2Quik.DISCONNECT(out error, msg, msg.Capacity);
    }

    // **********************************************************************

    void StatusCallback(Trans2Quik.Result evnt, int err, string m)
    {
      switch(evnt)
      {
        case Trans2Quik.Result.QUIK_DISCONNECTED:
          connecting.Change(0, cfg.QuikTryConnectInterval);
          break;

        case Trans2Quik.Result.DLL_DISCONNECTED:
          connected = false;
          connecting.Change(0, cfg.QuikTryConnectInterval);
          break;
      }
    }

    // **********************************************************************
    // *                               Сделки                               *
    // **********************************************************************

    void TradeStatusCallback(
      int nMode,
      double trade_id,
      double order_id,
      string classCode,
      string secCode,
      double price,
      int quantity,
      double msum,
      int isSell,
      int tradeDescriptor)
    {
      string comment = Marshal.PtrToStringAnsi(Trans2Quik.TRADE_BROKERREF(tradeDescriptor));

      if(nMode == 0 && ((comment != null && comment.EndsWith(cfg.FullProgName)) || cfg.u.AcceptAllTrades))
      {
        if(isSell != 0)
          quantity = -quantity;

        int date = Trans2Quik.TRADE_DATE(tradeDescriptor);
        int time = Trans2Quik.TRADE_TIME(tradeDescriptor);

        int year, month, day;
        int hour, min, sec;

        year = date / 10000;
        month = (day = date - year * 10000) / 100;
        day -= month * 100;

        hour = time / 10000;
        min = (sec = time - hour * 10000) / 100;
        sec -= min * 100;

        mgr.PutOwnTrade(new OwnTrade(
          new DateTime(year, month, day, hour, min, sec),
          (long)order_id, Price.GetInt(price), quantity));
      }
    }

    // **********************************************************************
    // *                               Заявки                               *
    // **********************************************************************

    void TransactionReplyCallback(
      Trans2Quik.Result r,
      int err,
      int rc,
      int tid,
      double order_id,
      string msg)
    {
      if(r == Trans2Quik.Result.SUCCESS && rc == 3)
        mgr.ActionReply(tid, (long)order_id, null);
      else
        mgr.ActionReply(tid, (long)order_id, msg.Length == 0 ? r + ", " + err : msg.ToString());
    }

    // **********************************************************************

    void OrderStatusCallback(
      int nMode,
      int tid,
      double order_id,
      string classCode,
      string secCode,
      double price,
      int balance,
      double msum,
      int isSell,
      int status,
      int orderDescriptor)
    {
      if(nMode == 0)
      {
        int filled;

        if(isSell == 0)
          filled = Trans2Quik.ORDER_QTY(orderDescriptor) - balance;
        else
        {
          filled = balance - Trans2Quik.ORDER_QTY(orderDescriptor);
          balance = -balance;
        }

        if(status == 1)
          mgr.OrderUpdate((long)order_id, balance, filled);
        else
          mgr.OrderUpdate((long)order_id, 0, filled);
      }
    }

    // **********************************************************************
    // *                        Управление заявками                         *
    // **********************************************************************

    string SendOrder(char op, int price, int quantity, out int tid)
    {
      if(connected)
      {
        tid = ++transId;

        Trans2Quik.Result r = Trans2Quik.SEND_ASYNC_TRANSACTION(
          "TRANS_ID=" + tid +
          "; ACCOUNT=" + cfg.u.QuikAccount +
          "; CLIENT_CODE=" + cfg.u.QuikClientCode + "//" + cfg.FullProgName +
          "; SECCODE=" + cfg.u.SecCode +
          "; CLASSCODE=" + cfg.u.ClassCode +
          "; ACTION=NEW_ORDER; OPERATION=" + op +
          "; PRICE=" + Price.GetRaw(price) +
          "; QUANTITY=" + quantity +
          ";",
          out error, msg, msg.Capacity);

        if(r == Trans2Quik.Result.SUCCESS)
          return null;
        else
        {
          tid = 0;
          return msg.Length == 0 ? r + ", " + error : msg.ToString();
        }
      }
      else
      {
        tid = 0;
        return NotConnectedStr;
      }
    }

    // **********************************************************************

    public string SendBuyOrder(int price, int quantity, out int tid)
    {
      return SendOrder('B', price, quantity, out tid);
    }

    // **********************************************************************

    public string SendSellOrder(int price, int quantity, out int tid)
    {
      return SendOrder('S', price, quantity, out tid);
    }

    // **********************************************************************

    public string KillOrder(long oid)
    {
      if(connected)
      {
        transId++;

        Trans2Quik.Result r = Trans2Quik.SEND_ASYNC_TRANSACTION(
          "TRANS_ID=" + transId +
          "; SECCODE=" + cfg.u.SecCode +
          "; CLASSCODE=" + cfg.u.ClassCode +
          "; ACTION=KILL_ORDER; ORDER_KEY=" + oid +
          ";",
          out error, msg, msg.Capacity);

        if(r == Trans2Quik.Result.SUCCESS)
          return null;
        else
          return msg.Length == 0 ? r + ", " + error : msg.ToString();
      }
      else
        return NotConnectedStr;
    }

    // **********************************************************************
  }
}
