// ===========================================================================
//  GuideWindow.xaml.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// ===========================================================================

using System.Windows;
using System.Windows.Controls;

namespace QScalp.Windows.Config
{
  public partial class GuideWindow : Window
  {
    // **********************************************************************

    public GuideWindow(GuideSource source)
    {
      InitializeComponent();

      secCode.Text = source.SecCode;
      classCode.Text = source.ClassCode;
      priceStep.Value = source.PriceStep;
      wnew.Value = source.Wnew;
      wsrc.Value = source.Wsrc;

      buttonOk.Content = "Обновить";
    }

    // **********************************************************************

    public GuideWindow()
    {
      InitializeComponent();
      buttonOk.Content = "Добавить";
    }

    // **********************************************************************

    public GuideSource GuideSource
    {
      get
      {
        return new GuideSource(
          secCode.Text,
          classCode.Text,
          priceStep.Value,
          wnew.Value,
          wsrc.Value);
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
        priceStep.Value = slw.PriceStep;
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
