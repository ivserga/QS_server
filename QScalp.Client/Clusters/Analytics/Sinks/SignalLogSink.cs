// ==========================================================================
//  SignalLogSink.cs — CSV-лог всех сработавших сигналов
// ==========================================================================
//  Headless-порт оригинала из QScalp.View.ClustersSpace.Analytics.Sinks.
//  Формат:  Time;Source;Kind;Direction;Price;Strength;Details
// ==========================================================================

using System.Globalization;
using System.IO;

namespace QScalp.Client.Clusters.Analytics.Sinks
{
    internal sealed class SignalLogSink : ISignalSink
    {
        readonly string _filePath;
        readonly object _gate = new object();
        bool _headerWritten;

        public SignalLogSink(string filePath)
        {
            _filePath = filePath;
        }

        public void Emit(Signal signal)
        {
            if (signal == null || string.IsNullOrEmpty(_filePath)) return;

            try
            {
                lock (_gate)
                {
                    EnsureHeader();
                    using (var sw = new StreamWriter(_filePath, append: true))
                    {
                        sw.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0:yyyy-MM-dd HH:mm:ss.fff};{1};{2};{3};{4};{5:F3};{6}",
                            signal.Time, Escape(signal.Source),
                            signal.Kind, signal.Direction,
                            signal.Price, signal.Strength,
                            Escape(signal.Details)));
                    }
                }
            }
            catch
            {
                // лог сигналов не должен ронять UI
            }
        }

        void EnsureHeader()
        {
            if (_headerWritten) return;

            if (!File.Exists(_filePath) || new FileInfo(_filePath).Length == 0)
            {
                using (var sw = new StreamWriter(_filePath, append: true))
                    sw.WriteLine("Time;Source;Kind;Direction;Price;Strength;Details");
            }
            _headerWritten = true;
        }

        static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}
