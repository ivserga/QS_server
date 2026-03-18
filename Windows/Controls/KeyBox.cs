// =====================================================================
//    KeyBox.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// =====================================================================

using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QScalp.Windows.Controls
{
  class KeyBox : TextBox
  {
    // **********************************************************************

    static LinkedList<KeyBox> kblist;
    static KeyBox() { kblist = new LinkedList<KeyBox>(); }

    // **********************************************************************

    Key value;
    Brush savedBackground;
    int conflicts;

    // **********************************************************************

    public KeyBox() { this.ContextMenu = null; }
    public void Register() { kblist.AddLast(this); }
    public void Unregister() { kblist.Remove(this); }

    // **********************************************************************

    public void Conflicted(Key key1, Key key2, out bool was, out bool now)
    {
      if(key1 == this.value && key1 != Key.None)
      {
        conflicts--;
        was = true;
      }
      else
        was = false;

      if(key2 == this.value && key2 != Key.None)
      {
        conflicts++;
        now = true;
      }
      else
        now = false;

      if(conflicts == 0)
      {
        if(savedBackground != null)
        {
          Background = savedBackground;
          savedBackground = null;
        }
      }
      else
      {
        if(savedBackground == null)
          savedBackground = Background;

        Background = Brushes.Red;
      }
    }

    // **********************************************************************

    public Key Value
    {
      get { return value; }
      set
      {
        if(value < Key.F1 || value > Key.F12)
        {
          foreach(KeyBox kb in kblist)
          {
            if(kb != this)
            {
              bool was, now;
              kb.Conflicted(this.value, value, out was, out now);
              if(was)
                conflicts--;
              if(now)
                conflicts++;
            }
          }

          if(conflicts == 0)
          {
            if(savedBackground != null)
            {
              Background = savedBackground;
              savedBackground = null;
            }
          }
          else
          {
            if(savedBackground == null)
              savedBackground = Background;

            Background = Brushes.Red;
          }

          this.value = value;

          if(value == Key.None)
            Text = "";
          else
            Text = value.ToString();

          CaretIndex = Text.Length;
        }
      }
    }

    // **********************************************************************

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
      Key key = e.Key == Key.System ? e.SystemKey : e.Key;

      if(Value != Key.None)
        switch(key)
        {
          case Key.Back:
            key = Key.None;
            break;

          case Key.Tab:
            base.OnPreviewKeyDown(e);
            return;
        }

      Value = key;
      e.Handled = true;
    }

    // **********************************************************************
  }
}
