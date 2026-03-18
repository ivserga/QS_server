// ==========================================================================
//  ToneWindow.xaml.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// ==========================================================================

using System.Windows;
using System.Windows.Controls;

namespace QScalp.Windows.Config
{
  public partial class ToneWindow : Window
  {
    // **********************************************************************

    public ToneWindow(ToneSource source)
    {
      InitializeComponent();

      secCode.Text = source.SecCode;
      classCode.Text = source.ClassCode;
      interval.Value = source.Interval;
      fillVolume.Value = source.FillVolume;

      buttonOk.Content = "Обновить";
    }

    // **********************************************************************

    public ToneWindow()
    {
      InitializeComponent();
      buttonOk.Content = "Добавить";
    }

    // **********************************************************************

    public ToneSource ToneSource
    {
      get
      {
        return new ToneSource(
          secCode.Text,
          classCode.Text,
          (int)interval.Value,
          (int)fillVolume.Value);
      }
    }

    // **********************************************************************

    private void SecCodeChanged(object sender, TextChangedEventArgs e)
    {
      buttonOk.IsEnabled = secCode.Text.Length > 0;
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
      }

      e.Handled = true;
    }

    // **********************************************************************

    private void buttonOk_Click(object sender, RoutedEventArgs e)
    {
      DialogResult = true;
      this.Close();

      e.Handled = true;
    }

    // **********************************************************************
  }
}
