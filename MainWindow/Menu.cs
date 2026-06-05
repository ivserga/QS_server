// ===================================================================
//    Menu.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ===================================================================

using System;
using System.Globalization;
using System.IO;
using System.Windows;
using QScalp.Windows;

namespace QScalp
{
  partial class MainWindow
  {
    // **********************************************************************

    void InitMenuTips()
    {
      menu.ToolTip = "Главное меню (" + cfg.FKeyShowMenu + ")";

      menuSaveConf.InputGestureText = cfg.FKeySaveConf.ToString();
      menuLoadConf.InputGestureText = cfg.FKeyLoadConf.ToString();
      menuSettings.InputGestureText = cfg.FKeyCfgOrExit.ToString();
      menuTradeLog.InputGestureText = cfg.FKeyTradeLog.ToString();
      menuDropPos.InputGestureText = cfg.FKeyDropPos.ToString();
      menuClearGuide.InputGestureText = cfg.FKeyClearGuide.ToString();
      menuClearLevels.InputGestureText = cfg.FKeyClearLevels.ToString();

      menuEmulation.IsChecked = cfg.u.TermEmulation;
      menuLlmAnalysis.IsChecked = cfg.u.LlmAnalysisEnabled;
    }

    // **********************************************************************
    // *                          Функции меню                              *
    // **********************************************************************

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
      AboutWindow aw = new AboutWindow();
      aw.Owner = this;
      aw.ShowDialog();
    }

    // **********************************************************************

    const string FileDialogsFilter = "Настройки " + cfg.ProgName
      + " (*." + cfg.UserCfgFileExt + ")|*." + cfg.UserCfgFileExt;

    private void MenuSaveConf_Click(object sender, RoutedEventArgs e)
    {
      System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog();

      sfd.Filter = FileDialogsFilter;
      sfd.RestoreDirectory = true;
      sfd.Title = "Выгрузить настройки в файл";

      string dot = ".";
      sfd.FileName = cfg.ProgName + dot + cfg.u.SecCode
        + dot + cfg.u.ClassCode + dot + cfg.UserCfgFileExt;

      if(sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
      {
        SaveTlwLocation();
        SaveWindowState();
        cfg.SaveUserConfig(sfd.FileName);
      }

      Focus();
    }

    // **********************************************************************

    private void MenuLoadConf_Click(object sender, RoutedEventArgs e)
    {
      System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();

      ofd.Filter = FileDialogsFilter;
      ofd.RestoreDirectory = true;
      ofd.Title = "Загрузить настройки из файла";

      if(ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
      {
        foreach(Window w in OwnedWindows)
          w.Close();

        this.Hide();

        UserSettings35 oldSettings = cfg.u;

        cfg.LoadUserConfig(ofd.FileName);
        LoadWindowState();
        CheckConfigChanges(oldSettings);

        this.Show();

        InitTradeLogWindow();
      }

      Focus();
    }

    // **********************************************************************

    private void ExportClustersButton_Click(object sender, RoutedEventArgs e)
    {
      DateTime from, to;
      if(!sv.TryGetClusterPeriod(out from, out to))
      {
        MessageBox.Show("Нет кластеров для выгрузки.", cfg.ProgName,
          MessageBoxButton.OK, MessageBoxImage.Information);
        Focus();
        return;
      }

      ClusterExportPeriodWindow pew = new ClusterExportPeriodWindow(from, to);
      pew.Owner = this;

      if(pew.ShowDialog() != true)
      {
        Focus();
        return;
      }

      int count = sv.CountClustersForExport(pew.From, pew.To);
      if(count == 0)
      {
        MessageBox.Show("В выбранном периоде нет кластеров.", cfg.ProgName,
          MessageBoxButton.OK, MessageBoxImage.Information);
        Focus();
        return;
      }

      System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog();
      sfd.Filter = "JSON (*.json)|*.json|Все файлы (*.*)|*.*";
      sfd.DefaultExt = "json";
      sfd.RestoreDirectory = true;
      sfd.Title = "Выгрузить кластеры для нейросети";
      sfd.FileName = MakeSafeFileName("clusters." + cfg.u.SecCode + "." + cfg.u.ClassCode
        + "." + DateTime.Now.ToString("yyyyMMdd.HHmmss", CultureInfo.InvariantCulture) + ".json");

      if(sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        try
        {
          int exported = sv.ExportClustersForAi(pew.From, pew.To, sfd.FileName);
          MessageBox.Show("Экспортировано кластеров: " + exported.ToString(CultureInfo.InvariantCulture),
            cfg.ProgName, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch(Exception ex)
        {
          MessageBox.Show("Ошибка выгрузки кластеров: " + ex.Message, cfg.ProgName,
            MessageBoxButton.OK, MessageBoxImage.Error);
        }

      Focus();
    }

    // **********************************************************************

    static string MakeSafeFileName(string fileName)
    {
      foreach(char c in Path.GetInvalidFileNameChars())
        fileName = fileName.Replace(c, '_');

      return fileName;
    }

    // **********************************************************************

    private void MenuSettings_Click(object sender, RoutedEventArgs e)
    {
      if(cfgw == null)
      {
        cfgw = new ConfigWindow();
        cfgw.Owner = this;
        cfgw.Closing += delegate { Activate(); };
        cfgw.Closed += delegate { cfgw = null; };

        cfgw.ApplyChanges += delegate
        {
          CheckConfigChanges(cfgw.SavedSettings);
        };

        cfgw.Show();
      }
      else
        cfgw.Activate();
    }

    // **********************************************************************

    private void MenuTradeLog_Click(object sender, RoutedEventArgs e)
    {
      cfg.u.ShowTradeLog = !cfg.u.ShowTradeLog;
      InitTradeLogWindow();
    }

    // **********************************************************************

    private void MenuEmulation_Click(object sender, RoutedEventArgs e)
    {
      if((tmgr.Position.ByOrders == 0 && tmgr.QueueLength == 0)
         || MessageBox.Show("Переключение режима эмуляции приведет к сбросу\n"
              + "текущей информации о позиции. Продолжить?", cfg.ProgName,
              MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
      {
        sv.ClearOrders();
        cfg.u.TermEmulation = !cfg.u.TermEmulation;

        tmgr.Disconnect();
        tmgr.DropState();
        tmgr.Connect();
      }

      menuEmulation.IsChecked = cfg.u.TermEmulation;
    }

    // **********************************************************************

    private void MenuLlmAnalysis_Click(object sender, RoutedEventArgs e)
    {
      cfg.u.LlmAnalysisEnabled = !cfg.u.LlmAnalysisEnabled;
      menuLlmAnalysis.IsChecked = cfg.u.LlmAnalysisEnabled;
    }

    // **********************************************************************

    private void MenuDropPos_Click(object sender, RoutedEventArgs e)
    {
      if(MessageBox.Show("Сбросить информацию о текущей позиции?", cfg.ProgName,
        MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
      {
        tmgr.DropState();
        sv.ClearOrders();
      }
    }

    // **********************************************************************

    private void MenuClearGuide_Click(object sender, RoutedEventArgs e)
    {
      sv.ClearGuide();
    }

    // **********************************************************************

    private void MenuClearLevels_Click(object sender, RoutedEventArgs e)
    {
      sv.ClearLevels();
    }

    // **********************************************************************

    private void MenuExit_Click(object sender, RoutedEventArgs e) { Close(); }

    // **********************************************************************
    // *                           TradeLog Window                          *
    // **********************************************************************

    void InitTradeLogWindow()
    {
      menuTradeLog.IsChecked = cfg.u.ShowTradeLog;

      if(tlw == null)
      {
        if(cfg.u.ShowTradeLog)
        {
          tlw = new TradeLogWindow(tmgr.Position.TradeLog);

          tlw.Owner = this;
          tlw.Closing += delegate { cfg.u.ShowTradeLog = false; };

          tlw.Closed += delegate
          {
            SaveTlwLocation();
            menuTradeLog.IsChecked = false;
            tlw = null;
          };

          tlw.Left = cfg.u.TlwLeft;
          tlw.Top = cfg.u.TlwTop;
          tlw.Height = cfg.u.TlwHeight;

          tlw.Loaded += delegate { Program.FixWindowLocation(tlw); };

          tlw.Update();
          tlw.Show();
        }
      }
      else if(!cfg.u.ShowTradeLog)
        tlw.Close();
    }

    // **********************************************************************

    void SaveTlwLocation()
    {
      if(tlw != null)
      {
        cfg.u.TlwLeft = tlw.Left;
        cfg.u.TlwTop = tlw.Top;
        cfg.u.TlwHeight = tlw.ActualHeight;
      }
    }

    // **********************************************************************
  }
}
