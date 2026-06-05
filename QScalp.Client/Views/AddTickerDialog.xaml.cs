using System.Windows;

using QScalp.Client.ViewModels;

namespace QScalp.Client.Views
{
    public partial class AddTickerDialog : Window
    {
        public AddTickerDialog()
        {
            InitializeComponent();
        }

        void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        void OnOk(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is AddTickerVM vm)) { DialogResult = false; return; }

            if (string.IsNullOrWhiteSpace(vm.Ticker))
            {
                MessageBox.Show(this, "Укажите тикер.", "QScalp.Client",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
        }
    }
}
