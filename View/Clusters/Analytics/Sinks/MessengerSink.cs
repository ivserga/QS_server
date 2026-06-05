// ==========================================================================
//  MessengerSink.cs — Вывод сигналов в экранный Messenger (MsgQueue)
// ==========================================================================
//
//  Формат выводимого сообщения:
//    "[★★★☆☆ 0.62] <текст сигнала из детектора>"
//  Звёздочки — это визуализация Signal.Strength [0..1] в 5 делений, чтобы
//  пользователь сразу видел силу паттерна не вчитываясь в число.
//
//  На экран не выводятся сигналы со Strength < MinMessengerStrength (0.6),
//  чтобы слабые паттерны не отвлекали; полный журнал по-прежнему в CSV (SignalLogSink).
//
// ==========================================================================

using System;
using System.Globalization;
using QScalp.View;

namespace QScalp.View.ClustersSpace.Analytics.Sinks
{
  // ==========================================================================

  sealed class MessengerSink : ISignalSink
  {
    /// <summary>Порог силы паттерна: ниже этого значения сообщение не показывается в Messenger.</summary>
    const double MinMessengerStrength = 0.6;

    readonly ViewManager vmgr;

    public MessengerSink(ViewManager vmgr)
    {
      this.vmgr = vmgr;
    }

    public void Emit(Signal signal)
    {
      if(signal == null || string.IsNullOrEmpty(signal.Message))
        return;

      if(signal.Strength < MinMessengerStrength)
        return;

      string prefix = FormatStrength(signal.Strength);
      vmgr.MsgQueue.Enqueue(new Message(prefix + " " + signal.Message));
    }

    static string FormatStrength(double strength)
    {
      if(strength < 0) strength = 0;
      if(strength > 1) strength = 1;

      const int slots = 5;
      int filled = (int)Math.Round(strength * slots);
      if(filled < 0) filled = 0;
      if(filled > slots) filled = slots;

      // ★ U+2605 (закрашенная), ☆ U+2606 (полая)
      string stars = new string('\u2605', filled) + new string('\u2606', slots - filled);

      return string.Format(CultureInfo.InvariantCulture, "[{0} {1:F2}]", stars, strength);
    }
  }

  // ==========================================================================
}
