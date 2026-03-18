// =============================================================================
//  SecListWindow.xaml.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QScalp.Windows.Config
{
  public partial class SecListWindow : Window
  {
    // **********************************************************************

    public string SecCode { get; protected set; }
    public string ClassCode { get; protected set; }
    public double PriceStep { get; protected set; }

    // **********************************************************************

    class ClassRec
    {
      public readonly string Code;
      public readonly LinkedList<SecRec> SecList;

      public ClassRec(string code)
      {
        Code = code;
        SecList = new LinkedList<SecRec>();
      }
    }

    // **********************************************************************

    class SecRec
    {
      public readonly string Name;
      public readonly string Code;
      public readonly double Step;

      public SecRec(string name, string code, double step)
      {
        Name = name;
        Code = code;
        Step = step;
      }
    }

    // **********************************************************************

    const int ClassNameIndex = 0;
    const int ClassCodeIndex = 1;
    const int SecNameIndex = 2;
    const int SecCodeIndex = 3;
    const int PriceStepIndex = 4;

    // **********************************************************************

    public SecListWindow(string secCode, string classCode)
    {
      InitializeComponent();

      SecCode = string.Empty;
      ClassCode = string.Empty;
      PriceStep = 1;

      try
      {
        SortedDictionary<string, ClassRec> data = new SortedDictionary<string, ClassRec>();

        // ----------------------------------------------------------

        using(StreamReader stream = new StreamReader(cfg.SecFile))
        {
          char[] delimiter = new char[] { ';' };
          string line;

          while((line = stream.ReadLine()) != null)
          {
            string[] str = line.Split(delimiter);
            double step;

            if(str.Length < 5 || !double.TryParse(str[PriceStepIndex],
              NumberStyles.Float, NumberFormatInfo.InvariantInfo, out step))
              throw new FormatException("Неверный формат файла.");

            ClassRec cr;

            if(!data.TryGetValue(str[ClassNameIndex], out cr))
              data.Add(str[ClassNameIndex], cr = new ClassRec(str[ClassCodeIndex]));

            cr.SecList.AddLast(new SecRec(str[SecNameIndex], str[SecCodeIndex], step));
          }
        }

        // ----------------------------------------------------------

        string sep = "    \x2022    ";

        TreeViewItem cClassItem = null;
        TreeViewItem cSecItem = null;

        foreach(KeyValuePair<string, ClassRec> kvp in data)
        {
          TreeViewItem classItem = new TreeViewItem();
          classItem.Header = kvp.Key + sep + kvp.Value.Code;
          classItem.Tag = kvp.Value;
          classItem.Selected += new RoutedEventHandler(classItem_Selected);

          if(cClassItem == null && kvp.Value.Code == classCode)
            cClassItem = classItem;

          foreach(SecRec sr in kvp.Value.SecList)
          {
            TreeViewItem secItem = new TreeViewItem();
            secItem.Header = sr.Name + sep + sr.Code;
            secItem.Tag = sr;
            secItem.Selected += new RoutedEventHandler(secItem_Selected);
            secItem.Unselected += new RoutedEventHandler(secItem_Unselected);
            secItem.MouseDoubleClick += new MouseButtonEventHandler(buttonOk_Click);

            if(sr.Code == secCode && kvp.Value.Code == classCode)
            {
              cClassItem = classItem;
              cSecItem = secItem;
            }

            classItem.Items.Add(secItem);
          }

          treeView.Items.Add(classItem);
        }

        if(cSecItem != null)
        {
          cClassItem.IsExpanded = true;
          cSecItem.IsSelected = true;
        }
        else if(cClassItem != null)
          cClassItem.IsSelected = true;

        // ----------------------------------------------------------
      }
      catch(Exception e)
      {
        TreeViewItem item = new TreeViewItem();
        item.Header = "Ошибка чтения списка инструметов:\n" + e.Message;
        item.IsEnabled = false;
        treeView.Items.Add(item);
      }

      Loaded += new RoutedEventHandler(SecListWindow_Loaded);
    }

    // **********************************************************************

    void SecListWindow_Loaded(object sender, RoutedEventArgs e)
    {
      treeView.Focus();
    }

    // **********************************************************************

    void secItem_Unselected(object sender, RoutedEventArgs e)
    {
      buttonOk.IsEnabled = false;
    }

    // **********************************************************************

    void secItem_Selected(object sender, RoutedEventArgs e)
    {
      SecCode = ((SecRec)((TreeViewItem)sender).Tag).Code;
      PriceStep = ((SecRec)((TreeViewItem)sender).Tag).Step;

      buttonOk.IsEnabled = true;
    }

    // **********************************************************************

    void classItem_Selected(object sender, RoutedEventArgs e)
    {
      ClassCode = ((ClassRec)((TreeViewItem)sender).Tag).Code;
    }

    // **********************************************************************

    private void buttonOk_Click(object sender, RoutedEventArgs e)
    {
      DialogResult = true;
      e.Handled = true;
      this.Close();
    }

    // **********************************************************************
  }
}
