// ==========================================================================
//  DetectorVM.cs — UI-обёртка одного детектора (Enabled + параметры).
// ==========================================================================

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

using QScalp.Client.Clusters.Analytics;

namespace QScalp.Client.ViewModels
{
    public sealed class DetectorVM : ViewModelBase
    {
        // Краткие описания на русском — выводятся в карточке детектора
        // под именем. Ключ — Detector.Name из ISignalDetector.
        static readonly Dictionary<string, string> Descriptions =
            new Dictionary<string, string>
            {
                { "Absorption",         "Поглощение крупного потока на границе диапазона: ожидание отскока от уровня." },
                { "Climax",             "Аномальный объём в узкой ценовой зоне: возможна кульминация и разворот." },
                { "BalanceAfterClimax", "Компрессия объёма и диапазона после климакса: подтверждение завершения импульса." },
                { "VReversal",          "V-разворот после серии однонаправленных кластеров с поглощением у края." },
                { "HvnReject",          "Отскок цены от High Volume Node — крупного аккумулированного уровня." },
                { "Distribution",       "Распределение у вершины: lower highs, ослабление объёма, цена ниже середины." },
                { "Accumulation",       "Накопление у дна: higher lows, рост объёма, возврат покупателей." },
                { "Breakout",           "Чистый пробой диапазона на повышенном объёме с сильным телом." },
                { "DoubleTop",          "Двойная вершина (M-pattern): два пика близко по цене, цель — линия шеи." },
                { "DoubleBottom",       "Двойное дно (W-pattern): два минимума близко по цене, цель — линия шеи." },
                { "OrphanClose",        "Закрытие далеко от центра объёма: ожидание возврата к COM." },
                { "LegacyAbsorption",   "Поглощение по 3-кластерной модели (BearishDivergence/BullishDivergence)." },
                { "LegacyClimax",       "Объёмный климакс по 3 кластерам с x3 объёмом и ratio распределения." },
                { "LegacyRejection",    "Отбой цены от уровня поддержки/сопротивления по 3 кластерам с касаниями." }
            };

        readonly ISignalDetector _detector;

        public string Name => _detector.Name;

        public string Description
            => Descriptions.TryGetValue(_detector.Name, out var d) ? d : "";

        /// <summary>«Modern» для современных детекторов и «Legacy» для портов
        /// 3-кластерного ClusterAnalyzer.cs. Используется для группировки в UI.</summary>
        public string Category
            => _detector.Name.StartsWith("Legacy", System.StringComparison.Ordinal)
                ? "Legacy (3-кластерный анализ)"
                : "Современные детекторы";

        public int ParameterCount => Parameters.Count;

        public bool Enabled
        {
            get => _detector.Enabled;
            set
            {
                if (_detector.Enabled == value) return;
                _detector.Enabled = value;
                Raise(nameof(Enabled));
            }
        }

        public ObservableCollection<DetectorParamVM> Parameters { get; }

        // ********************************************************************

        internal DetectorVM(ISignalDetector det)
        {
            _detector = det;

            var props = det.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite
                            && p.Name != nameof(ISignalDetector.Enabled)
                            && p.Name != nameof(ISignalDetector.Name)
                            && IsScalar(p.PropertyType));

            Parameters = new ObservableCollection<DetectorParamVM>(
                props.Select(p => new DetectorParamVM(det, p)));
        }

        // ********************************************************************

        internal ISignalDetector Detector => _detector;

        static bool IsScalar(System.Type t)
        {
            return t == typeof(int) || t == typeof(double) || t == typeof(bool)
                || t == typeof(long) || t == typeof(float);
        }
    }
}
