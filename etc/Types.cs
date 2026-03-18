// ====================================================================
//    Types.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ====================================================================

using System;
using System.Xml.Serialization;

namespace QScalp
{
  // ************************************************************************

  public enum TradeOp { Cancel, Buy, Sell, Upsize, Downsize, Close, Reverse, Wait }
  public enum BaseQuote { None, Counter, Similar, Absolute }
  public enum QuoteType { Unknown, Free, Spread, Ask, Bid, BestAsk, BestBid }
  public enum ClusterView { Summary, Separate, Delta }
  public enum ClusterFill { Double, SingleDelta, SingleBalance }

  [Flags]
  public enum ClusterBase
  {
    None = 0x00, Time = 0x01, Volume = 0x02,
    Range = 0x04, Ticks = 0x08, Delta = 0x10
  }


  // ************************************************************************

  public struct OwnAction
  {
    public readonly TradeOp Operation;

    public BaseQuote Quote;
    public int Value;
    public int Quantity;

    public OwnAction(TradeOp operation)
    {
      this.Operation = operation;
      this.Quote = BaseQuote.None;
      this.Value = 0;
      this.Quantity = 0;
    }

    public OwnAction(TradeOp operation, BaseQuote quote, int value, int quantity)
    {
      this.Operation = operation;
      this.Quote = quote;
      this.Value = value;
      this.Quantity = quantity;
    }
  }

  // ************************************************************************

  public struct GuideSource
  {
    [XmlAttributeAttribute]
    public string SecCode;

    [XmlAttributeAttribute]
    public string ClassCode;

    [XmlAttributeAttribute]
    public double PriceStep;

    [XmlAttributeAttribute]
    public double Wnew;

    [XmlAttributeAttribute]
    public double Wsrc;

    public string StrSecCode { get { return SecCode; } }
    public string StrClassCode { get { return ClassCode; } }
    public string StrPriceStep { get { return PriceStep.ToString(); } }
    public string StrWnew { get { return Wnew.ToString("N2"); } }
    public string StrWsrc { get { return Wsrc.ToString("N2"); } }

    public GuideSource(string secCode, string classCode, double priceStep, double wnew, double wsrc)
    {
      this.SecCode = secCode;
      this.ClassCode = classCode;
      this.PriceStep = priceStep;
      this.Wnew = wnew;
      this.Wsrc = wsrc;
    }
  }

  // ************************************************************************

  public struct ToneSource
  {
    [XmlAttributeAttribute]
    public string SecCode;

    [XmlAttributeAttribute]
    public string ClassCode;

    [XmlAttributeAttribute]
    public int Interval;

    [XmlAttributeAttribute]
    public int FillVolume;

    public string StrSecCode { get { return SecCode; } }
    public string StrClassCode { get { return ClassCode; } }
    public string StrInterval { get { return Interval.ToString("N", cfg.BaseCulture); } }
    public string StrFillVolume { get { return FillVolume.ToString("N", cfg.BaseCulture); } }

    public ToneSource(string secCode, string classCode, int interval, int fillVolume)
    {
      this.SecCode = secCode;
      this.ClassCode = classCode;
      this.Interval = interval;
      this.FillVolume = fillVolume;
    }
  }

  // ************************************************************************
  // *                             Data types                               *
  // ************************************************************************

  struct Message
  {
    public readonly DateTime DateTime;
    public readonly string Text;

    public Message(string text)
    {
      this.DateTime = DateTime.Now;
      this.Text = text;
    }
  }

  // ************************************************************************

  struct Quote
  {
    public int Price;
    public int Volume;
    public QuoteType Type;

    public Quote(int price, int volume, QuoteType type)
    {
      this.Price = price;
      this.Volume = volume;
      this.Type = type;
    }
  }

  // ************************************************************************

  struct Spread
  {
    public readonly int Ask;
    public readonly int Bid;

    public Spread(int ask, int bid)
    {
      this.Ask = ask;
      this.Bid = bid;
    }
  }

  // ************************************************************************

  struct Trade
  {
    public int IntPrice;
    public double RawPrice;
    public int Quantity;
    public TradeOp Op;
    public DateTime DateTime;
    //public DateTime Received;
  }

  // ************************************************************************

  struct OwnOrder
  {
    public readonly long Id;
    public readonly int Price;

    public readonly int Active;
    public readonly int Filled;

    public OwnOrder(long id, int price, int active, int filled)
    {
      this.Id = id;
      this.Price = price;
      this.Active = active;
      this.Filled = filled;
    }
  }

  // ************************************************************************

  struct OwnTrade
  {
    public readonly DateTime DateTime;
    public readonly long OId;
    public readonly int Price;
    public readonly int Quantity;

    public OwnTrade(DateTime dateTime, long oid, int price, int quantity)
    {
      this.DateTime = dateTime;
      this.OId = oid;
      this.Price = price;
      this.Quantity = quantity;
    }
  }

  // ************************************************************************
  // *                     Data Receiver Interface                          *
  // ************************************************************************

  interface IDataReceiver
  {
    void PutMessage(Message msg);
    void PutStock(Quote[] quotes, Spread spread);
    void PutTrade(string skey, Trade trade);
    void PutOwnOrder(OwnOrder order);
    void PutPosition(int quantity, int price);
  }

  // ************************************************************************
}
