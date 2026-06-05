using System;
using System.Windows;
using QScalp.Recorder.Recording;

namespace QScalp.Recorder
{
    public partial class MainWindow : Window
    {
        private readonly RecorderConfig _config = RecorderConfig.Load();
        private RecordingSession _session;

        public MainWindow()
        {
            InitializeComponent();
            LoadConfigToUi();
        }

        private void LoadConfigToUi()
        {
            tbServerUrl.Text = string.IsNullOrWhiteSpace(_config.ServerUrl)
                ? "ws://127.0.0.1:9500/"
                : _config.ServerUrl;
            tbOutputDir.Text = string.IsNullOrWhiteSpace(_config.OutputDirectory)
                ? "Records"
                : _config.OutputDirectory;
            if (_config.Tickers != null && _config.Tickers.Length > 0)
                tbTicker.Text = _config.Tickers[0];
            cbSnapshots.IsChecked = _config.RecordSnapshots;
        }

        private void SaveConfigFromUi()
        {
            _config.ServerUrl = tbServerUrl.Text.Trim();
            _config.OutputDirectory = tbOutputDir.Text.Trim();
            var t = tbTicker.Text.Trim().ToUpperInvariant();
            _config.Tickers = string.IsNullOrEmpty(t) ? Array.Empty<string>() : new[] { t };
            _config.RecordSnapshots = cbSnapshots.IsChecked == true;
            _config.Save();
        }

        private void BtnRecord_Click(object sender, RoutedEventArgs e)
        {
            SaveConfigFromUi();

            var ticker = tbTicker.Text.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(ticker))
            {
                MessageBox.Show("Введите тикер.", "QScalp Recorder",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var serverUrl = tbServerUrl.Text.Trim();
            if (string.IsNullOrEmpty(serverUrl))
            {
                MessageBox.Show("Укажите URL сервера (ws://…).", "QScalp Recorder",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _session = new RecordingSession();
                _session.OnLog += Log;
                _session.OnError += s => Log("[ERR] " + s);
                _session.OnLineCountChanged += n =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        tbStatus.Text = $"Запись {ticker} — строк: {n}";
                    }));
                };

                _session.Start(serverUrl, ticker, tbOutputDir.Text.Trim(),
                    cbSnapshots.IsChecked == true);

                tbSessionPath.Text = _session.SessionDirectory;
                tbStatus.Text = $"Запись {ticker}…";
                SetRecordingState(true);
                Log($"Старт: {ticker} → {_session.SessionDirectory}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                _session?.Dispose();
                _session = null;
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e) => StopRecording();

        private void StopRecording()
        {
            if (_session == null) return;

            try { _session.Stop(); }
            catch (Exception ex) { Log("[ERR] " + ex.Message); }
            finally
            {
                _session.Dispose();
                _session = null;
            }

            tbStatus.Text = "Остановлено";
            SetRecordingState(false);
        }

        private void SetRecordingState(bool recording)
        {
            btnRecord.IsEnabled = !recording;
            btnStop.IsEnabled = recording;
            tbServerUrl.IsEnabled = !recording;
            tbOutputDir.IsEnabled = !recording;
            tbTicker.IsEnabled = !recording;
            cbSnapshots.IsEnabled = !recording;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveConfigFromUi();
            StopRecording();
        }

        private void Log(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<string>(Log), message);
                return;
            }

            tbLog.AppendText($"{DateTime.Now:HH:mm:ss}  {message}\r\n");
            tbLog.ScrollToEnd();
        }
    }
}
