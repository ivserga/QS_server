// =====================================================================
//    VStock.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// =====================================================================

using System.Windows;
using System.Windows.Media;

namespace QScalp.View.StockSpace
{
  class VStock : ContainerVisual, IVObsoletable, IVScrollable, IOrdersHandler
  {
    // **********************************************************************

    ViewManager vmgr;
    Quote[] quotes;

    // **********************************************************************

    public bool Obsolete { get; private set; }

    // **********************************************************************

    public VStock(ViewManager vmgr)
    {
      this.vmgr = vmgr;

      vmgr.RegisterObject(this);
      vmgr.OrdersList.RegisterHandler(this);
    }

    // **********************************************************************

    public void OrdersUpdated(int price) { Obsolete = true; }

    // **********************************************************************

    public void PutQuotes(Quote[] quotes)
    {
      this.quotes = quotes;
      Obsolete = true;
    }

    // **********************************************************************

    public void Refresh()
    {
      Obsolete = false;

      Quote[] quotes = this.quotes;
      int row = 0;

      if(quotes == null)
        foreach(StockQuote sq in Children)
          sq.Update(QuoteType.Spread, 0, false);
      else
        foreach(StockQuote sq in Children)
        {
          // --------------------------------------------------------

          QuoteType type = QuoteType.Free;
          int volume = 0;

          // --------------------------------------------------------

          while(row < quotes.Length)
          {
            if(quotes[row].Price == sq.Price)
            {
              type = quotes[row].Type;
              volume = quotes[row].Volume;

              row++;
              break;
            }
            else if(quotes[row].Price < sq.Price)
            {
              if(sq.Price < vmgr.Ask && sq.Price > vmgr.Bid)
                type = QuoteType.Spread;

              break;
            }

            row++;
          }

          // --------------------------------------------------------

          sq.Update(type, volume, vmgr.OrdersList.Contains(sq.Price));

          // --------------------------------------------------------
        }
    }

    // **********************************************************************

    StockQuote ChildQuote(int index) { return (StockQuote)Children[index]; }

    // **********************************************************************

    StockQuote NewQuote(int price)
    {
      StockQuote quote = new StockQuote(price);
      quote.Offset = new Vector(0, vmgr.PriceOffset(price));
      return quote;
    }

    // **********************************************************************

    public void UpdateOffset()
    {
      Offset = new Vector(0, vmgr.BaseY);

      while(Children.Count > 0 && !vmgr.QVisible(ChildQuote(0).Price))
        Children.RemoveAt(0);

      while(Children.Count > 0 && !vmgr.QVisible(ChildQuote(Children.Count - 1).Price))
        Children.RemoveAt(Children.Count - 1);

      if(Children.Count == 0)
        Children.Add(NewQuote(vmgr.PriceFromY(0)));

      while(vmgr.QVisible(ChildQuote(0).Price + cfg.u.PriceStep))
        Children.Insert(0, NewQuote(ChildQuote(0).Price + cfg.u.PriceStep));

      while(vmgr.QVisible(ChildQuote(Children.Count - 1).Price - cfg.u.PriceStep))
        Children.Add(NewQuote(ChildQuote(Children.Count - 1).Price - cfg.u.PriceStep));

      Refresh();
    }

    // **********************************************************************

    public void Rebuild()
    {
      Children.Clear();
      UpdateOffset();
    }

    // **********************************************************************

    /// <summary>
    /// Очищает котировки (для перезагрузки данных за другую дату)
    /// </summary>
    public void Clear()
    {
      quotes = null;
      Obsolete = true;
    }

    // **********************************************************************
  }
}
