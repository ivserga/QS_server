// ==========================================================================
//  MainVM.cs — Главная ViewModel: список тикеров + URL сервера + команды.
// ==========================================================================

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using QScalp.Client.Config;

namespace QScalp.Client.ViewModels
{
    public sealed class MainVM : ViewModelBase
    {
        readonly Dispatcher _dispatcher;
        readonly ClientConfig _config;

        string _serverUrl;
        TickerVM _selected;

        public ObservableCollection<TickerVM> Tickers { get; } = new ObservableCollection<TickerVM>();

        public string ServerUrl
        {
            get => _serverUrl;
            set { if (Set(ref _serverUrl, value)) _config.ServerUrl = value; }
        }

        public TickerVM Selected
        {
            get => _selected;
            set => Set(ref _selected, value);
        }

        public RelayCommand AddTickerCommand { get; }
        public RelayCommand RemoveTickerCommand { get; }
        public RelayCommand StartTickerCommand { get; }
        public RelayCommand StopTickerCommand { get; }
        public RelayCommand StartAllCommand { get; }
        public RelayCommand StopAllCommand { get; }
        public RelayCommand SaveCommand { get; }

        public Func<AddTickerVM> AddTickerDialogFactory { get; set; }

        // ********************************************************************

        public MainVM(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _config = ClientConfig.Load();
            _serverUrl = _config.ServerUrl;

            foreach (var tc in _config.Tickers)
                Tickers.Add(new TickerVM(_serverUrl, tc, _dispatcher));

            Selected = Tickers.FirstOrDefault();

            AddTickerCommand = new RelayCommand(AddTicker);
            RemoveTickerCommand = new RelayCommand(_ => RemoveSelected(), _ => Selected != null);
            StartTickerCommand = new RelayCommand(_ => Selected?.Start(), _ => Selected != null && !Selected.IsRunning);
            StopTickerCommand = new RelayCommand(async _ => { if (Selected != null) await Selected.StopAsync(); },
                                                  _ => Selected != null && Selected.IsRunning);
            StartAllCommand = new RelayCommand(StartAll);
            StopAllCommand = new RelayCommand(async _ => await StopAllAsync());
            SaveCommand = new RelayCommand(_ => SaveConfig());
        }

        // ********************************************************************

        void AddTicker(object _)
        {
            var dlgVM = AddTickerDialogFactory?.Invoke();
            if (dlgVM == null) return;
            if (string.IsNullOrWhiteSpace(dlgVM.Ticker)) return;

            var tc = new TickerConfig
            {
                Ticker = dlgVM.Ticker.Trim().ToUpperInvariant(),
                SecKey = string.IsNullOrWhiteSpace(dlgVM.SecKey) ? null : dlgVM.SecKey.Trim(),
                PriceStep = dlgVM.PriceStep,
                ClusterBase = dlgVM.ClusterBase,
                ClusterSize = dlgVM.ClusterSize,
                HistoryCapacity = dlgVM.HistoryCapacity,
                LogToCsv = dlgVM.LogToCsv,
                ExportClustersToJson = dlgVM.ExportClustersToJson
            };

            if (Tickers.Any(t => string.Equals(t.Ticker, tc.Ticker, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Тикер уже добавлен.", "QScalp.Client",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _config.Tickers.Add(tc);
            var vm = new TickerVM(_serverUrl, tc, _dispatcher);
            Tickers.Add(vm);
            Selected = vm;
        }

        // ********************************************************************

        async void RemoveSelected()
        {
            var vm = Selected;
            if (vm == null) return;

            try { await vm.StopAsync(); } catch { }

            _config.Tickers.RemoveAll(t => string.Equals(t.Ticker, vm.Ticker, StringComparison.OrdinalIgnoreCase));
            Tickers.Remove(vm);
            Selected = Tickers.FirstOrDefault();
        }

        // ********************************************************************

        void StartAll(object _)
        {
            foreach (var t in Tickers)
                if (!t.IsRunning) t.Start();
        }

        async Task StopAllAsync()
        {
            foreach (var t in Tickers)
                if (t.IsRunning) await t.StopAsync();
        }

        // ********************************************************************

        public void SaveConfig()
        {
            try
            {
                foreach (var t in Tickers) t.SyncConfigFromDetectors();
                _config.ServerUrl = _serverUrl;
                _config.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения: " + ex.Message, "QScalp.Client",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ********************************************************************

        public async Task ShutdownAsync()
        {
            try { SaveConfig(); } catch { }
            await StopAllAsync();
        }
    }
}
