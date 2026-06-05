// ==========================================================================
//  ActionSink.cs — Универсальный sink, делегирующий в callback.
// ==========================================================================
//  Используется ViewModel'ями ткикеров: при срабатывании сигнала надо
//  опубликовать его в UI через Dispatcher → ObservableCollection.
// ==========================================================================

using System;

namespace QScalp.Client.Clusters.Analytics.Sinks
{
    internal sealed class ActionSink : ISignalSink
    {
        readonly Action<Signal> _action;

        public ActionSink(Action<Signal> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public void Emit(Signal signal)
        {
            if (signal == null) return;
            _action(signal);
        }
    }
}
