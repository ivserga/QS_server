using System;
using System.Windows;
using QScalp.Server.Broadcasting;
using QScalp.Server.Config;

namespace QScalp.Server
{
    public partial class MainWindow : Window
    {
        private ServerConfig _config;
        private ServerEngine _engine;

        // ********************************************************************

        public MainWindow()
        {
            InitializeComponent();
            // Подписка после Init: иначе SelectionChanged при IsSelected в XAML
            // срабатывает до создания tbStrikerBaseUrl / tbApiBaseUrl и падает с NullReferenceException.
            cbDataSource.SelectionChanged += CbDataSource_SelectionChanged;

            _config = ServerConfig.Load();
            LoadConfigToUI();
        }

        // ********************************************************************

        private void LoadConfigToUI()
        {
            SelectDataSourceInCombo(_config.DataSource);
            tbStrikerBaseUrl.Text = string.IsNullOrWhiteSpace(_config.StrikerHttpBaseUrl)
                ? "http://127.0.0.1:16239"
                : _config.StrikerHttpBaseUrl;
            tbStrikerPollMs.Text = (_config.StrikerQuotePollMs > 0 ? _config.StrikerQuotePollMs : 500).ToString();
            cbStrikerTimeSales.IsChecked = _config.StrikerEnableTimeAndSales;

            tbStrikerWsUrl.Text = string.IsNullOrWhiteSpace(_config.StrikerWsBaseUrl)
                ? "ws://127.0.0.1:9700"
                : _config.StrikerWsBaseUrl;
            tbStrikerWsPwd.Password = _config.StrikerWsPassword ?? string.Empty;
            tbStrikerWsStreamer.Text = _config.StrikerWsStreamerId ?? string.Empty;

            tbApiBaseUrl.Text = _config.ApiBaseUrl;
            tbWsBaseUrl.Text = _config.WsBaseUrl;
            tbApiKey.Password = _config.ApiKey;
            tbListenUrl.Text = _config.ListenUrl;
            tbMaxClients.Text = _config.MaxClients.ToString();
            cbDebugMode.IsChecked = _config.DebugMode;
            tbReplaySessionPath.Text = _config.ReplaySessionPath ?? string.Empty;
            tbReplaySpeed.Text = (_config.ReplaySpeed > 0 ? _config.ReplaySpeed : 1.0).ToString("0.##");
            cbReplayLoop.IsChecked = _config.ReplayLoop;
            ApplyDataSourceUiState();
        }

        private void SelectDataSourceInCombo(string dataSource)
        {
            string want;
            var t = dataSource?.Trim();
            if (string.Equals(t, "Replay", StringComparison.OrdinalIgnoreCase))
                want = "Replay";
            else if (string.Equals(t, "StrikerWs", StringComparison.OrdinalIgnoreCase))
                want = "StrikerWs";
            else if (string.Equals(t, "Striker", StringComparison.OrdinalIgnoreCase))
                want = "Striker";
            else
                want = "Massive";

            foreach (System.Windows.Controls.ComboBoxItem item in cbDataSource.Items)
            {
                if (item.Tag as string == want)
                {
                    cbDataSource.SelectedItem = item;
                    return;
                }
            }
            cbDataSource.SelectedIndex = 0;
        }

        private string GetSelectedDataSource()
        {
            if (cbDataSource.SelectedItem is System.Windows.Controls.ComboBoxItem sel &&
                sel.Tag is string tag)
                return tag;
            return "Massive";
        }

        private void ApplyDataSourceUiState()
        {
            if (tbStrikerBaseUrl == null || tbApiBaseUrl == null || tbStrikerWsUrl == null)
                return;

            var src = GetSelectedDataSource();
            var replay = string.Equals(src, "Replay", StringComparison.OrdinalIgnoreCase);
            var striker = string.Equals(src, "Striker", StringComparison.OrdinalIgnoreCase);
            var strikerWs = string.Equals(src, "StrikerWs", StringComparison.OrdinalIgnoreCase);
            var massive = !striker && !strikerWs && !replay;

            tbStrikerBaseUrl.IsEnabled = striker;
            tbStrikerPollMs.IsEnabled = striker;
            cbStrikerTimeSales.IsEnabled = striker;

            tbStrikerWsUrl.IsEnabled = strikerWs;
            tbStrikerWsPwd.IsEnabled = strikerWs;
            tbStrikerWsStreamer.IsEnabled = strikerWs;

            tbApiBaseUrl.IsEnabled = massive;
            tbWsBaseUrl.IsEnabled = massive;
            tbApiKey.IsEnabled = massive;

            tbReplaySessionPath.IsEnabled = replay;
            tbReplaySpeed.IsEnabled = replay;
            cbReplayLoop.IsEnabled = replay;
        }

        private void CbDataSource_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cbDataSource == null) return;
            ApplyDataSourceUiState();
        }

        private void SaveConfigFromUI()
        {
            _config.DataSource = GetSelectedDataSource();
            _config.StrikerHttpBaseUrl = tbStrikerBaseUrl.Text.Trim();
            if (int.TryParse(tbStrikerPollMs.Text, out int spoll) && spoll >= 50)
                _config.StrikerQuotePollMs = spoll;
            else
                _config.StrikerQuotePollMs = 500;
            _config.StrikerEnableTimeAndSales = cbStrikerTimeSales.IsChecked == true;

            _config.StrikerWsBaseUrl = tbStrikerWsUrl.Text.Trim();
            _config.StrikerWsPassword = tbStrikerWsPwd.Password ?? string.Empty;
            _config.StrikerWsStreamerId = tbStrikerWsStreamer.Text.Trim();

            _config.ApiBaseUrl = tbApiBaseUrl.Text.Trim();
            _config.WsBaseUrl = tbWsBaseUrl.Text.Trim();
            _config.ApiKey = tbApiKey.Password;
            _config.ListenUrl = tbListenUrl.Text.Trim();
            int.TryParse(tbMaxClients.Text, out int maxClients);
            _config.MaxClients = maxClients > 0 ? maxClients : 10;
            _config.DebugMode = cbDebugMode.IsChecked == true;
            _config.ReplaySessionPath = tbReplaySessionPath.Text.Trim();
            if (double.TryParse(tbReplaySpeed.Text.Replace(',', '.'),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double rs) && rs > 0)
                _config.ReplaySpeed = rs;
            else
                _config.ReplaySpeed = 1.0;
            _config.ReplayLoop = cbReplayLoop.IsChecked == true;
            _config.Save();
        }

        // ********************************************************************

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            SaveConfigFromUI();

            if (string.Equals(_config.DataSource, "Replay", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(_config.ReplaySessionPath) ||
                    (!System.IO.Directory.Exists(_config.ReplaySessionPath) &&
                     !System.IO.File.Exists(_config.ReplaySessionPath)))
                {
                    MessageBox.Show(
                        "Укажите существующую папку сессии (с файлами TICKER.jsonl) или один .jsonl файл.",
                        "QScalp Server",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else if (string.Equals(_config.DataSource, "StrikerWs", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(_config.StrikerWsBaseUrl))
                {
                    MessageBox.Show("Укажите Striker WS URL (например ws://127.0.0.1:9700).", "QScalp Server",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrEmpty(_config.StrikerWsPassword))
                {
                    MessageBox.Show("Введите пароль Medved Trader / Striker для WS connect.", "QScalp Server",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else if (string.Equals(_config.DataSource, "Striker", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(_config.StrikerHttpBaseUrl))
                {
                    MessageBox.Show("Укажите базовый URL Striker (например http://127.0.0.1:16239).", "QScalp Server",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else if (string.IsNullOrEmpty(_config.ApiKey))
            {
                MessageBox.Show("Введите API Key (режим Massive).", "QScalp Server",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetRunningState(true);
            Log("Запуск сервера...");

            try
            {
                _engine = new ServerEngine(_config, Log, UpdateClientList);
                await _engine.StartAsync();
                Log($"Сервер запущен. Ожидание клиентов на {_config.ListenUrl}");
            }
            catch (Exception ex)
            {
                Log($"ОШИБКА запуска: {ex.Message}");
                SetRunningState(false);
            }
        }

        // ********************************************************************

        private async void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            Log("Остановка сервера...");
            try
            {
                if (_engine != null)
                {
                    await _engine.StopAsync();
                    _engine = null;
                }
                Log("Сервер остановлен.");
            }
            catch (Exception ex)
            {
                Log($"Ошибка остановки: {ex.Message}");
            }
            SetRunningState(false);
        }

        // ********************************************************************

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveConfigFromUI();
            if (_engine != null)
            {
                _engine.StopAsync().Wait(TimeSpan.FromSeconds(5));
            }
        }

        // ********************************************************************

        private void SetRunningState(bool running)
        {
            btnStart.IsEnabled = !running;
            btnStop.IsEnabled = running;
            cbDataSource.IsEnabled = !running;

            var src = GetSelectedDataSource();
            var isReplay = string.Equals(src, "Replay", StringComparison.OrdinalIgnoreCase);
            var isStriker = string.Equals(src, "Striker", StringComparison.OrdinalIgnoreCase);
            var isStrikerWs = string.Equals(src, "StrikerWs", StringComparison.OrdinalIgnoreCase);
            var isMassive = !isStriker && !isStrikerWs && !isReplay;

            tbStrikerBaseUrl.IsEnabled = !running && isStriker;
            tbStrikerPollMs.IsEnabled = !running && isStriker;
            cbStrikerTimeSales.IsEnabled = !running && isStriker;

            tbStrikerWsUrl.IsEnabled = !running && isStrikerWs;
            tbStrikerWsPwd.IsEnabled = !running && isStrikerWs;
            tbStrikerWsStreamer.IsEnabled = !running && isStrikerWs;

            tbApiBaseUrl.IsEnabled = !running && isMassive;
            tbWsBaseUrl.IsEnabled = !running && isMassive;
            tbApiKey.IsEnabled = !running && isMassive;

            tbListenUrl.IsEnabled = !running;
            tbMaxClients.IsEnabled = !running;
            cbDebugMode.IsEnabled = !running;
            tbReplaySessionPath.IsEnabled = !running && isReplay;
            tbReplaySpeed.IsEnabled = !running && isReplay;
            cbReplayLoop.IsEnabled = !running && isReplay;
        }

        // ********************************************************************

        private void Log(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<string>(Log), message);
                return;
            }

            var line = $"{DateTime.Now:HH:mm:ss}  {message}\r\n";
            tbLog.AppendText(line);
            tbLog.ScrollToEnd();
        }

        // ********************************************************************

        private void UpdateClientList(ClientInfo[] clients)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<ClientInfo[]>(UpdateClientList), new object[] { clients });
                return;
            }

            lvClients.ItemsSource = clients;
        }
    }
}
