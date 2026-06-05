using System.ComponentModel;
using System.Windows;

using QScalp.Client.ViewModels;

namespace QScalp.Client.Views
{
    public partial class MainWindow : Window
    {
        readonly MainVM _vm;

        public MainWindow()
        {
            InitializeComponent();

            _vm = new MainVM(Dispatcher);
            _vm.AddTickerDialogFactory = ShowAddTickerDialog;

            DataContext = _vm;
        }

        // ********************************************************************

        AddTickerVM ShowAddTickerDialog()
        {
            var vm = new AddTickerVM();
            var dlg = new AddTickerDialog
            {
                Owner = this,
                DataContext = vm
            };
            return dlg.ShowDialog() == true ? vm : null;
        }

        // ********************************************************************

        protected override void OnClosing(CancelEventArgs e)
        {
            _vm.ShutdownAsync().GetAwaiter().GetResult();
            base.OnClosing(e);
        }
    }
}
