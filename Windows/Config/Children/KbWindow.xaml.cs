// ==========================================================================
//   KbWindow.xaml.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ==========================================================================

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using QScalp.ObjItems;
using QScalp.Windows.Controls;

namespace QScalp.Windows.Config
{
  public partial class KbWindow : Window
  {
    // **********************************************************************

    const string keyDownStr = "нажатии";
    const string keyUpStr = "отпускании";

    const string qtyTypeAbs = "абсолютный, лоты";
    const string qtyTypeRel = "относительный, %";

    // **********************************************************************

    bool createNewItem;
    NumStepBox priceStepBox;

    // **********************************************************************

    public KbItem KbItem { get; protected set; }

    // **********************************************************************

    public KbWindow(KbItem kbItem, bool createNewItem, NumStepBox priceStepBox)
    {
      InitializeComponent();

      // ------------------------------------------------------------

      this.KbItem = (kbItem == null) ? new KbItem(Key.None, true,
        new OwnAction(TradeOp.Buy, BaseQuote.Counter, 0, 0),
        0, priceStepBox.Ratio) : kbItem;

      this.createNewItem = createNewItem;
      this.priceStepBox = priceStepBox;

      // ------------------------------------------------------------

      actionKey.IsEnabled = createNewItem;
      buttonOk.Content = createNewItem ? "Добавить" : "Обновить";

      // ------------------------------------------------------------

      actionKey.Register();
      actionKey.Value = KbItem.Key;
      actionKey.TextChanged += new TextChangedEventHandler(TuneButtons);

      keyEvent.ItemsSource = new string[] { keyDownStr, keyUpStr };
      keyEvent.SelectedItem = KbItem.OnKeyDown ? keyDownStr : keyUpStr;
      keyEvent.SelectionChanged += new SelectionChangedEventHandler(TuneButtons);

      operation.ItemsSource = TradeOpItem.GetItems();
      operation.SelectedItem = new TradeOpItem(KbItem.Action.Operation);
      operation.SelectionChanged += new SelectionChangedEventHandler(TuneControls);

      baseQuote.ItemsSource = BaseQuoteItem.GetItems();
      baseQuote.SelectedItem = new BaseQuoteItem(KbItem.Action.Quote);
      if(baseQuote.SelectedIndex < 0)
        baseQuote.SelectedIndex = 0;
      baseQuote.SelectionChanged += new SelectionChangedEventHandler(TuneControls);

      offset.DecimalPlaces = priceStepBox.DecimalPlaces;
      offset.Increment = priceStepBox.Value;
      offset.Value = Price.GetRaw(KbItem.Action.Value, priceStepBox.Ratio);

      qtyType.ItemsSource = new string[] { qtyTypeAbs, qtyTypeRel };
      qtyType.SelectionChanged += new SelectionChangedEventHandler(qtyType_SelectionChanged);

      if(KbItem.Action.Quantity < 0)
      {
        qtyType.SelectedItem = qtyTypeRel;
        quantity.Value = -KbItem.Action.Quantity;
      }
      else
      {
        qtyType.SelectedItem = qtyTypeAbs;
        quantity.Value = KbItem.Action.Quantity;
      }

      // ------------------------------------------------------------

      Closed += new EventHandler(KbWindow_Closed);

      TuneButtons(this, null);
      TuneControls(this, null);

      // ------------------------------------------------------------
    }

    // **********************************************************************

    void KbWindow_Closed(object sender, EventArgs e)
    {
      actionKey.Unregister();
    }

    // **********************************************************************

    void TuneButtons(object sender, EventArgs e)
    {
      if(actionKey.Value == Key.None)
        buttonOk.IsEnabled = false;
      else
        buttonOk.IsEnabled = true;
    }

    // **********************************************************************

    void TuneControls(object sender, EventArgs e)
    {
      bool baseQuoteEnabled = false;
      bool offsetEnabled = false;
      bool quantityEnabled = false;

      if(operation.SelectedItem is TradeOpItem)
      {
        TradeOp op = ((TradeOpItem)operation.SelectedItem).Value;

        if(op != TradeOp.Cancel && op != TradeOp.Wait)
        {
          baseQuoteEnabled = true;

          if(baseQuote.SelectedItem is BaseQuoteItem
            && ((BaseQuoteItem)baseQuote.SelectedItem).Value != BaseQuote.Absolute)
            offsetEnabled = true;

          if(op != TradeOp.Close && op != TradeOp.Reverse)
            quantityEnabled = true;
        }
      }

      baseQuote.IsEnabled = baseQuoteEnabled;
      offset.IsEnabled = offsetEnabled;
      quantity.IsEnabled = quantityEnabled;
    }

    // **********************************************************************

    void qtyType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if(qtyType.SelectedItem as string == qtyTypeRel)
      {
        quantity.MinValue = 1;
        quantity.Value = 100;
      }
      else
      {
        quantity.MinValue = 0;
        quantity.Value = 0;
      }
    }

    // **********************************************************************

    private void buttonOk_Click(object sender, RoutedEventArgs e)
    {
      OwnAction action = new OwnAction(operation.SelectedItem is TradeOpItem
        ? ((TradeOpItem)operation.SelectedItem).Value : TradeOp.Cancel);

      // ------------------------------------------------------------

      if(action.Operation != TradeOp.Cancel && action.Operation != TradeOp.Wait)
      {
        if(baseQuote.SelectedItem is BaseQuoteItem)
        {
          action.Quote = ((BaseQuoteItem)baseQuote.SelectedItem).Value;

          if(action.Quote != BaseQuote.Absolute)
            action.Value = Price.GetInt(offset.Value, priceStepBox.Ratio);
        }

        if(action.Operation != TradeOp.Close && action.Operation != TradeOp.Reverse)
          if(qtyType.SelectedItem as string == qtyTypeRel)
            action.Quantity = -(int)quantity.Value;
          else
            action.Quantity = (int)quantity.Value;
      }

      // ------------------------------------------------------------

      if(createNewItem)
        KbItem = new KbItem(actionKey.Value, keyEvent.SelectedItem
          as string == keyDownStr, action, 0, priceStepBox.Ratio);
      else
        KbItem.Action = action;

      // ------------------------------------------------------------

      DialogResult = true;
      this.Close();
    }

    // **********************************************************************
  }
}
