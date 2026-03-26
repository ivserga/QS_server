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

            _config = ServerConfig.Load();
            LoadConfigToUI();
        }

        // ********************************************************************

        private void LoadConfigToUI()
        {
            tbApiBaseUrl.Text = _config.ApiBaseUrl;
            tbWsBaseUrl.Text = _config.WsBaseUrl;
            tbApiKey.Password = _config.ApiKey;
            tbListenUrl.Text = _config.ListenUrl;
            tbMaxClients.Text = _config.MaxClients.ToString();
            cbDebugMode.IsChecked = _config.DebugMode;
        }

        private void SaveConfigFromUI()
        {
            _config.ApiBaseUrl = tbApiBaseUrl.Text.Trim();
            _config.WsBaseUrl = tbWsBaseUrl.Text.Trim();
            _config.ApiKey = tbApiKey.Password;
            _config.ListenUrl = tbListenUrl.Text.Trim();
            int.TryParse(tbMaxClients.Text, out int maxClients);
            _config.MaxClients = maxClients > 0 ? maxClients : 10;
            _config.DebugMode = cbDebugMode.IsChecked == true;
            _config.Save();
        }

        // ********************************************************************

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            SaveConfigFromUI();

            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                MessageBox.Show("Введите API Key.", "QScalp Server",
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
            tbApiBaseUrl.IsEnabled = !running;
            tbWsBaseUrl.IsEnabled = !running;
            tbApiKey.IsEnabled = !running;
            tbListenUrl.IsEnabled = !running;
            tbMaxClients.IsEnabled = !running;
            cbDebugMode.IsEnabled = !running;
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
