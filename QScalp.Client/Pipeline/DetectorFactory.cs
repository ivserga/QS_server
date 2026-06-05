// ==========================================================================
//  DetectorFactory.cs — Создание и настройка детекторов из конфига.
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Newtonsoft.Json.Linq;

using QScalp.Client.Clusters.Analytics;
using QScalp.Client.Clusters.Analytics.Detectors;
using QScalp.Client.Config;

namespace QScalp.Client.Pipeline
{
    internal static class DetectorFactory
    {
        // Все доступные типы детекторов; порядок важен — UI выводит их в
        // том же порядке.
        public static readonly Type[] AllDetectorTypes =
        {
            typeof(AbsorptionDetector),
            typeof(ClimaxDetector),
            typeof(BalanceAfterClimaxDetector),
            typeof(VReversalDetector),
            typeof(HvnRejectDetector),
            typeof(DistributionDetector),
            typeof(AccumulationDetector),
            typeof(BreakoutDetector),
            typeof(DoubleTopDetector),
            typeof(DoubleBottomDetector),
            typeof(OrphanCloseDetector),
            typeof(LegacyAbsorptionDetector),
            typeof(LegacyClimaxDetector),
            typeof(LegacyRejectionDetector)
        };

        // ********************************************************************

        public static ISignalDetector CreateDefault(Type t)
        {
            return (ISignalDetector)Activator.CreateInstance(t);
        }

        public static ISignalDetector CreateByName(string name)
        {
            var t = AllDetectorTypes.FirstOrDefault(x =>
                ((ISignalDetector)Activator.CreateInstance(x)).Name == name);
            return t != null ? CreateDefault(t) : null;
        }

        // ********************************************************************

        /// <summary>
        /// Применяет параметры из словаря к свойствам детектора через рефлексию.
        /// Ключи словаря должны совпадать с именами публичных свойств.
        /// </summary>
        public static void ApplyParams(ISignalDetector det, IDictionary<string, object> args)
        {
            if (det == null || args == null) return;

            var props = det.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite);

            foreach (var p in props)
            {
                if (!args.TryGetValue(p.Name, out var raw)) continue;
                if (raw == null) continue;

                try
                {
                    object value = ConvertValue(raw, p.PropertyType);
                    p.SetValue(det, value, null);
                }
                catch
                {
                    // молча игнорируем — параметр останется значением по умолчанию
                }
            }
        }

        public static Dictionary<string, object> ExtractParams(ISignalDetector det)
        {
            var result = new Dictionary<string, object>();
            if (det == null) return result;

            foreach (var p in det.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanRead || !p.CanWrite) continue;
                if (p.Name == "Name" || p.Name == "Enabled") continue;
                if (!IsScalar(p.PropertyType)) continue;

                result[p.Name] = p.GetValue(det, null);
            }
            return result;
        }

        // ********************************************************************

        static bool IsScalar(Type t)
        {
            return t == typeof(int) || t == typeof(double) || t == typeof(bool)
                || t == typeof(long) || t == typeof(float);
        }

        static object ConvertValue(object raw, Type targetType)
        {
            if (raw is JValue jv) raw = jv.Value;

            if (targetType == typeof(int)) return Convert.ToInt32(raw);
            if (targetType == typeof(long)) return Convert.ToInt64(raw);
            if (targetType == typeof(double)) return Convert.ToDouble(raw);
            if (targetType == typeof(float)) return Convert.ToSingle(raw);
            if (targetType == typeof(bool)) return Convert.ToBoolean(raw);

            return Convert.ChangeType(raw, targetType);
        }
    }
}
