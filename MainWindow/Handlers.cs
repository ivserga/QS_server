// =======================================================================
//    Handlers.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =======================================================================

using System.Windows.Input;
using QScalp.XMedia;

namespace QScalp
{
  partial class MainWindow
  {
    // **********************************************************************
    // *                       Обработка клавиатуры                         *
    // **********************************************************************

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
      if(IsKeyboardFocused)
      {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        switch(key)
        {
          // --------------------------------------------------------

          case cfg.FKeySaveConf:
            MenuSaveConf_Click(this, null);
            break;
          case cfg.FKeyLoadConf:
            MenuLoadConf_Click(this, null);
            break;

          case cfg.FKeyCfgOrExit:
            if((e.KeyboardDevice.Modifiers & ModifierKeys.Alt)
              == ModifierKeys.None)
              MenuSettings_Click(this, null);
            else
              MenuExit_Click(this, null);
            break;

          case cfg.FKeyTradeLog:
            MenuTradeLog_Click(this, null);
            break;

          case cfg.FKeyDropPos:
            MenuDropPos_Click(this, null);
            break;
          case cfg.FKeyClearGuide:
            MenuClearGuide_Click(this, null);
            break;
          case cfg.FKeyClearLevels:
            MenuClearLevels_Click(this, null);
            break;

          case cfg.FKeyShowMenu:
            menu.IsSubmenuOpen = true;
            menu.Focus();
            break;

          // --------------------------------------------------------

          default:
            if(key == cfg.u.KeyCenterSpread)
              sv.CenterSpread();

            else if(key == cfg.u.KeyPageUp)
              sv.Page(1);
            else if(key == cfg.u.KeyPageDown)
              sv.Page(-1);

            else if(key == cfg.u.KeyWorkSizeInc)
            {
              if(!e.IsRepeat)
                UpdateWorkSize(cfg.u.WorkSizeDelta);
            }
            else if(key == cfg.u.KeyWorkSizeDec)
            {
              if(!e.IsRepeat)
                UpdateWorkSize(-cfg.u.WorkSizeDelta);
            }

            else if(!pressedKeys.Contains(key))
            {
              TryExecAction(cfg.u.KeyDownBindings, key);
              pressedKeys.Add(key);
            }

            break;

          // --------------------------------------------------------
        }

        UpdateKeyStatus();
        e.Handled = true;
      }
      else
        base.OnPreviewKeyDown(e);
    }

    // **********************************************************************

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
      if(IsKeyboardFocused)
      {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        TryExecAction(cfg.u.KeyUpBindings, key);
        pressedKeys.Remove(key);

        UpdateKeyStatus();
        e.Handled = true;
      }
      else
        base.OnPreviewKeyUp(e);
    }

    // **********************************************************************

    void TryExecAction(KeyBindings bindings, Key key)
    {
      OwnAction[] actions;

      if(!pressedKeys.Contains(cfg.u.KeyBlockKey)
        && bindings.TryGetValue(key, out actions))
      {
        for(int i = 0; i < actions.Length; i++)
        {
          OwnAction a = actions[i];

          if(a.Quote == BaseQuote.Absolute)
          {
            if(sv.SelectedPrice != 0)
            {
              a.Value = sv.SelectedPrice;
              tmgr.ExecAction(a);
            }
          }
          else
            tmgr.ExecAction(a);
        }
      }
    }

    // **********************************************************************
    // *                          Обработка мыши                            *
    // **********************************************************************

    void QuoteClick(MouseButtonEventArgs e, int price)
    {
      switch(e.ChangedButton)
      {
        // ----------------------------------------------

        case MouseButton.Left:

          if(cfg.u.MouseEnabled && tmgr.AskPrice > tmgr.BidPrice)
          {
            if(price >= tmgr.AskPrice)
              tmgr.ExecAction(new OwnAction(
                TradeOp.Sell,
                BaseQuote.Absolute,
                price,
                cfg.u.WorkSize));

            else if(price <= tmgr.BidPrice)
              tmgr.ExecAction(new OwnAction(
                TradeOp.Buy,
                BaseQuote.Absolute,
                price,
                cfg.u.WorkSize));
          }

          break;

        // ----------------------------------------------

        case MouseButton.Middle:

          if(cfg.u.MouseEnabled && tmgr.AskPrice > tmgr.BidPrice)
          {
            if(price >= tmgr.AskPrice)
              tmgr.CreateStopOrder(
                price,
                price + cfg.u.MouseSlippage,
                cfg.u.WorkSize);

            else if(price <= tmgr.BidPrice)
              tmgr.CreateStopOrder(
                price,
                price - cfg.u.MouseSlippage,
                -cfg.u.WorkSize);
          }

          break;

        // ----------------------------------------------

        case MouseButton.Right:

          tmgr.ExecAction(new OwnAction(
            TradeOp.Cancel,
            BaseQuote.Absolute,
            price,
            0));

          break;

        // ----------------------------------------------
      }
    }

    // **********************************************************************

    void WorkSizeStatusClick(object sender, MouseButtonEventArgs e)
    {
      switch(e.ChangedButton)
      {
        case MouseButton.Left:
          UpdateWorkSize(cfg.u.WorkSizeDelta);
          break;

        case MouseButton.Right:
          UpdateWorkSize(-cfg.u.WorkSizeDelta);
          break;
      }
    }

    // **********************************************************************

    void KeyStatusClick(object sender, MouseButtonEventArgs e)
    {
      if(e.ChangedButton == MouseButton.Left && e.ClickCount > 1)
      {
        MenuSettings_Click(sender, e);
        cfgw.ShowKeyBindings();
      }
    }

    // **********************************************************************

    void PosStatusClick(object sender, MouseButtonEventArgs e)
    {
      if(e.ChangedButton == MouseButton.Left && e.ClickCount > 1)
      {
        MenuSettings_Click(sender, e);
        cfgw.ShowPosition();
      }
    }

    // **********************************************************************

    void ResultStatusClick(object sender, MouseButtonEventArgs e)
    {
      if(e.ChangedButton == MouseButton.Left && e.ClickCount > 1)
        MenuTradeLog_Click(sender, e);
    }

    // **********************************************************************
  }
}
