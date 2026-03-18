// ==========================================================================
//    TabPosition.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ==========================================================================

using System;

namespace QScalp.Windows
{
  partial class ConfigWindow
  {
    // **********************************************************************

    void InitPosition()
    {
      stopOffset.Value = Price.GetRaw(cfg.u.AutoStopOffset);
      stopOffset_ValueChanged(this, EventArgs.Empty);
      stopOffset.ValueChanged += new EventHandler(stopOffset_ValueChanged);

      stopSlippage.Value = Price.GetRaw(cfg.u.AutoStopSlippage);
      stopTrail.IsChecked = cfg.u.AutoStopTrail;
      takeOffset.Value = Price.GetRaw(cfg.u.AutoTakeOffset);

      singleTradeTimeout.Value = cfg.u.SingleTradeTimeout;
      tradeLogFlush.IsChecked = cfg.u.TradeLogFlush;
    }

    // **********************************************************************

    void stopOffset_ValueChanged(object sender, EventArgs e)
    {
      stopSlippage.IsEnabled =
        stopTrail.IsEnabled =
        stopOffset.Value != 0;
    }

    // **********************************************************************

    void ApplyPosition()
    {
      cfg.u.AutoStopOffset = Price.GetInt(stopOffset.Value);
      cfg.u.AutoStopSlippage = Price.GetInt(stopSlippage.Value);
      cfg.u.AutoStopTrail = stopTrail.IsChecked == true;
      cfg.u.AutoTakeOffset = Price.GetInt(takeOffset.Value);

      cfg.u.SingleTradeTimeout = (int)singleTradeTimeout.Value;
      cfg.u.TradeLogFlush = tradeLogFlush.IsChecked == true;
    }

    // **********************************************************************
  }
}
