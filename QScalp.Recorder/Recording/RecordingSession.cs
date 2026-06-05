using System;
using System.IO;
using Newtonsoft.Json;
using QScalp.Recorder.Net;

namespace QScalp.Recorder.Recording
{
    public sealed class RecordingSession : IDisposable
    {
        private AsyncJsonlWriter _writer;
        private RecorderWsClient _client;
        private long _lineCount;

        public string SessionDirectory { get; private set; }
        public string Ticker { get; private set; }
        public bool IsActive { get; private set; }
        public long LineCount => _lineCount;

        public event Action<string> OnLog;
        public event Action<string> OnError;
        public event Action<long> OnLineCountChanged;

        public void Start(string serverUrl, string ticker, string outputDirectory, bool recordSnapshots)
        {
            if (IsActive)
                throw new InvalidOperationException("Запись уже идёт.");

            Ticker = ticker.Trim().ToUpperInvariant();
            var root = string.IsNullOrWhiteSpace(outputDirectory) ? "Records" : outputDirectory.Trim();
            SessionDirectory = Path.Combine(root, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            Directory.CreateDirectory(SessionDirectory);

            File.WriteAllText(Path.Combine(SessionDirectory, "_session.json"),
                JsonConvert.SerializeObject(new
                {
                    kind = "meta",
                    startedUtc = DateTime.UtcNow,
                    serverUrl,
                    tickers = new[] { Ticker },
                    formatVersion = 1
                }, Formatting.Indented));

            _lineCount = 0;
            var filePath = Path.Combine(SessionDirectory, Ticker + ".jsonl");
            _writer = new AsyncJsonlWriter(filePath);

            _client = new RecorderWsClient(serverUrl, Ticker, Ticker, recordSnapshots);
            _client.OnRecordableMessage += json =>
            {
                _writer.Enqueue(json);
                _lineCount++;
                OnLineCountChanged?.Invoke(_lineCount);
            };
            _client.OnLog += s => OnLog?.Invoke(s);
            _client.OnError += s => OnError?.Invoke(s);
            _client.Start();

            IsActive = true;
            OnLog?.Invoke($"Запись: {filePath}");
        }

        public void Stop()
        {
            if (!IsActive) return;

            try { _client?.Dispose(); } catch { }
            _client = null;

            try { _writer?.Dispose(); } catch { }
            _writer = null;

            IsActive = false;
            OnLog?.Invoke($"Остановлено. Строк: {_lineCount}. Папка: {SessionDirectory}");
        }

        public void Dispose() => Stop();
    }
}
