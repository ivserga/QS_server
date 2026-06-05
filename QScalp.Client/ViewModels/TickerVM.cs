// ==========================================================================
//  TickerVM.cs — UI-состояние одного анализируемого тикера.
// ==========================================================================
//  Владеет TickerAnalyzer'ом, перенаправляет события в Dispatcher,
//  держит ObservableCollection сигналов и список DetectorVM для редактора.
// ==========================================================================

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

using QScalp.Client.Clusters.Analytics;
using QScalp.Client.Config;
using QScalp.Client.Pipeline;
using QScalp.Shared.Models;

namespace QScalp.Client.ViewModels
{
    public sealed class TickerVM : ViewModelBase, IDisposable
    {
        readonly Dispatcher _dispatcher;
        readonly TickerConfig _config;
        readonly string _serverUrl;

        TickerAnalyzer _analyzer;
        bool _connected;
        string _status = "Остановлен";
        int _signalCount;
        int _clusterCount;
        DateTime _lastTradeTime;

        public string Ticker => _config.Ticker;
        public string SecKey
        {
            get => _config.SecKey;
            set { _config.SecKey = value; Raise(); }
        }
        public int PriceStep
        {
            get => _config.PriceStep;
            set { _config.PriceStep = value; Raise(); }
        }
        public ClusterBase ClusterBase
        {
            get => _config.ClusterBase;
            set { _config.ClusterBase = value; Raise(); }
        }
        public int ClusterSize
        {
            get => _config.ClusterSize;
            set { _config.ClusterSize = value; Raise(); }
        }
        public int HistoryCapacity
        {
            get => _config.HistoryCapacity;
            set { _config.HistoryCapacity = value; Raise(); }
        }
        public bool LogToCsv
        {
            get => _config.LogToCsv;
            set { _config.LogToCsv = value; Raise(); }
        }

        public bool ExportClustersToJson
        {
            get => _config.ExportClustersToJson;
            set
            {
                if (_config.ExportClustersToJson == value) return;
                _config.ExportClustersToJson = value;
                Raise();
                Raise(nameof(ClustersExportPath));
                _analyzer?.ApplyClusterJsonExport(value);
            }
        }

        /// <summary>Путь к JSONL рядом с exe: clusters\clusters_{TICKER}.jsonl</summary>
        public string ClustersExportPath
            => QScalp.Client.Clusters.Export.ClusterJsonSink.BuildDefaultPath(_config);

        public int MinSignalIntervalSeconds
        {
            get => _config.MinSignalIntervalSeconds;
            set { _config.MinSignalIntervalSeconds = value; Raise(); }
        }

        public int MinBarsBetweenRepeatedSignals
        {
            get => _config.MinBarsBetweenRepeatedSignals;
            set { _config.MinBarsBetweenRepeatedSignals = value; Raise(); }
        }

        public int MaxSignalsPerCluster
        {
            get => _config.MaxSignalsPerCluster;
            set { _config.MaxSignalsPerCluster = value; Raise(); }
        }

        public double MinSignalStrength
        {
            get => _config.MinSignalStrength;
            set { _config.MinSignalStrength = value; Raise(); }
        }

        public bool AutoFillTradeTicket
        {
            get => _config.AutoFillTradeTicket;
            set { _config.AutoFillTradeTicket = value; Raise(); }
        }

        public double TradeTicketMinSignalStrength
        {
            get => _config.TradeTicketMinSignalStrength;
            set { _config.TradeTicketMinSignalStrength = value; Raise(); }
        }

        public int TradeTicketQuantity
        {
            get => _config.TradeTicketQuantity;
            set { _config.TradeTicketQuantity = value; Raise(); }
        }

        public ObservableCollection<DetectorVM> Detectors { get; } = new ObservableCollection<DetectorVM>();

        public RelayCommand EnableAllDetectorsCommand { get; }
        public RelayCommand DisableAllDetectorsCommand { get; }

        public int EnabledDetectorsCount => Detectors.Count(d => d.Enabled);
        public int TotalDetectorsCount   => Detectors.Count;
        public string EnabledDetectorsText
            => Detectors.Count == 0
                ? "Запустите анализ — список появится"
                : $"Активно {EnabledDetectorsCount} из {TotalDetectorsCount}";
        public ObservableCollection<SignalRowVM> Signals { get; } = new ObservableCollection<SignalRowVM>();

        public bool Connected
        {
            get => _connected;
            private set
            {
                if (Set(ref _connected, value))
                    RaiseStatusVisuals();
            }
        }

        public string Status
        {
            get => _status;
            private set => Set(ref _status, value);
        }

        public int SignalCount
        {
            get => _signalCount;
            private set
            {
                if (Set(ref _signalCount, value))
                    Raise(nameof(SignalCountBrush));
            }
        }

        public int ClusterCount
        {
            get => _clusterCount;
            private set => Set(ref _clusterCount, value);
        }

        public DateTime LastTradeTime
        {
            get => _lastTradeTime;
            private set { Set(ref _lastTradeTime, value); Raise(nameof(LastTradeText)); }
        }

        public string LastTradeText
            => _lastTradeTime == DateTime.MinValue ? "—" : _lastTradeTime.ToString("HH:mm:ss");

        // ------- Старое поле, оставлено для совместимости (Ellipse в TickerDetailView) -------
        public string StatusBrush => _connected ? "#22A06B" : "#C7392A";

        // ------- Новые цвета/тексты статуса для шапки и списка тикеров ----------------------
        /// <summary>Текст бейджа статуса: «LIVE» / «STOPPED» / «WAIT».</summary>
        public string StatusBadgeText
        {
            get
            {
                if (!IsRunning) return "OFF";
                return _connected ? "LIVE" : "WAIT";
            }
        }

        /// <summary>Акцентный цвет статуса (контур бейджа, левая полоска тикера, текст бейджа).</summary>
        public string StatusAccentBrush
        {
            get
            {
                if (!IsRunning) return "#5A5D63";
                return _connected ? "#22A06B" : "#E0A23A";
            }
        }

        /// <summary>Приглушённый фон бейджа статуса.</summary>
        public string StatusBgBrush
        {
            get
            {
                if (!IsRunning) return "#2B2D31";
                return _connected ? "#1B3A2C" : "#3A2E18";
            }
        }

        /// <summary>Цвет числа сигналов: акцентный, если сигналы есть; нейтральный, если 0.</summary>
        public string SignalCountBrush
            => _signalCount > 0 ? "#3FCC85" : "#9CA0A6";

        public bool IsRunning => _analyzer != null;

        // ********************************************************************

        void RaiseStatusVisuals()
        {
            Raise(nameof(StatusBrush));
            Raise(nameof(StatusBadgeText));
            Raise(nameof(StatusAccentBrush));
            Raise(nameof(StatusBgBrush));
        }

        public TickerConfig Config => _config;

        public ObservableCollection<LogEntryVM> Log { get; } = new ObservableCollection<LogEntryVM>();

        // ********************************************************************

        public TickerVM(string serverUrl, TickerConfig config, Dispatcher dispatcher)
        {
            _serverUrl = serverUrl;
            _config = config;
            _dispatcher = dispatcher;

            EnableAllDetectorsCommand  = new RelayCommand(_ => SetAllDetectors(true),
                                                          _ => Detectors.Count > 0);
            DisableAllDetectorsCommand = new RelayCommand(_ => SetAllDetectors(false),
                                                          _ => Detectors.Count > 0);
        }

        // ********************************************************************

        void SetAllDetectors(bool enabled)
        {
            foreach (var d in Detectors) d.Enabled = enabled;
        }

        void OnDetectorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DetectorVM.Enabled))
                RaiseDetectorStats();
        }

        void RaiseDetectorStats()
        {
            Raise(nameof(EnabledDetectorsCount));
            Raise(nameof(TotalDetectorsCount));
            Raise(nameof(EnabledDetectorsText));
        }

        // ********************************************************************

        public void Start()
        {
            if (IsRunning) return;

            _analyzer = new TickerAnalyzer(_serverUrl, _config);
            RebuildDetectorList();

            _analyzer.SignalEmitted += OnSignal;
            _analyzer.ConnectionStateChanged += OnConnState;
            _analyzer.LogMessage += msg => RunOnUi(() => AppendLog(msg));
            _analyzer.ErrorMessage += msg => RunOnUi(() => AppendLog("ERR: " + msg));
            _analyzer.ClusterClosed += c => RunOnUi(() =>
            {
                ClusterCount++;
                LastTradeTime = c.DateTime;
            });

            Status = "Подключение...";
            _analyzer.Start();
            Raise(nameof(IsRunning));
            RaiseStatusVisuals();
        }

        public async Task StopAsync()
        {
            if (!IsRunning) return;

            // Сохраняем текущие настройки детекторов в конфиг — иначе при
            // следующем Старте детекторы пересоздадутся со значениями из
            // _config.Detectors и пользовательские правки потеряются.
            try { SyncConfigFromDetectors(); } catch { }

            var a = _analyzer;
            _analyzer = null;
            try { await a.StopAsync(); } catch { }
            try { a.Dispose(); } catch { }

            Connected = false;
            Status = "Остановлен";
            Raise(nameof(IsRunning));
            RaiseStatusVisuals();
        }

        // ********************************************************************

        void RebuildDetectorList()
        {
            // Отписаться от старых VM, чтобы не текли подписки.
            foreach (var d in Detectors)
                d.PropertyChanged -= OnDetectorPropertyChanged;

            Detectors.Clear();
            if (_analyzer?.Bus?.Detectors == null)
            {
                RaiseDetectorStats();
                return;
            }

            foreach (var d in _analyzer.Bus.Detectors)
            {
                var vm = new DetectorVM(d);
                vm.PropertyChanged += OnDetectorPropertyChanged;
                Detectors.Add(vm);
            }
            RaiseDetectorStats();
        }

        // ********************************************************************

        void OnSignal(Signal signal)
        {
            TryFillTradeTicket(signal);

            RunOnUi(() =>
            {
                Signals.Insert(0, new SignalRowVM(signal));
                while (Signals.Count > 500) Signals.RemoveAt(Signals.Count - 1);
                SignalCount++;
            });
        }

        void TryFillTradeTicket(Signal signal)
        {
            if (signal == null || _analyzer == null) return;
            if (!_config.AutoFillTradeTicket) return;
            if (_config.TradeTicketQuantity <= 0) return;
            if (signal.Strength < _config.TradeTicketMinSignalStrength) return;

            string side;
            switch (signal.Direction)
            {
                case SignalDirection.Up:
                    side = "Buy";
                    break;
                case SignalDirection.Down:
                    side = "Sell";
                    break;
                default:
                    AppendTicketLog("Trade Ticket пропущен: у сигнала нет направления.");
                    return;
            }

            var msg = $"{signal.Source}: {signal.Kind} {signal.Direction}";
            _ = SendTradeTicketAsync(side, _config.TradeTicketQuantity, signal.Strength, msg);
        }

        async Task SendTradeTicketAsync(string side, int quantity, double strength, string message)
        {
            try
            {
                var analyzer = _analyzer;
                if (analyzer == null) return;

                await analyzer.FillTradeTicketAsync(side, quantity, strength, message);
                AppendTicketLog($"Trade Ticket: запрос отправлен ({side} {quantity} {Ticker}, signal={strength:0.###}).");
            }
            catch (Exception ex)
            {
                AppendTicketLog("Trade Ticket ошибка: " + ex.Message);
            }
        }

        void AppendTicketLog(string message)
        {
            RunOnUi(() => AppendLog(message));
        }

        void OnConnState(bool connected)
        {
            RunOnUi(() =>
            {
                Connected = connected;
                Status = connected ? "Подключён" : "Соединение потеряно";
            });
        }

        // ********************************************************************

        void AppendLog(string msg)
        {
            var line = DateTime.Now.ToString("HH:mm:ss") + "  " + msg;
            Log.Insert(0, new LogEntryVM(line));
            while (Log.Count > 200) Log.RemoveAt(Log.Count - 1);
        }

        // ********************************************************************

        /// <summary>
        /// Сериализует текущие настройки детекторов обратно в TickerConfig.Detectors.
        /// </summary>
        public void SyncConfigFromDetectors()
        {
            _config.Detectors.Clear();
            foreach (var dvm in Detectors)
            {
                var det = dvm.Detector;
                _config.Detectors.Add(new DetectorConfig
                {
                    Name = det.Name,
                    Enabled = det.Enabled,
                    Params = DetectorFactory.ExtractParams(det)
                });
            }
        }

        // ********************************************************************

        void RunOnUi(Action a)
        {
            if (_dispatcher.CheckAccess()) a();
            else _dispatcher.BeginInvoke(a);
        }

        public void Dispose()
        {
            try { StopAsync().GetAwaiter().GetResult(); } catch { }
        }
    }
}
