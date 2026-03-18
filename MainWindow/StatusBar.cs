// ========================================================================
//    StatusBar.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ========================================================================

using System;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

using QScalp.Connector;

namespace QScalp
{
  partial class MainWindow
  {
    // **********************************************************************

    void UpdateDataProviderStatus(StatusBarItem sbItem)
    {
      Brush b;

      if(dp.IsConnected)
        if(dp.IsError)
        {
          b = Brushes.Red;
        }
        else
        {
          b = Brushes.LimeGreen;
        }
      else
        b = Brushes.Silver;

      if(sbItem.Background != b)
        sbItem.Background = b;
    }

    // **********************************************************************

    void SbUpdaterTick(object sender, EventArgs e)
    {
      if(sv.AutoCentering)
      {
        if(acStatus.Text.Length != 2)
          acStatus.Text = "\x2191\x2193";
      }
      else
      {
        if(acStatus.Text.Length != 1)
          acStatus.Text = "-";
      }

      // ------------------------------------------------------------

      // Обновляем статус единого REST API поллера
      UpdateDataProviderStatus(stockStatus);
      UpdateDataProviderStatus(tradesStatus);

      // ------------------------------------------------------------

      if(tmgr.ConnectionUpdated)
      {
        switch(tmgr.Connected)
        {
          case TermConnection.Full:
            quikStatus.Background = Brushes.LimeGreen;
            break;

          case TermConnection.Partial:
            quikStatus.Background = Brushes.Orange;
            break;

          case TermConnection.Emulation:
            quikStatus.Background = Brushes.Silver;
            break;

          default:
            quikStatus.Background = Brushes.Red;
            break;
        }

        quikStatus.ToolTip = tmgr.ConnectionText;
      }

      // ------------------------------------------------------------

      if(tmgr.QueueUpdated)
      {
        if(tmgr.QueueLength > 0)
          opqStatus.Text = "\x2022 " + tmgr.QueueLength + " \x2022";
        else
          opqStatus.Text = "\x00b7 \x00b7 \x00b7";

        opqStatus.ToolTip = tmgr.QueueText;
      }

      // ------------------------------------------------------------

      if(tmgr.Position.ByOrdersUpdated)
      {
        if(tmgr.Position.ByOrders > 0)
          posStatus.Text = "L " + tmgr.Position.ByOrders.ToString("N", cfg.BaseCulture);
        else if(tmgr.Position.ByOrders < 0)
          posStatus.Text = "S " + (-tmgr.Position.ByOrders).ToString("N", cfg.BaseCulture);
        else
          posStatus.Text = "\x00d8";
      }

      // ------------------------------------------------------------

      if(tmgr.Position.TradeLog.Updated)
      {
        const string sep = "   /   ";

        resultStatus.Text = Price.GetString(tmgr.Position.TradeLog.AverageResultPerLot)
          + sep + tmgr.Position.TradeLog.TradesCount
          + sep + Price.GetString(tmgr.Position.TradeLog.LastResult);

        if(tlw != null)
          tlw.Update();
      }

      // ------------------------------------------------------------
    }

    // **********************************************************************

    void UpdateWorkSize(int delta)
    {
      if((cfg.u.WorkSize += delta) < 0)
        cfg.u.WorkSize = 0;

      wsStatus.Text = "( " + cfg.u.WorkSize.ToString("N", cfg.BaseCulture) + " )";
    }

    // **********************************************************************

    void UpdateKeyStatus()
    {
      if(IsKeyboardFocused)
      {
        if(pressedKeys.Contains(cfg.u.KeyBlockKey))
          keyStatus.Text = "\x00d7";
        else if(pressedKeys.Count > 0)
          keyStatus.Text = "\x2117";
        else
          keyStatus.Text = "\x25cb";

        sv.Opacity = 1;
        keyStatusItem.Background = Brushes.Transparent;
      }
      else
      {
        sv.Opacity = cfg.s.LostFocusOpacity;
        keyStatusItem.Background = Brushes.Silver;
        keyStatus.Text = "нет фокуса";
      }
    }

    // **********************************************************************

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
      UpdateKeyStatus();
      pressedKeys.Clear();

      base.OnLostKeyboardFocus(e);
    }

    // **********************************************************************

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
      UpdateKeyStatus();
      base.OnGotKeyboardFocus(e);
    }

    // **********************************************************************
  }
}
