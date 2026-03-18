// =======================================================================
//    TabGuide.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =======================================================================

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

    ObservableCollection<GuideSource> guideSources;

    // **********************************************************************

    void InitGuide()
    {
      guideSources = new ObservableCollection<GuideSource>(cfg.u.GuideSources);
      guideSources.CollectionChanged += new NotifyCollectionChangedEventHandler(EnableApplyButton);

      guideList.SelectionChanged += new SelectionChangedEventHandler(guideList_SelectionChanged);
      guideList.ItemsSource = guideSources;

      guideXScale.Value = cfg.u.GuideTickWidth;
      guideYScale.Value = cfg.u.GuideTickHeight;
      guideTickInterval.Value = cfg.u.GuideTickInterval;
    }

    // **********************************************************************

    private void guideList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      buttonEditGuideSrc.IsEnabled =
        buttonDeleteGuideSrc.IsEnabled =
        guideList.SelectedItem != null;
    }

    // **********************************************************************

    private void buttonAddGuideSrc_Click(object sender, RoutedEventArgs e)
    {
      GuideWindow gw = new GuideWindow();
      gw.Owner = this;

      if(gw.ShowDialog() == true)
      {
        guideSources.Add(gw.GuideSource);
        guideList.SelectedIndex = guideSources.Count - 1;

        guideList.ScrollIntoView(guideList.SelectedItem);
      }
    }

    // **********************************************************************

    private void buttonEditGuideSrc_Click(object sender, RoutedEventArgs e)
    {
      if(guideList.SelectedIndex >= 0)
      {
        GuideWindow gw = new GuideWindow((GuideSource)guideList.SelectedItem);
        gw.Owner = this;

        int i = guideList.SelectedIndex;

        if(gw.ShowDialog() == true)
        {
          guideSources[i] = gw.GuideSource;
          guideList.Items.Refresh();

          guideList.SelectedIndex = i;
          guideList.ScrollIntoView(guideList.SelectedItem);
        }
      }
    }

    // **********************************************************************

    private void buttonDeleteGuideSrc_Click(object sender, RoutedEventArgs e)
    {
      if(guideList.SelectedIndex >= 0)
        guideSources.RemoveAt(guideList.SelectedIndex);
    }

    // **********************************************************************

    void ApplyGuide()
    {
      cfg.u.GuideTickWidth = guideXScale.Value;
      cfg.u.GuideTickHeight = guideYScale.Value;
      cfg.u.GuideTickInterval = (int)guideTickInterval.Value;

      cfg.u.GuideSources = new GuideSource[guideSources.Count];
      guideSources.CopyTo(cfg.u.GuideSources, 0);
    }

    // **********************************************************************
  }
}
