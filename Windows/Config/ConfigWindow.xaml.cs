// ============================================================================
//  ConfigWindow.xaml.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ============================================================================

using System;
using System.Windows;
using System.Windows.Controls;

using QScalp.ObjItems;
using QScalp.Windows.Controls;

namespace QScalp.Windows
{
  public partial class ConfigWindow : Window
  {
    // **********************************************************************

    public UserSettings35 SavedSettings { get; protected set; }

    public event EventHandler ApplyChanges;

    // **********************************************************************

    public ConfigWindow()
    {
      InitializeComponent();
      Title = "Настройки " + cfg.ProgName;

      SavedSettings = cfg.u;

      // ------------------------------------------------------------

      priceStep.Set(cfg.u.PriceRatio, cfg.u.PriceStep);
      TunePriceControls();

      // ------------------------------------------------------------

      InitGeneric();
      InitGraph();
      InitGuide();
      InitTone();
      InitClusters();
      InitKeyBindings();
      InitManagement();
      InitPosition();
      InitOther();

      // ------------------------------------------------------------

      ProcessChildren(this);
    }

    // **********************************************************************

    bool ProcessChildren(FrameworkElement fe)
    {
      bool processed = false;

      if(fe != null)
        foreach(object obj in LogicalTreeHelper.GetChildren(fe))
        {
          if(obj is TextBox)
          {
            ((TextBox)obj).TextChanged += new TextChangedEventHandler(EnableApplyButton);
            processed = true;
          }
          //else if(obj is NumUpDownBase)
          //{
          //  ((NumUpDownBase)obj).ValueChanged += new EventHandler(EnableApplyButton);
          //  processed = true;
          //}
          else if(obj is CheckBox)
          {
            ((CheckBox)obj).Checked += new RoutedEventHandler(EnableApplyButton);
            ((CheckBox)obj).Unchecked += new RoutedEventHandler(EnableApplyButton);

            processed = true;
          }
          else if(obj is ComboBox)
          {
            if(!ProcessChildren(obj as FrameworkElement))
              ((ComboBox)obj).SelectionChanged += new SelectionChangedEventHandler(EnableApplyButton);

            processed = true;
          }
          else
            processed |= ProcessChildren(obj as FrameworkElement);
        }

      return processed;
    }

    // **********************************************************************

    void TunePriceControls()
    {
      grid1.DecimalPlaces = priceStep.DecimalPlaces;
      grid1.Increment = priceStep.Value * 10;

      grid2.DecimalPlaces = priceStep.DecimalPlaces;
      grid2.Increment = priceStep.Value * 100;

      if(kbList != null)
        foreach(KbItem kb in kbList)
          kb.PriceRatio = priceStep.Ratio;

      InitPriceBox(mouseSlippage);
      InitPriceBox(stopOffset);
      InitPriceBox(stopSlippage);
      InitPriceBox(takeOffset);

      if(clusterBase.SelectedItem is ClusterBaseItem &&
        ((ClusterBaseItem)clusterBase.SelectedItem).Value == ClusterBase.Range)
      {
        InitPriceBox(clusterSize);
        clusterSize_ValueChanged(this, EventArgs.Empty);
      }
    }

    // **********************************************************************

    void InitPriceBox(NumUpDown priceBox)
    {
      double v = priceBox.Value * Math.Pow(10, priceBox.DecimalPlaces)
        / Math.Pow(10, priceStep.DecimalPlaces);

      priceBox.DecimalPlaces = priceStep.DecimalPlaces;
      priceBox.Increment = priceStep.Value;
      priceBox.Value = v;
    }

    // **********************************************************************

    void EnableApplyButton(object sender, EventArgs e)
    {
      if(IsLoaded)
        buttonApply.IsEnabled = true;
    }

    // **********************************************************************

    private void buttonOk_Click(object sender, RoutedEventArgs e)
    {
      this.Hide();
      buttonApply_Click(sender, e);
      this.Close();
    }

    // **********************************************************************

    private void buttonCancel_Click(object sender, RoutedEventArgs e)
    {
      this.Close();
    }

    // **********************************************************************

    private void buttonApply_Click(object sender, RoutedEventArgs e)
    {
      buttonApply.IsEnabled = false;

      SavedSettings = cfg.u.Clone();

      ApplyGeneric();
      ApplyGraph();
      ApplyGuide();
      ApplyTone();
      ApplyClusters();
      ApplyKeyBindings();
      ApplyManagement();
      ApplyPosition();
      ApplyOther();

      cfg.Reinit();

      if(ApplyChanges != null)
        ApplyChanges(this, e);
    }

    // **********************************************************************

    public void ShowKeyBindings() { tabKeyBindings.Focus(); }
    public void ShowPosition() { tabPosition.Focus(); }

    // **********************************************************************
  }
}
