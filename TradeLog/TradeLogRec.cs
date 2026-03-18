// ==========================================================================
//    TradeLogRec.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ==========================================================================

using System;
using System.ComponentModel;

namespace QScalp.TradeLogSpace
{
  public class TradeLogRec : INotifyPropertyChanged
  {
    // **********************************************************************

    [Flags]
    enum Flags
    {
      OpenExist = 0x01, OpenChanged = 0x02,
      CloseExist = 0x04, CloseChanged = 0x08,
      ResultExist = 0x10, ResultChanged = 0x20,
      None = 0x00
    }

    // **********************************************************************

    int priceRatio;
    Flags flags;

    DateTime openTime;
    int openQty;
    int openSum;

    DateTime closeTime;
    int closeQty;
    int closeSum;

    int result;

    // **********************************************************************

    public TradeLogRec(int priceRatio) { this.priceRatio = priceRatio; }

    // **********************************************************************

    public void SetOpen(DateTime dateTime, int qty, int sum)
    {
      this.openTime = dateTime;
      this.openQty = qty;
      this.openSum = sum;

      flags |= Flags.OpenExist | Flags.OpenChanged;
    }

    // **********************************************************************

    public void AddOpen(int qty, int sum)
    {
      this.openQty += qty;
      this.openSum += sum;

      flags |= Flags.OpenChanged;
    }

    // **********************************************************************

    public void SetClose(DateTime dateTime, int qty, int sum)
    {
      this.closeTime = dateTime;
      this.closeQty = qty;
      this.closeSum = sum;

      flags |= Flags.CloseExist | Flags.CloseChanged;
    }

    // **********************************************************************

    public void AddClose(int qty, int sum)
    {
      this.closeQty += qty;
      this.closeSum += sum;

      flags |= Flags.CloseChanged;
    }

    // **********************************************************************

    public void SetResult(int result)
    {
      this.result = result;
      flags |= Flags.ResultExist | Flags.ResultChanged;
    }

    // **********************************************************************

    public bool OpenExist { get { return (flags & Flags.OpenExist) != Flags.None; } }
    public bool CloseExist { get { return (flags & Flags.CloseExist) != Flags.None; } }
    public bool ResultExist { get { return (flags & Flags.ResultExist) != Flags.None; } }

    // **********************************************************************

    //public string Date { get { return (OpenExist ? OpenTime : CloseTime).ToString("dd/MM/yyyy, dddd"); } }

    // **********************************************************************

    public string OpenTime { get { return OpenExist ? openTime.ToLongTimeString() : null; } }
    public string OpenQty { get { return OpenExist ? openQty.ToString("N", cfg.BaseCulture) : null; } }

    public string OpenPrice
    {
      get { return OpenExist && openQty != 0 ? Price.GetString(openSum / openQty, priceRatio) : null; }
    }

    // **********************************************************************

    public string CloseTime { get { return CloseExist ? closeTime.ToLongTimeString() : null; } }
    public string CloseQty { get { return CloseExist ? closeQty.ToString("N", cfg.BaseCulture) : null; } }

    public string ClosePrice
    {
      get { return CloseExist && closeQty != 0 ? Price.GetString(closeSum / closeQty, priceRatio) : null; }
    }

    public string Result { get { return ResultExist ? Price.GetString(result, priceRatio) : null; } }

    // **********************************************************************

    public static readonly string CsvHeader1 = "Открытие;;;;;Закрытие";

    public static readonly string CsvHeader2 =
      "Дата;Время;Кол-во;Цена;;Дата;Время;Кол-во;Цена;;Результат";

    // **********************************************************************

    public string CsvValue
    {
      get
      {
        string od = string.Empty;
        string ot = string.Empty;
        string oq = string.Empty;
        string op = string.Empty;

        string cd = string.Empty;
        string ct = string.Empty;
        string cq = string.Empty;
        string cp = string.Empty;

        string r = string.Empty;

        if(OpenExist)
        {
          od = openTime.ToShortDateString();
          ot = openTime.ToLongTimeString();
          oq = openQty.ToString();
          op = (Price.GetRaw(openSum, priceRatio) / openQty).ToString();
        }

        if(CloseExist)
        {
          cd = closeTime.ToShortDateString();
          ct = closeTime.ToLongTimeString();
          cq = closeQty.ToString();
          cp = (Price.GetRaw(closeSum, priceRatio) / closeQty).ToString();
        }

        if(ResultExist)
          r = Price.GetRaw(result, priceRatio).ToString();

        return string.Format("{0};{1};{2};{3};;{4};{5};{6};{7};;{8}",
          od, ot, oq, op, cd, ct, cq, cp, r);
      }
    }

    // **********************************************************************

    public event PropertyChangedEventHandler PropertyChanged;

    // **********************************************************************

    public void NotifyObservers()
    {
      if(PropertyChanged != null)
      {
        //PropertyChanged(this, new PropertyChangedEventArgs("Date"));

        if((flags & Flags.OpenChanged) != Flags.None)
        {
          PropertyChanged(this, new PropertyChangedEventArgs("OpenTime"));
          PropertyChanged(this, new PropertyChangedEventArgs("OpenQty"));
          PropertyChanged(this, new PropertyChangedEventArgs("OpenPrice"));

          flags &= ~Flags.OpenChanged;
        }

        if((flags & Flags.CloseChanged) != Flags.None)
        {
          PropertyChanged(this, new PropertyChangedEventArgs("CloseTime"));
          PropertyChanged(this, new PropertyChangedEventArgs("CloseQty"));
          PropertyChanged(this, new PropertyChangedEventArgs("ClosePrice"));

          flags &= ~Flags.CloseChanged;
        }

        if((flags & Flags.ResultChanged) != Flags.None)
        {
          PropertyChanged(this, new PropertyChangedEventArgs("Result"));
          flags &= ~Flags.ResultChanged;
        }
      }
    }

    // **********************************************************************
  }
}
