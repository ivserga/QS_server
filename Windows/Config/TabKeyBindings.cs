// =========================================================================
//  TabKeyBindings.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =========================================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

using QScalp.ObjItems;
using QScalp.Windows.Config;
using QScalp.XMedia;

namespace QScalp.Windows
{
  partial class ConfigWindow
  {
    // **********************************************************************

    ObservableCollection<KbItem> kbList;
    ListCollectionView kbView;

    // **********************************************************************

    void LoadKeyBindings(KeyBindings kb, bool onKeyDown)
    {
      foreach(KeyValuePair<Key, OwnAction[]> kvp in kb)
      {
        int id = 0;
        foreach(OwnAction a in kvp.Value)
          kbList.Add(new KbItem(kvp.Key, onKeyDown, a, id++, cfg.u.PriceRatio));
      }
    }

    // **********************************************************************

    void InitKeyBindings()
    {
      kbList = new ObservableCollection<KbItem>();

      LoadKeyBindings(cfg.u.KeyDownBindings, true);
      LoadKeyBindings(cfg.u.KeyUpBindings, false);

      kbView = new ListCollectionView(kbList);

      kbView.SortDescriptions.Add(new SortDescription("KeyEvent", ListSortDirection.Ascending));
      kbView.SortDescriptions.Add(new SortDescription("Id", ListSortDirection.Ascending));
      kbView.GroupDescriptions.Add(new PropertyGroupDescription("KeyEvent"));

      keyBindings.ItemsSource = kbView;

      keyBindings.SelectedItem = null;

      kbList.CollectionChanged += new NotifyCollectionChangedEventHandler(EnableApplyButton);
      keyBindings.SelectionChanged += new SelectionChangedEventHandler(UpdateKeyBindingsButtons);
    }

    // **********************************************************************

    void UpdateKeyBindingsButtons(object sender, EventArgs e)
    {
      if(keyBindings.SelectedIndex >= 0)
      {
        buttonEditKb.IsEnabled = buttonDeleteKb.IsEnabled = true;

        KbItem curr = (KbItem)kbView.GetItemAt(keyBindings.SelectedIndex);

        if(keyBindings.SelectedIndex > 0)
        {
          KbItem prev = (KbItem)kbView.GetItemAt(keyBindings.SelectedIndex - 1);
          buttonUpKb.IsEnabled = curr.Key == prev.Key && curr.OnKeyDown == prev.OnKeyDown;
        }
        else
          buttonUpKb.IsEnabled = false;

        if(keyBindings.SelectedIndex + 1 < kbView.Count)
        {
          KbItem next = (KbItem)kbView.GetItemAt(keyBindings.SelectedIndex + 1);
          buttonDownKb.IsEnabled = curr.Key == next.Key && curr.OnKeyDown == next.OnKeyDown;
        }
        else
          buttonDownKb.IsEnabled = false;
      }
      else
        buttonEditKb.IsEnabled =
          buttonDeleteKb.IsEnabled =
          buttonUpKb.IsEnabled =
          buttonDownKb.IsEnabled = false;
    }

    // **********************************************************************

    void SelectKeyBinding(object item)
    {
      keyBindings.SelectedItem = item;

      keyBindings.Dispatcher.BeginInvoke(DispatcherPriority.Background,
        (Action)delegate { keyBindings.ScrollIntoView(item); });
    }

    // **********************************************************************

    private void buttonAddKb_Click(object sender, RoutedEventArgs e)
    {
      KbWindow kbw = new KbWindow(keyBindings.SelectedItem as KbItem, true, priceStep);

      kbw.Owner = this;

      if(kbw.ShowDialog() == true)
      {
        foreach(KbItem kbItem in kbView)
          if(kbItem.Key == kbw.KbItem.Key
            && kbItem.OnKeyDown == kbw.KbItem.OnKeyDown
            && kbItem.Id >= kbw.KbItem.Id)
            kbw.KbItem.Id = kbItem.Id + 1;

        kbList.Add(kbw.KbItem);
        SelectKeyBinding(kbw.KbItem);
      }
    }

    // **********************************************************************

    private void buttonEditKb_Click(object sender, RoutedEventArgs e)
    {
      KbItem kbItem = keyBindings.SelectedItem as KbItem;

      if(kbItem != null)
      {
        KbWindow kbw = new KbWindow(kbItem, false, priceStep);

        kbw.Owner = this;

        if(kbw.ShowDialog() == true)
        {
          SelectKeyBinding(kbw.KbItem);
          EnableApplyButton(this, e);
        }
      }
    }

    // **********************************************************************

    private void buttonDeleteKb_Click(object sender, RoutedEventArgs e)
    {
      if(keyBindings.SelectedIndex >= 0)
      {
        kbView.RemoveAt(keyBindings.SelectedIndex);
        SelectKeyBinding(keyBindings.SelectedItem);
      }

      UpdateKeyBindingsButtons(sender, EventArgs.Empty);
    }

    // **********************************************************************

    private void buttonUpKb_Click(object sender, RoutedEventArgs e)
    {
      if(keyBindings.SelectedIndex > 0)
      {
        KbItem curr = (KbItem)kbView.GetItemAt(keyBindings.SelectedIndex);
        KbItem prev = (KbItem)kbView.GetItemAt(keyBindings.SelectedIndex - 1);

        if(curr.Key == prev.Key && curr.OnKeyDown == prev.OnKeyDown)
        {
          int id = curr.Id;

          kbView.EditItem(curr);
          kbView.EditItem(prev);

          curr.Id = prev.Id;
          prev.Id = id;

          kbView.CommitEdit();

          SelectKeyBinding(curr);
          EnableApplyButton(this, e);
        }
      }

      UpdateKeyBindingsButtons(sender, EventArgs.Empty);
    }

    // **********************************************************************

    private void buttonDownKb_Click(object sender, RoutedEventArgs e)
    {
      if(keyBindings.SelectedIndex >= 0 && keyBindings.SelectedIndex + 1 < kbView.Count)
      {
        KbItem curr = (KbItem)kbView.GetItemAt(keyBindings.SelectedIndex);
        KbItem next = (KbItem)kbView.GetItemAt(keyBindings.SelectedIndex + 1);

        if(curr.Key == next.Key && curr.OnKeyDown == next.OnKeyDown)
        {
          int id = curr.Id;

          kbView.EditItem(curr);
          kbView.EditItem(next);

          curr.Id = next.Id;
          next.Id = id;

          kbView.CommitEdit();

          SelectKeyBinding(curr);
          EnableApplyButton(this, e);
        }
      }

      UpdateKeyBindingsButtons(sender, EventArgs.Empty);
    }

    // **********************************************************************

    void ApplyKeyBindings()
    {
      Dictionary<Key, List<OwnAction>> keyDownBindings = new Dictionary<Key, List<OwnAction>>();
      Dictionary<Key, List<OwnAction>> keyUpBindings = new Dictionary<Key, List<OwnAction>>();

      Dictionary<Key, List<OwnAction>> bindings;
      List<OwnAction> actions;

      foreach(KbItem kbItem in kbView)
      {
        bindings = kbItem.OnKeyDown ? keyDownBindings : keyUpBindings;

        if(!bindings.TryGetValue(kbItem.Key, out actions))
          bindings.Add(kbItem.Key, actions = new List<OwnAction>());

        actions.Add(kbItem.Action);
      }

      cfg.u.KeyDownBindings = new KeyBindings();
      cfg.u.KeyUpBindings = new KeyBindings();

      foreach(KeyValuePair<Key, List<OwnAction>> kvp in keyDownBindings)
        cfg.u.KeyDownBindings.Add(kvp.Key, kvp.Value.ToArray());

      foreach(KeyValuePair<Key, List<OwnAction>> kvp in keyUpBindings)
        cfg.u.KeyUpBindings.Add(kvp.Key, kvp.Value.ToArray());
    }

    // **********************************************************************
  }
}
