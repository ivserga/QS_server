// =======================================================================
//    TabOther.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =======================================================================

using System;

namespace QScalp.Windows
{
  partial class ConfigWindow
  {
    // **********************************************************************

    void InitOther()
    {
      // REST API / WebSocket
      apiBaseUrl.Text = cfg.u.ApiBaseUrl;
      apiKey.Password = cfg.u.ApiKey;
      wsBaseUrl.Text = cfg.u.WsBaseUrl;
      apiDataDate.Text = cfg.u.ApiDataDate;
      wsDebugMode.IsChecked = cfg.u.WsDebugMode;
      skipHistoricalData.IsChecked = cfg.u.SkipHistoricalData;

      // DDE (устаревший)
      ddeServerName.Text = cfg.u.DdeServerName;

      enableQuikLog.IsChecked = cfg.u.EnableQuikLog;
      acceptAllTrades.IsChecked = cfg.u.AcceptAllTrades;

      emulatorDelayMin.Value = cfg.u.EmulatorDelayMin;
      emulatorDelayMin.ValueChanged += new EventHandler(emulatorDelayMin_ValueChanged);

      emulatorDelayMax.Value = cfg.u.EmulatorDelayMax;
      emulatorDelayMax.ValueChanged += new EventHandler(emulatorDelayMax_ValueChanged);

      emulatorLimit.Value = cfg.u.EmulatorLimit;

      fontFamily.Text = cfg.u.FontFamily;
      fontSize.Value = cfg.u.FontSize;
    }

    // **********************************************************************

    void emulatorDelayMin_ValueChanged(object sender, EventArgs e)
    {
      if(emulatorDelayMax.Value < emulatorDelayMin.Value)
        emulatorDelayMax.Value = emulatorDelayMin.Value;
    }

    // **********************************************************************

    void emulatorDelayMax_ValueChanged(object sender, EventArgs e)
    {
      if(emulatorDelayMin.Value > emulatorDelayMax.Value)
        emulatorDelayMin.Value = emulatorDelayMax.Value;
    }

    // **********************************************************************

    void ApplyOther()
    {
      // REST API / WebSocket
      cfg.u.ApiBaseUrl = apiBaseUrl.Text;
      cfg.u.ApiKey = apiKey.Password;
      cfg.u.WsBaseUrl = wsBaseUrl.Text;
      cfg.u.ApiDataDate = apiDataDate.Text.Trim();
      cfg.u.WsDebugMode = wsDebugMode.IsChecked == true;
      cfg.u.SkipHistoricalData = skipHistoricalData.IsChecked == true;

      // DDE (устаревший)
      cfg.u.DdeServerName = ddeServerName.Text;

      cfg.u.EnableQuikLog = enableQuikLog.IsChecked == true;
      cfg.u.AcceptAllTrades = acceptAllTrades.IsChecked == true;

      cfg.u.EmulatorDelayMin = (int)emulatorDelayMin.Value;
      cfg.u.EmulatorDelayMax = (int)emulatorDelayMax.Value;
      cfg.u.EmulatorLimit = (int)emulatorLimit.Value;

      cfg.u.FontFamily = fontFamily.Text;
      cfg.u.FontSize = fontSize.Value;
    }

    // **********************************************************************
  }
}
