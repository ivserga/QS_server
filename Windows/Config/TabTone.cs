// ======================================================================
//    TabTone.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ======================================================================

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

using QScalp.Windows.Config;

namespace QScalp.Windows
{
  partial class ConfigWindow
  {
    // **********************************************************************

    ObservableCollection<ToneSource> toneSources;

    // **********************************************************************

    void InitTone()
    {
      toneSources = new ObservableCollection<ToneSource>(cfg.u.ToneSources);
      toneSources.CollectionChanged += new NotifyCollectionChangedEventHandler(EnableApplyButton);

      toneList.SelectionChanged += new SelectionChangedEventHandler(toneList_SelectionChanged);
      toneList.ItemsSource = toneSources;

      toneMeterHeight.Value = cfg.u.ToneMeterHeight;
    }

    // **********************************************************************

    private void toneList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      buttonEditToneSrc.IsEnabled =
        buttonDeleteToneSrc.IsEnabled =
        buttonUpToneSrc.IsEnabled =
        buttonDownToneSrc.IsEnabled =
        toneList.SelectedItem != null;
    }

    // **********************************************************************

    private void buttonAddToneSrc_Click(object sender, RoutedEventArgs e)
    {
      ToneWindow tw = new ToneWindow();
      tw.Owner = this;

      if(tw.ShowDialog() == true)
      {
        toneSources.Add(tw.ToneSource);
        toneList.SelectedIndex = toneSources.Count - 1;
        toneList.ScrollIntoView(toneList.SelectedItem);
      }
    }

    // **********************************************************************

    private void buttonEditToneSrc_Click(object sender, RoutedEventArgs e)
    {
      if(toneList.SelectedIndex >= 0)
      {
        ToneWindow tw = new ToneWindow((ToneSource)toneList.SelectedItem);
        tw.Owner = this;

        int i = toneList.SelectedIndex;

        if(tw.ShowDialog() == true)
        {
          toneSources[i] = tw.ToneSource;
          toneList.Items.Refresh();

          toneList.SelectedIndex = i;
          toneList.ScrollIntoView(toneList.SelectedItem);
        }
      }
    }

    // **********************************************************************

    private void buttonDeleteToneSrc_Click(object sender, RoutedEventArgs e)
    {
      if(toneList.SelectedIndex >= 0)
        toneSources.RemoveAt(toneList.SelectedIndex);
    }

    // **********************************************************************

    private void buttonUpToneSrc_Click(object sender, RoutedEventArgs e)
    {
      if(toneList.SelectedIndex >= 1)
      {
        toneSources.Move(toneList.SelectedIndex, toneList.SelectedIndex - 1);
        toneList.ScrollIntoView(toneList.SelectedItem);
      }
    }

    // **********************************************************************

    private void buttonDownToneSrc_Click(object sender, RoutedEventArgs e)
    {
      if(toneList.SelectedIndex < toneSources.Count - 1)
      {
        toneSources.Move(toneList.SelectedIndex, toneList.SelectedIndex + 1);
        toneList.ScrollIntoView(toneList.SelectedItem);
      }
    }

    // **********************************************************************

    void ApplyTone()
    {
      cfg.u.ToneMeterHeight = toneMeterHeight.Value;

      cfg.u.ToneSources = new ToneSource[toneSources.Count];
      toneSources.CopyTo(cfg.u.ToneSources, 0);
    }

    // **********************************************************************
  }
}
