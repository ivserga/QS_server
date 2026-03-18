// =======================================================================
//    TabGraph.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =======================================================================

using System;

namespace QScalp.Windows
{
  partial class ConfigWindow
  {
    // **********************************************************************

    void InitGraph()
    {
      tradeVolume1.Value = cfg.u.TradeVolume1;
      tradeVolume1.ValueChanged += new EventHandler(tradeVolume1_ValueChanged);

      tradeVolume2.Value = cfg.u.TradeVolume2;
      tradeVolume2.ValueChanged += new EventHandler(tradeVolume2_ValueChanged);

      tradeVolume3.Value = cfg.u.TradeVolume3;
      tradeVolume3.ValueChanged += new EventHandler(tradeVolume3_ValueChanged);

      tradeVolume3Div.Value = cfg.u.TradeVolume3Div;
      tradesTickInterval.Value = cfg.u.TradesTickInterval;

      spreadXScale.Value = cfg.u.SpreadTickWidth;
      spreadXScale_ValueChanged(this, EventArgs.Empty);
      spreadXScale.ValueChanged += new EventHandler(spreadXScale_ValueChanged);

      spreadsTickInterval.Value = cfg.u.SpreadsTickInterval;
    }

    // **********************************************************************

    void tradeVolume1_ValueChanged(object sender, EventArgs e)
    {
      if(tradeVolume2.Value < tradeVolume1.Value)
        tradeVolume2.Value = tradeVolume1.Value;
    }

    // **********************************************************************

    void tradeVolume2_ValueChanged(object sender, EventArgs e)
    {
      if(tradeVolume1.Value > tradeVolume2.Value)
        tradeVolume1.Value = tradeVolume2.Value;

      if(tradeVolume3.Value < tradeVolume2.Value)
        tradeVolume3.Value = tradeVolume2.Value;
    }

    // **********************************************************************

    void tradeVolume3_ValueChanged(object sender, EventArgs e)
    {
      if(tradeVolume2.Value > tradeVolume3.Value)
        tradeVolume2.Value = tradeVolume3.Value;
    }

    // **********************************************************************

    void spreadXScale_ValueChanged(object sender, EventArgs e)
    {
      spreadsTickInterval.IsEnabled = spreadXScale.Value != 0;
    }

    // **********************************************************************

    void ApplyGraph()
    {
      cfg.u.TradeVolume1 = (int)tradeVolume1.Value;
      cfg.u.TradeVolume2 = (int)tradeVolume2.Value;
      cfg.u.TradeVolume3 = (int)tradeVolume3.Value;
      cfg.u.TradeVolume3Div = tradeVolume3Div.Value;
      cfg.u.TradesTickInterval = (int)tradesTickInterval.Value;

      cfg.u.SpreadTickWidth = spreadXScale.Value;
      cfg.u.SpreadsTickInterval = (int)spreadsTickInterval.Value;
    }

    // **********************************************************************
  }
}
