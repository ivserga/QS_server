// ========================================================================
//  TabManagement.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ========================================================================

using System;

namespace QScalp.Windows
{
  partial class ConfigWindow
  {
    // **********************************************************************

    void InitManagement()
    {
      keyBlockKey.Register();

      keyCenterSpread.Register();
      keyPageUp.Register();
      keyPageDown.Register();

      keyWorkSizeInc.Register();
      keyWorkSizeDec.Register();

      this.Closed += delegate
      {
        keyBlockKey.Unregister();

        keyCenterSpread.Unregister();
        keyPageUp.Unregister();
        keyPageDown.Unregister();

        keyWorkSizeInc.Unregister();
        keyWorkSizeDec.Unregister();
      };

      keyBlockKey.Value = cfg.u.KeyBlockKey;
      keyCenterSpread.Value = cfg.u.KeyCenterSpread;
      keyPageUp.Value = cfg.u.KeyPageUp;
      keyPageDown.Value = cfg.u.KeyPageDown;

      mouseEnabled.IsChecked = cfg.u.MouseEnabled;
      mouseSlippage.Value = Price.GetRaw(cfg.u.MouseSlippage);

      workSize.Value = cfg.u.WorkSize;
      workSizeDelta.Value = cfg.u.WorkSizeDelta;

      keyWorkSizeInc.Value = cfg.u.KeyWorkSizeInc;
      keyWorkSizeDec.Value = cfg.u.KeyWorkSizeDec;
    }

    // **********************************************************************

    void ApplyManagement()
    {
      cfg.u.KeyBlockKey = keyBlockKey.Value;
      cfg.u.KeyCenterSpread = keyCenterSpread.Value;
      cfg.u.KeyPageUp = keyPageUp.Value;
      cfg.u.KeyPageDown = keyPageDown.Value;

      cfg.u.MouseEnabled = mouseEnabled.IsChecked == true;
      cfg.u.MouseSlippage = Price.GetInt(mouseSlippage.Value);

      cfg.u.WorkSize = (int)workSize.Value;
      cfg.u.WorkSizeDelta = (int)workSizeDelta.Value;

      cfg.u.KeyWorkSizeInc = keyWorkSizeInc.Value;
      cfg.u.KeyWorkSizeDec = keyWorkSizeDec.Value;
    }

    // **********************************************************************
  }
}
