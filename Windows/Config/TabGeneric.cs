// =========================================================================
//    TabGeneric.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =========================================================================

using System;
using System.Windows;

using QScalp.Windows.Config;

namespace QScalp.Windows
{
  partial class ConfigWindow
  {
    // **********************************************************************

    void InitGeneric()
    {
      quikFolder.Text = cfg.u.QuikFolder;

      account.Text = cfg.u.QuikAccount;
      clientCode.Text = cfg.u.QuikClientCode;
      clientCode.MaxLength = QuikIO.QuikTerminal.ClientCodeMaxLength;
      clientCode.IsEnabled = cfg.u.QuikClientCode.Length > 0;
      clientCodeLabel.MouseDoubleClick += delegate
      {
        if(!clientCode.IsEnabled)
        {
          clientCode.IsEnabled = true;
          clientCode.Focus();
        }
      };

      secCode.Text = cfg.u.SecCode;
      classCode.Text = cfg.u.ClassCode;

      // [ctor] priceStep.Set(cfg.u.PriceRatio, cfg.u.PriceStep);
      priceStep.ValueChanged += new EventHandler(priceStep_ValueChanged);

      fullVolume.Value = cfg.u.FullVolume;

      grid1.Value = Price.GetRaw(cfg.u.Grid1Step);
      grid1.ValueChanged += new EventHandler(grid1_ValueChanged);

      grid2.Value = Price.GetRaw(cfg.u.Grid2Step);
      grid2.ValueChanged += new EventHandler(grid2_ValueChanged);

      windowTopMost.IsChecked = cfg.u.WindowTopmost;
      confirmExit.IsChecked = cfg.u.ConfirmExit;
    }

    // **********************************************************************

    void priceStep_ValueChanged(object sender, EventArgs e)
    {
      TunePriceControls();

      grid1.Value = priceStep.Value * 10;
      grid2.Value = priceStep.Value * 100;
    }

    // **********************************************************************

    void grid1_ValueChanged(object sender, EventArgs e)
    {
      if(grid1.Value < priceStep.Value)
        grid1.Value = grid1.Increment;

      if(grid2.Value < grid1.Value)
        grid2.Value = grid1.Value;
    }

    // **********************************************************************

    void grid2_ValueChanged(object sender, EventArgs e)
    {
      if(grid2.Value < priceStep.Value)
        grid2.Value = grid2.Increment;

      if(grid1.Value > grid2.Value)
        grid1.Value = grid2.Value;
    }

    // **********************************************************************

    private void buttonFolderSelect_Click(object sender, RoutedEventArgs e)
    {
      using(System.Windows.Forms.FolderBrowserDialog fb =
        new System.Windows.Forms.FolderBrowserDialog())
      {
        fb.ShowNewFolderButton = false;
        fb.RootFolder = Environment.SpecialFolder.MyComputer;
        fb.SelectedPath = quikFolder.Text;
        fb.Description = "Рабочий путь терминала QUIK";
        fb.ShowDialog();

        quikFolder.Text = fb.SelectedPath;
      }
    }

    // **********************************************************************

    private void buttonSecSelect_Click(object sender, RoutedEventArgs e)
    {
      SecListWindow slw = new SecListWindow(secCode.Text, classCode.Text);
      slw.Owner = this;

      if(slw.ShowDialog() == true)
      {
        secCode.Text = slw.SecCode;
        classCode.Text = slw.ClassCode;
        priceStep.Value = slw.PriceStep;
      }
    }

    // **********************************************************************

    void ApplyGeneric()
    {
      cfg.u.QuikFolder = quikFolder.Text;

      cfg.u.QuikAccount = account.Text;
      cfg.u.QuikClientCode = clientCode.Text;

      cfg.u.SecCode = secCode.Text;
      cfg.u.ClassCode = classCode.Text;
      cfg.u.PriceRatio = priceStep.Ratio;
      cfg.u.PriceStep = priceStep.Step;

      cfg.u.FullVolume = (int)fullVolume.Value;

      cfg.u.Grid1Step = Price.GetInt(grid1.Value);
      cfg.u.Grid2Step = Price.GetInt(grid2.Value);

      cfg.u.WindowTopmost = windowTopMost.IsChecked == true;
      cfg.u.ConfirmExit = confirmExit.IsChecked == true;
    }

    // **********************************************************************
  }
}
