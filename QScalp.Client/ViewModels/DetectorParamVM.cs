// ==========================================================================
//  DetectorParamVM.cs — Один редактируемый параметр одного детектора.
// ==========================================================================
//  В UI генерируется TextBox для int/double и CheckBox для bool. ParseAndSet
//  принимает строку из TextBox и применяет её обратно к свойству детектора.
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

using QScalp.Client.Clusters.Analytics;

namespace QScalp.Client.ViewModels
{
    public sealed class DetectorParamVM : ViewModelBase
    {
        static readonly Dictionary<string, string> Descriptions =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // ---- Common / generic -------------------------------------------------
                { "AverageWindow", "Сколько предыдущих кластеров брать для среднего объёма/диапазона/плотности." },
                { "CooldownBars", "Минимальная пауза в закрытых кластерах после сигнала этого детектора." },
                { "Lookback", "Сколько последних закрытых кластеров анализировать для паттерна." },
                { "LookbackBars", "Сколько последних закрытых кластеров анализировать для уровня/диапазона." },
                { "MinStrength", "Минимальная внутренняя сила сигнала этого детектора. Больше = меньше сигналов." },
                { "UseClusterUplift", "Добавлять/вычитать баллы силы по дополнительным кластерным признакам." },
                { "VolumeMultiplier", "Текущий объём должен превышать средний объём в это число раз." },
                { "MinVolumeRatio", "Минимальный объём относительно среднего. 1.0 = не ниже среднего." },
                { "VolumeMinRatio", "Минимальный объём относительно среднего. 0.8 = не ниже 80% среднего." },
                { "MaxVolumeRatio", "Максимально допустимый объём второго экстремума относительно первого." },
                { "MinSwingDiffTicks", "Минимальная разница swing-экстремумов в тиках/ценовых шагах." },
                { "MinBodyRatio", "Минимальный размер тела кластера относительно всего диапазона." },
                { "MinBreakoutTicks", "Минимальная глубина пробоя предыдущего экстремума." },
                { "MaxTop3Share", "Максимальная допустимая концентрация объёма в трёх самых нагруженных уровнях." },
                { "MaxTailShare", "Максимальная доля объёма за проверяемым уровнем/узлом." },
                { "MinTop3Share", "Минимальная доля объёма в трёх самых нагруженных ценовых уровнях." },
                { "MinHvnShare", "Минимальная доля объёма HVN в общем объёме окна." },
                { "MinReclaimTicks", "На сколько тиков цена должна вернуться от HVN после прокола." },
                { "VolumeCenterTolTicks", "Допустимое смещение VolumeCenter между двумя экстремумами, в тиках." },

                // ---- Absorption -------------------------------------------------------
                { "Absorption.MinTop3Share", "Минимальная доля Top3. Больше = абсорбция должна быть концентрированнее." },
                { "Absorption.EdgeThreshold", "Насколько близко COM должен быть к краю диапазона. 0.85 = верхние/нижние 15%." },
                { "Absorption.TailMaxShare", "Максимальная доля объёма за COM. Меньше = уровень чище удержан." },

                // ---- Climax -----------------------------------------------------------
                { "Climax.MinDensityMultiplier", "Плотность объёма на диапазон должна быть выше средней в это число раз." },
                { "Climax.EdgePosComTop", "COM выше этого значения считается верхним климаксом покупок." },
                { "Climax.EdgePosComBottom", "COM ниже этого значения считается нижним климаксом продаж." },

                // ---- BalanceAfterClimax ---------------------------------------------
                { "BalanceAfterClimax.BalanceVolumeShare", "Объём текущего кластера должен быть не больше этой доли от климакса." },
                { "BalanceAfterClimax.BalanceRangeShare", "Диапазон текущего кластера должен быть не больше этой доли от климакса." },
                { "BalanceAfterClimax.MaxDeltaShare", "Максимальная абсолютная дельта как доля от объёма текущего кластера." },
                { "BalanceAfterClimax.ClimaxMinTop3Share", "Минимальная концентрация Top-3 у предыдущего кластера, чтобы считать его климаксом." },
                { "BalanceAfterClimax.ClimaxDensityMult", "Минимальная плотность предыдущего климакс-кластера относительно средней." },

                // ---- VReversal --------------------------------------------------------
                { "VReversal.MinRun", "Минимум подряд идущих однонаправленных кластеров перед разворотом." },
                { "VReversal.AbsorptionEdge", "Насколько близко COM предыдущего кластера должен быть к краю диапазона." },
                { "VReversal.AbsorptionTop3Share", "Минимальная доля Top3 у предыдущего кластера." },
                { "VReversal.MinCenterOfMassShift", "Минимальный сдвиг центра объёма в сторону разворота." },

                // ---- Distribution / Accumulation -------------------------------------
                { "Distribution.MaxVolumeSlope", "Максимальный наклон объёма. Отрицательное значение требует снижения объёма." },
                { "Distribution.MinHighAgeBars", "Сколько кластеров должно пройти после максимума, чтобы сигнал был допустим." },
                { "Distribution.PosComLowThreshold", "COM ниже этого значения считается расположенным внизу кластера." },
                { "Distribution.MinShareLowPosCom", "Минимальная доля кластеров окна с COM внизу." },
                { "Accumulation.MinVolumeSlope", "Минимальный наклон объёма. Больше = требуется более явный рост объёма." },
                { "Accumulation.MinLowAgeBars", "Сколько кластеров должно пройти после минимума, чтобы сигнал был допустим." },
                { "Accumulation.PosComHighThreshold", "COM выше этого значения считается расположенным вверху кластера." },
                { "Accumulation.MinShareHighPosCom", "Минимальная доля кластеров окна с COM вверху." },

                // ---- Breakout ---------------------------------------------------------
                { "Breakout.PosComFavorable", "COM должен быть в благоприятной зоне: сверху для пробоя вверх, снизу для пробоя вниз." },

                // ---- DoubleTop / DoubleBottom ----------------------------------------
                { "DoubleTop.PeakLeftRight", "Сколько кластеров слева/справа нужно для подтверждения локального пика." },
                { "DoubleTop.MaxPeakDiffTicks", "Максимальная разница между двумя вершинами." },
                { "DoubleTop.MinBarsBetweenPeaks", "Минимальное расстояние между двумя вершинами." },
                { "DoubleTop.MinCorrectionTicks", "Минимальная коррекция от вершин к линии шеи." },
                { "DoubleTop.MinSecondPeakAgeBars", "Вторая вершина должна быть не моложе этого числа кластеров." },
                { "DoubleTop.MaxSecondPeakAgeBars", "Вторая вершина должна быть не старше этого числа кластеров." },
                { "DoubleTop.MinPostPeakDropTicks", "Цена должна уже отойти вниз от второй вершины минимум на это число тиков." },
                { "DoubleBottom.TroughLeftRight", "Сколько кластеров слева/справа нужно для подтверждения локального дна." },
                { "DoubleBottom.MaxTroughDiffTicks", "Максимальная разница между двумя минимумами." },
                { "DoubleBottom.MinBarsBetweenTroughs", "Минимальное расстояние между двумя минимумами." },
                { "DoubleBottom.MinReboundTicks", "Минимальный отскок между двумя минимумами к линии шеи." },
                { "DoubleBottom.MinSecondTroughAgeBars", "Второе дно должно быть не моложе этого числа кластеров." },
                { "DoubleBottom.MaxSecondTroughAgeBars", "Второе дно должно быть не старше этого числа кластеров." },
                { "DoubleBottom.MinPostTroughRiseTicks", "Цена должна уже отойти вверх от второго дна минимум на это число тиков." },

                // ---- OrphanClose ------------------------------------------------------
                { "OrphanClose.MinRangeTicks", "Минимальный диапазон кластера для проверки сиротского закрытия." },
                { "OrphanClose.MinGapShare", "Насколько далеко close должен быть от COM относительно диапазона." },

                // ---- Legacy -----------------------------------------------------------
                { "VolumeRatioThreshold", "Минимальная доля объёма выше/ниже reference price для legacy-сигнала." },
                { "LegacyAbsorption.AbsorptionVolumeMultiplier", "Объём третьего кластера должен быть больше предыдущих в это число раз." },
                { "LegacyClimax.VolumeClimaxMultiplier", "Объём текущего кластера должен превышать два предыдущих в это число раз." },
                { "LegacyRejection.RejectionCellRatioThreshold", "Минимальная доля объёма на экстремальной цене кластера." },
                { "LegacyRejection.RejectionMinTouches", "Минимум касаний уровня за последние три кластера." }
            };

        readonly ISignalDetector _detector;
        readonly PropertyInfo _prop;

        public string Name => _prop.Name;
        public string TypeName => _prop.PropertyType.Name;
        public bool IsBool => _prop.PropertyType == typeof(bool);
        public string Description => GetDescription(_detector.Name, _prop.Name);
        public string Tooltip => $"{Name} ({TypeName})\n{Description}";

        public string ValueText
        {
            get
            {
                var v = _prop.GetValue(_detector, null);
                if (v is double d) return d.ToString("G", CultureInfo.InvariantCulture);
                if (v is float f) return f.ToString("G", CultureInfo.InvariantCulture);
                return v?.ToString() ?? string.Empty;
            }
            set
            {
                if (TryParse(value, out var converted))
                {
                    _prop.SetValue(_detector, converted, null);
                    Raise(nameof(ValueText));
                }
            }
        }

        public bool BoolValue
        {
            get => (bool)_prop.GetValue(_detector, null);
            set
            {
                _prop.SetValue(_detector, value, null);
                Raise(nameof(BoolValue));
            }
        }

        // ********************************************************************

        internal DetectorParamVM(ISignalDetector det, PropertyInfo prop)
        {
            _detector = det;
            _prop = prop;
        }

        // ********************************************************************

        bool TryParse(string raw, out object converted)
        {
            converted = null;
            if (raw == null) return false;
            raw = raw.Trim();

            try
            {
                if (_prop.PropertyType == typeof(int)) { converted = int.Parse(raw, CultureInfo.InvariantCulture); return true; }
                if (_prop.PropertyType == typeof(long)) { converted = long.Parse(raw, CultureInfo.InvariantCulture); return true; }
                if (_prop.PropertyType == typeof(double)) { converted = double.Parse(raw.Replace(',', '.'), CultureInfo.InvariantCulture); return true; }
                if (_prop.PropertyType == typeof(float)) { converted = float.Parse(raw.Replace(',', '.'), CultureInfo.InvariantCulture); return true; }
                if (_prop.PropertyType == typeof(bool)) { converted = bool.Parse(raw); return true; }
            }
            catch { /* ignored */ }
            return false;
        }

        static string GetDescription(string detectorName, string paramName)
        {
            if (Descriptions.TryGetValue(detectorName + "." + paramName, out var specific))
                return specific;
            if (Descriptions.TryGetValue(paramName, out var generic))
                return generic;
            return "Настраиваемый параметр детектора. Увеличение обычно делает условие строже, уменьшение — чувствительнее.";
        }
    }
}
