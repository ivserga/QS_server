// ==========================================================================
//  TickerAnalyzer.cs — Pipeline анализа одного тикера в реальном времени.
// ==========================================================================
//  Состав:
//    • QScalpWsClient    — WS-подписка на сервер;
//    • ClusterAggregator — построение кластеров из live-трейдов;
//    • SignalBus         — конвейер детекторов;
//    • SignalLogSink     — опциональный CSV-лог.
//
//  Snapshot от сервера ИГНОРИРУЕТСЯ (только подтверждение подписки) —
//  анализ начинается со следующего live-трейда. Это требование задачи.
//
//  Все события поднимаются на потоке WS — подписчики обязаны мершрутить
//  их в свой UI-поток самостоятельно (в ViewModel это делает Dispatcher).
// ==========================================================================

using System;
using System.IO;
using System.Threading.Tasks;

using QScalp.Client.Clusters;
using QScalp.Client.Clusters.Analytics;
using QScalp.Client.Clusters.Analytics.Detectors;
using QScalp.Client.Clusters.Analytics.Sinks;
using QScalp.Client.Clusters.Export;
using QScalp.Client.Config;
using QScalp.Client.Net;
using QScalp.Shared.Models;
using QScalp.Shared.Protocol;

namespace QScalp.Client.Pipeline
{
    internal sealed class TickerAnalyzer : IDisposable
    {
        readonly TickerConfig _config;
        readonly QScalpWsClient _ws;
        readonly ClusterAggregator _aggregator;
        readonly SignalBus _bus;
        ClusterJsonSink _clusterJsonSink;

        public TickerConfig Config => _config;
        public SignalBus Bus => _bus;

        public bool IsConnected => _ws.IsConnected;
        public string Ticker => _config.Ticker;

        public event Action<Signal> SignalEmitted;
        public event Action<bool> ConnectionStateChanged; // true=connected
        public event Action<string> LogMessage;
        public event Action<string> ErrorMessage;
        public event Action<Cluster> ClusterClosed;

        // ********************************************************************

        public TickerAnalyzer(string serverUrl, TickerConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _aggregator = new ClusterAggregator(config.ClusterBase, config.ClusterSize);
            _bus = new SignalBus(config.HistoryCapacity)
            {
                MinRepeatInterval = TimeSpan.FromSeconds(Math.Max(0, config.MinSignalIntervalSeconds)),
                MinRepeatBars = Math.Max(0, config.MinBarsBetweenRepeatedSignals),
                MaxSignalsPerCluster = Math.Max(0, config.MaxSignalsPerCluster),
                MinSignalStrength = Math.Max(0.0, config.MinSignalStrength)
            };

            BuildDetectors();

            _bus.AddSink(new ActionSink(s => SignalEmitted?.Invoke(s)));

            if (config.LogToCsv)
            {
                var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "signals");
                Directory.CreateDirectory(dir);
                var safeTicker = MakeSafeFilename(config.Ticker);
                var path = Path.Combine(dir, $"signals_{safeTicker}.csv");
                _bus.AddSink(new SignalLogSink(path));
            }

            if (config.ExportClustersToJson)
                StartClusterJsonExport(truncateFile: true);

            _aggregator.ClusterClosed += OnClusterClosed;

            _ws = new QScalpWsClient(serverUrl, config.Ticker, config.SecKey);
            _ws.OnTrade += OnTrade;
            _ws.OnSnapshot += OnSnapshot;
            _ws.OnConnected += () => ConnectionStateChanged?.Invoke(true);
            _ws.OnDisconnected += () => ConnectionStateChanged?.Invoke(false);
            _ws.OnError += msg => ErrorMessage?.Invoke(msg);
            _ws.OnLog += msg => LogMessage?.Invoke(msg);
        }

        // ********************************************************************

        void BuildDetectors()
        {
            // Идём по конфигу. Если конфиг пуст — добавляем все детекторы со
            // значениями по умолчанию.
            if (_config.Detectors == null || _config.Detectors.Count == 0)
            {
                _config.Detectors = new System.Collections.Generic.List<DetectorConfig>();
                foreach (var t in DetectorFactory.AllDetectorTypes)
                {
                    var d = DetectorFactory.CreateDefault(t);
                    d.Enabled = IsEnabledByDefault(d.Name);
                    _bus.AddDetector(d);
                    _config.Detectors.Add(new DetectorConfig
                    {
                        Name = d.Name,
                        Enabled = d.Enabled,
                        Params = DetectorFactory.ExtractParams(d)
                    });
                    ApplyTickerLegacyAbsorptionParams(d);
                }
                return;
            }

            foreach (var dc in _config.Detectors)
            {
                var det = DetectorFactory.CreateByName(dc.Name);
                if (det == null) continue;
                det.Enabled = dc.Enabled;
                DetectorFactory.ApplyParams(det, dc.Params);
                ApplyTickerLegacyAbsorptionParams(det);
                _bus.AddDetector(det);
            }
        }

        void ApplyTickerLegacyAbsorptionParams(ISignalDetector det)
        {
            if (det is LegacyAbsorptionDetector la)
            {
                la.VolumeRatioThreshold = _config.LegacyAbsorptionVolumeRatioThreshold;
                la.AbsorptionVolumeMultiplier = _config.LegacyAbsorptionVolumeMultiplier;
            }
        }

        static bool IsEnabledByDefault(string detectorName)
        {
            // Legacy-детекторы — прямые порты старого ClusterAnalyzer.cs.
            // Они полезны как дополнительный слой проверки, но слишком широкие
            // для стартового профиля и могут давать частые повторы на коротких
            // кластерах.
            return !detectorName.StartsWith("Legacy", StringComparison.Ordinal);
        }

        // ********************************************************************

        public void Start() => _ws.Start();

        public async Task StopAsync()
        {
            FlushOpenClusterToJson();
            await _ws.StopAsync();
            LogClusterJsonExportSummary();
        }

        public string ClusterJsonExportPath =>
            _clusterJsonSink?.FilePath ?? ClusterJsonSink.BuildDefaultPath(_config);

        public bool IsClusterJsonExportActive => _clusterJsonSink != null;

        public void ApplyClusterJsonExport(bool enabled)
        {
            if (enabled)
            {
                if (_clusterJsonSink != null) return;
                StartClusterJsonExport(truncateFile: false);
                return;
            }

            if (_clusterJsonSink == null) return;
            FlushOpenClusterToJson();
            LogClusterJsonExportSummary();
            try { _clusterJsonSink.Dispose(); } catch { }
            _clusterJsonSink = null;
        }
        public Task FillTradeTicketAsync(string side, int quantity, double signalStrength, string signalMessage)
            => _ws.FillTradeTicketAsync(side, quantity, signalStrength, signalMessage);

        // ********************************************************************

        void OnSnapshot(SnapshotPayload p)
        {
            // По требованию — снапшот не replay-им (анализ только с live).
            LogMessage?.Invoke($"[{_config.Ticker}] snapshot received and ignored " +
                               $"(recent={p.RecentTrades?.Length ?? 0})");
        }

        // ********************************************************************

        void OnTrade(Trade trade)
        {
            try
            {
                _aggregator.PutTrade(trade);
            }
            catch (Exception ex)
            {
                ErrorMessage?.Invoke("aggregator: " + ex.Message);
            }
        }

        // ********************************************************************

        void OnClusterClosed(Cluster c)
        {
            try
            {
                WriteClusterJson(c);
                _bus.OnClusterClosed(c, _config.PriceStep);
                ClusterClosed?.Invoke(c);
            }
            catch (Exception ex)
            {
                ErrorMessage?.Invoke("signalbus: " + ex.Message);
            }
        }

        // ********************************************************************

        void StartClusterJsonExport(bool truncateFile)
        {
            _clusterJsonSink = new ClusterJsonSink(
                _config,
                ClusterJsonSink.BuildDefaultPath(_config),
                msg => ErrorMessage?.Invoke("cluster export: " + msg));

            _clusterJsonSink.BeginSession(truncateFile);
            LogMessage?.Invoke($"[{_config.Ticker}] cluster JSON → {_clusterJsonSink.FilePath}");
        }

        void WriteClusterJson(Cluster c)
        {
            if (_clusterJsonSink == null || c == null) return;
            if (c.Volume <= 0 || c.DateTime == DateTime.MaxValue) return;

            var prev = _clusterJsonSink.ExportedCount;
            _clusterJsonSink.OnClusterClosed(c);
            if (_clusterJsonSink.ExportedCount == 1 && prev == 0)
                LogMessage?.Invoke($"[{_config.Ticker}] cluster JSON: first bar written");
        }

        void FlushOpenClusterToJson()
        {
            var c = _aggregator.Current;
            if (_clusterJsonSink == null || c == null) return;
            if (c.Volume <= 0 || c.DateTime == DateTime.MaxValue) return;

            var prev = _clusterJsonSink.ExportedCount;
            _clusterJsonSink.OnClusterClosed(c);
            if (_clusterJsonSink.ExportedCount > prev)
                LogMessage?.Invoke($"[{_config.Ticker}] cluster JSON: flushed open bar on stop");
        }

        void LogClusterJsonExportSummary()
        {
            if (_clusterJsonSink == null) return;
            LogMessage?.Invoke($"[{_config.Ticker}] cluster JSON done: " +
                               $"{_clusterJsonSink.ExportedCount} bar(s) → {_clusterJsonSink.FilePath}");
        }

        // ********************************************************************

        static string MakeSafeFilename(string s)
        {
            if (string.IsNullOrEmpty(s)) return "ticker";
            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s;
        }

        // ********************************************************************

        public void Dispose()
        {
            try { FlushOpenClusterToJson(); } catch { }
            try { _clusterJsonSink?.Dispose(); } catch { }
            try { _ws.Dispose(); } catch { }
        }
    }
}
