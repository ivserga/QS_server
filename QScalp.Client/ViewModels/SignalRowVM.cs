// ==========================================================================
//  SignalRowVM.cs — Строка в гриде сигналов одного тикера.
// ==========================================================================

using System;

using QScalp.Client.Clusters.Analytics;

namespace QScalp.Client.ViewModels
{
    public sealed class SignalRowVM
    {
        public DateTime Time { get; }
        public string TimeText => Time.ToString("HH:mm:ss");
        public string Source { get; }
        public string Kind { get; }
        public string Direction { get; }
        public int Price { get; }
        public double Strength { get; }
        public string StrengthText => Strength.ToString("F2");
        /// <summary>
        /// Дискретная категория силы сигнала: Low / Mid / High.
        /// Используется в XAML для раскрашивания ячейки силы триггерами.
        /// </summary>
        public string StrengthBucket
            => Strength >= 0.75 ? "High" : (Strength >= 0.50 ? "Mid" : "Low");

        public string Message { get; }

        internal SignalRowVM(Signal s)
        {
            Time = s.Time;
            Source = s.Source ?? string.Empty;
            Kind = s.Kind.ToString();
            Direction = s.Direction == SignalDirection.None ? "" : s.Direction.ToString();
            Price = s.Price;
            Strength = s.Strength;
            Message = s.Message ?? string.Empty;
        }
    }
}
