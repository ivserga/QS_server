// ==========================================================================
//  IntPrice.cs — Форматирование IntPrice для JSON-экспорта кластеров.
// ==========================================================================
//  Сервер и клиент используют IntPrice = raw × 1_000_000. Формат совпадает
//  с QScalp.Price в основном приложении (GetRaw / GetString).
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.Client.Clusters
{
    internal static class IntPrice
    {
        public const int DefaultRatio = 1_000_000;

        public static double GetRaw(int price, int ratio = DefaultRatio)
        {
            return (double)price / ratio;
        }

        public static string GetString(int price, int priceStep, int ratio = DefaultRatio)
        {
            int decimals = priceStep > 0
                ? Math.Max(0, (int)Math.Round(Math.Log10((double)ratio / priceStep)))
                : 2;
            return GetRaw(price, ratio).ToString("N" + decimals, CultureInfo.InvariantCulture);
        }
    }
}
