// ==========================================================================
//    TabClusters.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ==========================================================================

using System;
using System.Windows;
using System.Windows.Controls;

using QScalp.ObjItems;

namespace QScalp.Windows
{
  partial class ConfigWindow
  {
    // **********************************************************************

    ClusterBase lastClusterBase;

    // **********************************************************************

    void InitClusters()
    {
      clusterBase.SelectionChanged += new SelectionChangedEventHandler(clusterBase_SelectionChanged);
      clusterBase.ItemsSource = ClusterBaseItem.GetItems();
      clusterBase.SelectedItem = new ClusterBaseItem(cfg.u.ClusterBase);

      clusterSize.Value = (cfg.u.ClusterBase == ClusterBase.Range)
        ? Price.GetRaw(cfg.u.ClusterSize) : clusterSize.Value = cfg.u.ClusterSize;
      clusterSize.ValueChanged += new EventHandler(clusterSize_ValueChanged);

      clustersCount.Value = cfg.u.Clusters;

      clVolume.IsChecked = (int)(cfg.u.ClusterLegend & ClusterBase.Volume) > 0;
      clTicks.IsChecked = (int)(cfg.u.ClusterLegend & ClusterBase.Ticks) > 0;
      clDelta.IsChecked = (int)(cfg.u.ClusterLegend & ClusterBase.Delta) > 0;
      clRange.IsChecked = (int)(cfg.u.ClusterLegend & ClusterBase.Range) > 0;
      clTime.IsChecked = (int)(cfg.u.ClusterLegend & ClusterBase.Time) > 0;

      clusterView.ItemsSource = ClusterViewItem.GetItems();
      clusterView.SelectedIndex = (int)cfg.u.ClusterView;
      clusterValueFilter.Value = cfg.u.ClusterValueFilter;

      clusterFill.SelectionChanged += new SelectionChangedEventHandler(clusterFill_SelectionChanged);
      clusterFill.ItemsSource = ClusterFillItem.GetItems();
      clusterFill.SelectedIndex = (int)cfg.u.ClusterFill;

      clusterOpacityDelta.Value = cfg.u.ClusterOpacityDelta;

      clusterFillVolume1.Value = cfg.u.ClusterFillVolume1;
      clusterFillVolume1.ValueChanged += new EventHandler(clusterFillVolume1_ValueChanged);

      clusterFillVolume2.Value = cfg.u.ClusterFillVolume2;
      clusterFillVolume2.ValueChanged += new EventHandler(clusterFillVolume2_ValueChanged);

      clusterLegend.SelectionChanged += new SelectionChangedEventHandler(clusterLegend_SelectionChanged);
    }

    // **********************************************************************

    void clusterBase_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if(clusterBase.SelectedItem is ClusterBaseItem)
      {
        ClusterBase ccb = ((ClusterBaseItem)clusterBase.SelectedItem).Value;

        if(ccb != lastClusterBase)
        {
          if(ccb == ClusterBase.Range)
          {
            InitPriceBox(clusterSize);
            clusterSize_ValueChanged(sender, e);
          }
          else if(lastClusterBase == ClusterBase.Range)
          {
            clusterSize.Value *= Math.Pow(10, clusterSize.DecimalPlaces);
            clusterSize.DecimalPlaces = 0;
            clusterSize.Increment = 1;
          }

          lastClusterBase = ccb;
        }
      }
    }

    // **********************************************************************

    void clusterSize_ValueChanged(object sender, EventArgs e)
    {
      if(clusterSize.Value < clusterSize.Increment)
        clusterSize.Value = clusterSize.Increment;
    }

    // **********************************************************************

    void clusterLegend_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      clusterLegend.SelectedIndex = 0;
    }

    // **********************************************************************

    void clusterFill_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      switch((ClusterFill)clusterFill.SelectedIndex)
      {
        case ClusterFill.Double:
          clusterFillVolume1.IsEnabled = true;
          clusterFillVolume2.IsEnabled = true;
          clusterOpacityDelta.IsEnabled = false;
          break;

        case ClusterFill.SingleDelta:
          clusterFillVolume1.IsEnabled = true;
          clusterFillVolume2.IsEnabled = false;
          clusterOpacityDelta.IsEnabled = true;
          break;

        case ClusterFill.SingleBalance:
          clusterFillVolume1.IsEnabled = true;
          clusterFillVolume2.IsEnabled = false;
          clusterOpacityDelta.IsEnabled = false;
          break;
      }
    }

    // **********************************************************************

    void clusterFillVolume1_ValueChanged(object sender, EventArgs e)
    {
      if(clusterFillVolume2.Value < clusterFillVolume1.Value)
        clusterFillVolume2.Value = clusterFillVolume1.Value;
    }

    // **********************************************************************

    void clusterFillVolume2_ValueChanged(object sender, EventArgs e)
    {
      if(clusterFillVolume1.Value > clusterFillVolume2.Value)
        clusterFillVolume1.Value = clusterFillVolume2.Value;
    }

    // **********************************************************************

    void ApplyClusters()
    {
      if(clusterBase.SelectedItem is ClusterBaseItem)
        cfg.u.ClusterBase = ((ClusterBaseItem)clusterBase.SelectedItem).Value;

      if(cfg.u.ClusterBase == ClusterBase.Range)
        cfg.u.ClusterSize = Price.GetInt(clusterSize.Value);
      else
        cfg.u.ClusterSize = (int)clusterSize.Value;

      cfg.u.Clusters = (int)clustersCount.Value;

      cfg.u.ClusterLegend = ClusterBase.None;
      if(clVolume.IsChecked == true) cfg.u.ClusterLegend |= ClusterBase.Volume;
      if(clTicks.IsChecked == true) cfg.u.ClusterLegend |= ClusterBase.Ticks;
      if(clDelta.IsChecked == true) cfg.u.ClusterLegend |= ClusterBase.Delta;
      if(clRange.IsChecked == true) cfg.u.ClusterLegend |= ClusterBase.Range;
      if(clTime.IsChecked == true) cfg.u.ClusterLegend |= ClusterBase.Time;

      ClusterView newClusterView = (ClusterView)clusterView.SelectedIndex;

      if(cfg.u.ClusterView == ClusterView.Separate && newClusterView != ClusterView.Separate)
        cfg.u.ClusterWidth = Math.Round(cfg.u.ClusterWidth / cfg.s.ClusterWidthRatio);
      else if(cfg.u.ClusterView != ClusterView.Separate && newClusterView == ClusterView.Separate)
        cfg.u.ClusterWidth = Math.Round(cfg.u.ClusterWidth * cfg.s.ClusterWidthRatio);

      cfg.u.ClusterView = newClusterView;
      cfg.u.ClusterValueFilter = (int)clusterValueFilter.Value;

      cfg.u.ClusterFill = (ClusterFill)clusterFill.SelectedIndex;
      cfg.u.ClusterFillVolume1 = (int)clusterFillVolume1.Value;
      cfg.u.ClusterFillVolume2 = (int)clusterFillVolume2.Value;
      cfg.u.ClusterOpacityDelta = (int)clusterOpacityDelta.Value;
    }

    // **********************************************************************
  }
}
