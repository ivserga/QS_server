// ==========================================================================
//  SignalBus.cs — Оркестратор детекторов и приёмников сигналов
// ==========================================================================
//  Получает закрытые кластеры → считает ClusterStats → прогоняет детекторы →
//  рассылает результаты во все ISignalSink. Полный аналог оригинала из WPF
//  проекта, headless.
// ==========================================================================

using System;
using System.Collections.Generic;

namespace QScalp.Client.Clusters.Analytics
{
    internal sealed class SignalBus
    {
        public ClusterHistory History { get; private set; }

        readonly List<ISignalDetector> _detectors;
        readonly List<ISignalSink> _sinks;
        readonly Dictionary<string, EmissionState> _lastEmittedByKey;

        int _barIndex;

        public IReadOnlyList<ISignalDetector> Detectors => _detectors;

        /// <summary>
        /// Минимальный интервал между повторными сигналами одного типа
        /// (Source + Kind + Direction). 0 — без ограничения по времени.
        /// </summary>
        public TimeSpan MinRepeatInterval { get; set; }

        /// <summary>
        /// Минимальное число закрытых кластеров между повторными сигналами
        /// одного типа. 0 — без ограничения по барам.
        /// </summary>
        public int MinRepeatBars { get; set; }

        /// <summary>
        /// Максимум сигналов, который можно отправить наружу на одном закрытом
        /// кластере. 0 или меньше — без ограничения.
        /// </summary>
        public int MaxSignalsPerCluster { get; set; }

        /// <summary>
        /// Минимальная сила сигнала для публикации. 0 — без фильтра.
        /// </summary>
        public double MinSignalStrength { get; set; }

        public SignalBus(int historyCapacity)
        {
            History = new ClusterHistory(historyCapacity);
            _detectors = new List<ISignalDetector>();
            _sinks = new List<ISignalSink>();
            _lastEmittedByKey = new Dictionary<string, EmissionState>();

            MinRepeatInterval = TimeSpan.FromSeconds(30);
            MinRepeatBars = 3;
            MaxSignalsPerCluster = 3;
            MinSignalStrength = 0.0;
        }

        public SignalBus AddDetector(ISignalDetector detector)
        {
            if (detector != null) _detectors.Add(detector);
            return this;
        }

        public SignalBus AddSink(ISignalSink sink)
        {
            if (sink != null) _sinks.Add(sink);
            return this;
        }

        public void OnClusterClosed(Cluster cluster, int priceStep)
        {
            var stats = ClusterStats.Compute(cluster, priceStep);
            if (stats == null) return;

            History.Add(stats);
            _barIndex++;

            int emittedOnThisCluster = 0;
            for (int i = 0; i < _detectors.Count; i++)
            {
                var d = _detectors[i];
                if (!d.Enabled) continue;

                Signal s = d.Evaluate(History);
                if (s == null || s.Kind == SignalKind.None) continue;

                if (string.IsNullOrEmpty(s.Source)) s.Source = d.Name;
                if (!ShouldEmit(s)) continue;

                for (int j = 0; j < _sinks.Count; j++) _sinks[j].Emit(s);
                MarkEmitted(s);

                emittedOnThisCluster++;
                if (MaxSignalsPerCluster > 0 && emittedOnThisCluster >= MaxSignalsPerCluster)
                    break;
            }
        }

        public void Reset(int historyCapacity)
        {
            History = new ClusterHistory(historyCapacity);
            _lastEmittedByKey.Clear();
            _barIndex = 0;
        }

        bool ShouldEmit(Signal signal)
        {
            if (signal == null) return false;
            if (MinSignalStrength > 0 && signal.Strength < MinSignalStrength)
                return false;

            var key = GetSuppressionKey(signal);
            if (!_lastEmittedByKey.TryGetValue(key, out var last))
                return true;

            if (MinRepeatBars > 0 && _barIndex - last.BarIndex < MinRepeatBars)
                return false;

            if (MinRepeatInterval > TimeSpan.Zero)
            {
                var t = signal.Time == DateTime.MinValue ? History.LastTime : signal.Time;
                if (last.Time != DateTime.MinValue && t - last.Time < MinRepeatInterval)
                    return false;
            }

            return true;
        }

        void MarkEmitted(Signal signal)
        {
            var key = GetSuppressionKey(signal);
            _lastEmittedByKey[key] = new EmissionState
            {
                Time = signal.Time == DateTime.MinValue ? History.LastTime : signal.Time,
                BarIndex = _barIndex
            };
        }

        static string GetSuppressionKey(Signal signal)
        {
            return (signal.Source ?? string.Empty) + "|" + signal.Kind + "|" + signal.Direction;
        }

        struct EmissionState
        {
            public DateTime Time;
            public int BarIndex;
        }
    }
}
