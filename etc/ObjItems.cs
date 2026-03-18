// =======================================================================
//    ObjItems.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =======================================================================

using System;
using System.ComponentModel;
using System.Windows.Input;

namespace QScalp.ObjItems
{
  // ************************************************************************

  struct TradeOpItem
  {
    public readonly TradeOp Value;
    public TradeOpItem(TradeOp value) { this.Value = value; }

    public override string ToString() { return ToString(Value); }

    public static string ToString(TradeOp value)
    {
      switch(value)
      {
        case TradeOp.Buy:
          return "Покупка";
        case TradeOp.Sell:
          return "Продажа";
        case TradeOp.Upsize:
          return "Наращивание";
        case TradeOp.Downsize:
          return "Уменьшение";
        case TradeOp.Close:
          return "Закрытие";
        case TradeOp.Reverse:
          return "Разворот";
        case TradeOp.Cancel:
          return "Отмена";
        case TradeOp.Wait:
          return "Ожидание";
      }

      return value.ToString();
    }

    public static TradeOpItem[] GetItems()
    {
      return new TradeOpItem[] {
        new TradeOpItem(TradeOp.Buy),
        new TradeOpItem(TradeOp.Sell),
        new TradeOpItem(TradeOp.Upsize),
        new TradeOpItem(TradeOp.Downsize),
        new TradeOpItem(TradeOp.Close),
        new TradeOpItem(TradeOp.Reverse),
        new TradeOpItem(TradeOp.Cancel),
        new TradeOpItem(TradeOp.Wait) };
    }
  }

  // ************************************************************************

  struct BaseQuoteItem
  {
    public readonly BaseQuote Value;
    public BaseQuoteItem(BaseQuote value) { this.Value = value; }

    public override string ToString() { return ToString(Value, true); }

    public static string ToString(BaseQuote value) { return ToString(value, false); }

    public static string ToString(BaseQuote value, bool detailed)
    {
      switch(value)
      {
        case BaseQuote.Counter:
          return detailed ? "относительно лучшей встречной котировки" : "Встречная";
        case BaseQuote.Similar:
          return detailed ? "относительно лучшей попутной котировки" : "Попутная";
        case BaseQuote.Absolute:
          return detailed ? "указанная мышью в стакане" : "Указанная";
      }

      return value.ToString();
    }

    public static BaseQuoteItem[] GetItems()
    {
      Array values = Enum.GetValues(typeof(BaseQuote));
      BaseQuoteItem[] items = new BaseQuoteItem[values.Length - 1];

      for(int i = 1; i < values.Length; i++)
        items[i - 1] = new BaseQuoteItem((BaseQuote)values.GetValue(i));

      return items;
    }
  }

  // ************************************************************************

  struct ClusterBaseItem
  {
    public readonly ClusterBase Value;
    public ClusterBaseItem(ClusterBase value) { this.Value = value; }

    public override string ToString()
    {
      switch(Value)
      {
        case ClusterBase.Time: return "время (секунды)";
        case ClusterBase.Volume: return "объем";
        case ClusterBase.Range: return "диапазон цены";
        case ClusterBase.Ticks: return "кол-во сделок";
        case ClusterBase.Delta: return "модуль дельты";
      }

      return Value.ToString();
    }

    public static ClusterBaseItem[] GetItems()
    {
      Array values = Enum.GetValues(typeof(ClusterBase));
      ClusterBaseItem[] items = new ClusterBaseItem[values.Length - 1];

      for(int i = 1; i < values.Length; i++)
        items[i - 1] = new ClusterBaseItem((ClusterBase)values.GetValue(i));

      return items;
    }
  }

  // ************************************************************************

  struct ClusterViewItem
  {
    public readonly ClusterView Value;
    public ClusterViewItem(ClusterView value) { this.Value = value; }

    public override string ToString()
    {
      switch(Value)
      {
        case ClusterView.Summary: return "суммарный объем";
        case ClusterView.Separate: return "объемы по ask и bid";
        case ClusterView.Delta: return "разность ask и bid";
      }

      return Value.ToString();
    }

    public static ClusterViewItem[] GetItems()
    {
      Array values = Enum.GetValues(typeof(ClusterView));
      ClusterViewItem[] items = new ClusterViewItem[values.Length];

      for(int i = 0; i < values.Length; i++)
        items[i] = new ClusterViewItem((ClusterView)values.GetValue(i));

      return items;
    }
  }

  // ************************************************************************

  struct ClusterFillItem
  {
    public readonly ClusterFill Value;
    public ClusterFillItem(ClusterFill value) { this.Value = value; }

    public override string ToString()
    {
      switch(Value)
      {
        case ClusterFill.Double: return "относительно двойной шкалы";
        case ClusterFill.SingleDelta: return "с раскраской по дельте";
        case ClusterFill.SingleBalance: return "с балансом между ask и bid";
      }

      return Value.ToString();
    }

    public static ClusterFillItem[] GetItems()
    {
      Array values = Enum.GetValues(typeof(ClusterFill));
      ClusterFillItem[] items = new ClusterFillItem[values.Length];

      for(int i = 0; i < values.Length; i++)
        items[i] = new ClusterFillItem((ClusterFill)values.GetValue(i));

      return items;
    }
  }

  // ************************************************************************

  public class KbItem : INotifyPropertyChanged
  {
    // --------------------------------------------------------------

    public readonly Key Key;
    public readonly bool OnKeyDown;

    // --------------------------------------------------------------

    OwnAction action;
    int id;
    int priceRatio;

    // --------------------------------------------------------------

    public event PropertyChangedEventHandler PropertyChanged;

    // --------------------------------------------------------------

    public KbItem(Key key, bool onKeyDown, OwnAction action, int id, int priceRatio)
    {
      this.Key = key;
      this.OnKeyDown = onKeyDown;
      this.action = action;
      this.id = id;
      this.priceRatio = priceRatio;
    }

    // --------------------------------------------------------------

    public OwnAction Action
    {
      get { return action; }
      set
      {
        action = value;

        if(PropertyChanged != null)
        {
          PropertyChanged(this, new PropertyChangedEventArgs("Operation"));
          PropertyChanged(this, new PropertyChangedEventArgs("Quote"));
          PropertyChanged(this, new PropertyChangedEventArgs("Value"));
          PropertyChanged(this, new PropertyChangedEventArgs("Quantity"));
        }
      }
    }

    public int Id
    {
      get { return id; }
      set
      {
        if(id != value)
        {
          id = value;

          if(PropertyChanged != null)
            PropertyChanged(this, new PropertyChangedEventArgs("Id"));
        }
      }
    }

    public int PriceRatio
    {
      get { return priceRatio; }
      set
      {
        if(priceRatio != value)
        {
          priceRatio = value;

          if(PropertyChanged != null)
            PropertyChanged(this, new PropertyChangedEventArgs("Value"));
        }
      }
    }

    // --------------------------------------------------------------

    public string KeyEvent { get { return Key + ", " + (OnKeyDown ? "нажатие" : "отпускание"); } }

    public string Operation { get { return TradeOpItem.ToString(action.Operation); } }

    public string Quote
    {
      get
      {
        return action.Operation == TradeOp.Cancel || action.Operation == TradeOp.Wait
          ? null : BaseQuoteItem.ToString(action.Quote);
      }
    }

    public string Value
    {
      get
      {
        return action.Operation == TradeOp.Cancel
          || action.Operation == TradeOp.Wait
          || action.Quote == BaseQuote.Absolute
          ? null : Price.GetString(action.Value, PriceRatio);
      }
    }

    public string Quantity
    {
      get
      {
        if(action.Operation == TradeOp.Cancel || action.Operation == TradeOp.Wait
          || action.Operation == TradeOp.Close || action.Operation == TradeOp.Reverse)
          return null;
        else if(action.Quantity < 0)
          return "(" + (-action.Quantity).ToString("N", cfg.BaseCulture) + "%)";
        else
          return action.Quantity.ToString("N", cfg.BaseCulture);
      }
    }

    // --------------------------------------------------------------
  }

  // ************************************************************************
}
