// ==========================================================================
//  SignalLogSink.cs — CSV-лог всех сработавших сигналов
// ==========================================================================
//
//  Формат: Time;Source;Kind;Direction;Price;Strength;Details
//  Разделитель — ';', чтобы не конфликтовать с запятой в Details.
//  Файл дописывается при первом сигнале, заголовок пишется один раз.
//
// ==========================================================================

using System;
using System.Globalization;
using System.IO;

namespace QScalp.View.ClustersSpace.Analytics.Sinks
{
  // ==========================================================================

  sealed class SignalLogSink : ISignalSink
  {
    // **********************************************************************

    readonly string filePath;
    readonly object gate = new object();
    bool headerWritten;

    // **********************************************************************

    public SignalLogSink(string filePath)
    {
      this.filePath = filePath;
    }

    // **********************************************************************

    public void Emit(Signal signal)
    {
      if(signal == null || string.IsNullOrEmpty(filePath))
        return;

      try
      {
        lock(gate)
        {
          EnsureHeader();

          using(var sw = new StreamWriter(filePath, append: true))
          {
            sw.WriteLine(string.Format(
              CultureInfo.InvariantCulture,
              "{0:yyyy-MM-dd HH:mm:ss.fff};{1};{2};{3};{4};{5:F3};{6}",
              signal.Time,
              Escape(signal.Source),
              signal.Kind,
              signal.Direction,
              signal.Price,
              signal.Strength,
              Escape(signal.Details)));
          }
        }
      }
      catch
      {
        // лог сигналов не должен ронять UI; при любой ошибке I/O молча пропускаем.
      }
    }

    // **********************************************************************

    void EnsureHeader()
    {
      if(headerWritten)
        return;

      if(!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
      {
        using(var sw = new StreamWriter(filePath, append: true))
          sw.WriteLine("Time;Source;Kind;Direction;Price;Strength;Details");
      }

      headerWritten = true;
    }

    // **********************************************************************

    static string Escape(string s)
    {
      if(string.IsNullOrEmpty(s))
        return string.Empty;

      // убираем ;, перенос строк и кавычки
      return s
        .Replace(';', ',')
        .Replace('\r', ' ')
        .Replace('\n', ' ');
    }

    // **********************************************************************
  }

  // ==========================================================================
}
