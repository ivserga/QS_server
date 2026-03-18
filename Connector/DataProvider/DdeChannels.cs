// ==========================================================================
//    DdeChannels.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// ==========================================================================

using System;
using XlDde;

namespace QScalp.Connector
{
  // ************************************************************************
  // *                                  Stock                               *
  // ************************************************************************

  sealed class StockChannel : XlDdeChannel
  {
    // **********************************************************************

    const string cnAskVolume = "SELL_VOLUME";
    const string cnBidVolume = "BUY_VOLUME";
    const string cnPrice = "PRICE";

    // **********************************************************************

    IDataReceiver receiver;
    TermManager tmgr;

    public StockChannel(IDataReceiver receiver, TermManager tmgr)
    {
      this.receiver = receiver;
      this.tmgr = tmgr;
    }

    // **********************************************************************

    protected override void ProcessTable(XlTable xt)
    {
      if(xt.Rows < 3)
      {
        IsError = true;
        return;
      }

      // ------------------------------------------------------------

      int cAskVolume = -1, cBidVolume = -1, cPrice = -1;

      for(int col = 0; col < xt.Columns; col++)
      {
        xt.ReadValue();

        if(xt.ValueType == XlTable.BlockType.String)
          switch(xt.StringValue)
          {
            case cnAskVolume:
              cAskVolume = col;
              break;
            case cnBidVolume:
              cBidVolume = col;
              break;
            case cnPrice:
              cPrice = col;
              break;
          }
      }

      if(cAskVolume < 0 || cBidVolume < 0 || cPrice < 0)
      {
        IsError = true;
        return;
      }

      // ------------------------------------------------------------

      Quote[] quotes = new Quote[xt.Rows - 1];
      int ask = -1, bid = -1;

      // ------------------------------------------------------------

      for(int row = 0; row < quotes.Length; row++)
      {
        int p = 0, av = 0, bv = 0, sc = 0;

        for(int col = 0; col < xt.Columns; col++)
        {
          xt.ReadValue();

          switch(xt.ValueType)
          {
            case XlTable.BlockType.Float:
              if(col == cAskVolume)
                av = (int)xt.FloatValue;
              else if(col == cBidVolume)
                bv = (int)xt.FloatValue;
              else if(col == cPrice)
                p = Price.GetInt(xt.FloatValue);
              break;

            case XlTable.BlockType.String:
              sc++;
              break;
          }
        }

        if(p <= 0)
        {
          if(sc == xt.Columns)
            break;
          else
          {
            IsError = true;
            return;
          }
        }

        if(av > 0)
        {
          ask = row;
          quotes[row] = new Quote(p, av, QuoteType.Ask);
        }
        else if(bv > 0)
        {
          if(bid == -1)
            bid = row;

          quotes[row] = new Quote(p, bv, QuoteType.Bid);
        }
        else
        {
          IsError = true;
          return;
        }
      }

      // ------------------------------------------------------------

      if(ask == -1 || bid == -1 || quotes[0].Price <= quotes[1].Price)
      {
        IsError = true;
        return;
      }

      // ------------------------------------------------------------

      quotes[ask].Type = QuoteType.BestAsk;
      quotes[bid].Type = QuoteType.BestBid;

      Spread s = new Spread(quotes[ask].Price, quotes[bid].Price);

      tmgr.PutSpread(s);
      receiver.PutStock(quotes, s);
    }

    // **********************************************************************
  }


  // ************************************************************************
  // *                                 Trades                               *
  // ************************************************************************

  sealed class TradesChannel : XlDdeChannel
  {
    // **********************************************************************

    const string cnDate = "TRADEDATE";
    const string cnTime = "TRADETIME";
    const string cnSecCode = "SECCODE";
    const string cnClassCode = "CLASSCODE";
    const string cnPrice = "PRICE";
    const string cnQuantity = "QTY";
    const string cnOperation = "BUYSELL";

    const string strBuyOp = "BUY";
    const string strSellOp = "SELL";

    // **********************************************************************

    IDataReceiver receiver;
    TermManager tmgr;

    int cDate;
    int cTime;
    int cPrice;
    int cQuantity;
    int cOp;
    int cSecCode;
    int cClassCode;

    bool columnsUnknown;

    // **********************************************************************

    public TradesChannel(IDataReceiver receiver, TermManager tmgr)
    {
      this.receiver = receiver;
      this.tmgr = tmgr;
      columnsUnknown = true;
    }

    // **********************************************************************

    public override bool IsConnected
    {
      get
      {
        return base.IsConnected;
      }
      set
      {
        columnsUnknown = true;
        base.IsConnected = value;
      }
    }

    // **********************************************************************

    protected override void ProcessTable(XlTable xt)
    {
      int row = 0;

      // ------------------------------------------------------------

      if(columnsUnknown)
      {
        cDate = -1;
        cTime = -1;
        cPrice = -1;
        cQuantity = -1;
        cOp = -1;
        cSecCode = -1;
        cClassCode = -1;

        for(int col = 0; col < xt.Columns; col++)
        {
          xt.ReadValue();

          if(xt.ValueType == XlTable.BlockType.String)
            switch(xt.StringValue)
            {
              case cnDate:
                cDate = col;
                break;
              case cnTime:
                cTime = col;
                break;
              case cnPrice:
                cPrice = col;
                break;
              case cnQuantity:
                cQuantity = col;
                break;
              case cnOperation:
                cOp = col;
                break;
              case cnSecCode:
                cSecCode = col;
                break;
              case cnClassCode:
                cClassCode = col;
                break;
            }
        }

        if(cDate < 0
          || cTime < 0
          || cPrice < 0
          || cQuantity < 0
          || cOp < 0
          || cSecCode < 0
          || cClassCode < 0)
        {
          IsError = true;
          return;
        }

        row++;
        columnsUnknown = false;
      }

      // ------------------------------------------------------------

      while(row++ < xt.Rows)
      {
        bool rowCorrect = true;

        string secCode = string.Empty;
        string classCode = string.Empty;

        string date = string.Empty;
        string time = string.Empty;

        Trade t = new Trade();

        // ----------------------------------------------------------

        for(int col = 0; col < xt.Columns; col++)
        {
          xt.ReadValue();

          if(col == cDate)
          {
            if(xt.ValueType == XlTable.BlockType.String)
              date = xt.StringValue;
            else
              rowCorrect = false;
          }
          else if(col == cTime)
          {
            if(xt.ValueType == XlTable.BlockType.String)
              time = xt.StringValue;
            else
              rowCorrect = false;
          }
          else if(col == cPrice)
          {
            if(xt.ValueType == XlTable.BlockType.Float)
              t.RawPrice = xt.FloatValue;
            else
              rowCorrect = false;
          }
          else if(col == cQuantity)
          {
            if(xt.ValueType == XlTable.BlockType.Float)
              t.Quantity = (int)xt.FloatValue;
            else
              rowCorrect = false;
          }
          else if(col == cOp)
          {
            if(xt.ValueType == XlTable.BlockType.String)
              switch(xt.StringValue)
              {
                case strBuyOp:
                  t.Op = TradeOp.Buy;
                  break;
                case strSellOp:
                  t.Op = TradeOp.Sell;
                  break;
              }
            else
              rowCorrect = false;
          }
          else if(col == cSecCode)
          {
            if(xt.ValueType == XlTable.BlockType.String)
              secCode = xt.StringValue;
            else
              rowCorrect = false;
          }
          else if(col == cClassCode)
          {
            if(xt.ValueType == XlTable.BlockType.String)
              classCode = xt.StringValue;
            else
              rowCorrect = false;
          }
        }

        // ----------------------------------------------------------

        if(secCode == cfg.u.SecCode && classCode == cfg.u.ClassCode)
          if(DateTime.TryParse(date + " " + time, out t.DateTime))
          {
            t.IntPrice = Price.GetInt(t.RawPrice);
            tmgr.PutLastPrice(t.IntPrice);
          }
          else
            rowCorrect = false;

        // ----------------------------------------------------------

        if(rowCorrect)
          receiver.PutTrade(secCode + classCode, t);
        else
          IsError = true;

        // ----------------------------------------------------------
      }
    }

    // **********************************************************************
  }
}
