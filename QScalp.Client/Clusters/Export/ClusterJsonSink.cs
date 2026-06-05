// ==========================================================================
//  ClusterJsonSink.cs — Потоковая запись закрытых кластеров в JSONL.
// ==========================================================================
//  Каждая строка — один закрытый кластер в формате, совместимом с WPF
//  ClusterExporter (schema qscalp.cluster_export.v1). Первая строка файла —
//  заголовок сессии с настройками инструмента.
// ==========================================================================

using System;
using System.IO;

using QScalp.Client.Config;

namespace QScalp.Client.Clusters.Export
{
    internal sealed class ClusterJsonSink : IDisposable
    {
        readonly string _filePath;
        readonly TickerConfig _config;
        readonly Action<string> _onError;
        readonly object _gate = new object();
        int _index;
        bool _sessionWritten;

        public string FilePath => _filePath;
        public int ExportedCount { get; private set; }

        public ClusterJsonSink(TickerConfig config, string filePath, Action<string> onError = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _onError = onError;
        }

        public static string BuildDefaultPath(TickerConfig config)
        {
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "clusters");
            Directory.CreateDirectory(dir);
            var safeTicker = MakeSafeFilename(config.Ticker);
            return Path.Combine(dir, $"clusters_{safeTicker}.jsonl");
        }

        public void BeginSession(bool truncateFile)
        {
            lock (_gate)
            {
                _index = 0;
                ExportedCount = 0;
                _sessionWritten = false;

                if (truncateFile && File.Exists(_filePath))
                {
                    try { File.Delete(_filePath); }
                    catch (Exception ex) { ReportError("cannot truncate file: " + ex.Message); }
                }
            }
        }

        public void OnClusterClosed(Cluster cluster)
        {
            if (cluster == null || string.IsNullOrEmpty(_filePath)) return;

            try
            {
                lock (_gate)
                {
                    EnsureSessionHeader();
                    _index++;
                    var line = ClusterExporter.SerializeClusterLine(_index, cluster, _config);
                    File.AppendAllText(_filePath, line + Environment.NewLine);
                    ExportedCount++;
                }
            }
            catch (Exception ex)
            {
                ReportError(ex.Message);
            }
        }

        void EnsureSessionHeader()
        {
            if (_sessionWritten) return;

            var header = ClusterExporter.SerializeSessionHeader(_config);
            var exists = File.Exists(_filePath) && new FileInfo(_filePath).Length > 0;
            if (!exists)
                File.AppendAllText(_filePath, header + Environment.NewLine);

            _sessionWritten = true;
        }

        static string MakeSafeFilename(string s)
        {
            if (string.IsNullOrEmpty(s)) return "ticker";
            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s;
        }

        void ReportError(string message)
        {
            try { _onError?.Invoke(message); } catch { }
        }

        public void Dispose() { }
    }
}
