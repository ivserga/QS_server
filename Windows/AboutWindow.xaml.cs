// ===========================================================================
//  AboutWindow.xaml.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// ===========================================================================

using System.Diagnostics;
using System.Windows;

namespace QScalp.Windows
{
  public partial class AboutWindow : Window
  {
    public AboutWindow() { InitializeComponent(); }

    private void SiteURL_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        Process.Start("http://www.moroshkin.com");
      }
      catch
      {
        MessageBox.Show(this,
          "Ошибка открытия ссылки",
          cfg.ProgName,
          MessageBoxButton.OK,
          MessageBoxImage.Exclamation);
      }
    }
  }
}
