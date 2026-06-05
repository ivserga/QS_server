using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QScalp.Recorder.Recording
{
    /// <summary>Фоновая запись JSONL — приём сообщений не блокирует сеть.</summary>
    public sealed class AsyncJsonlWriter : IDisposable
    {
        private readonly string _filePath;
        private readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Task _worker;
        private StreamWriter _writer;
        private bool _disposed;

        public string FilePath => _filePath;

        public AsyncJsonlWriter(string filePath)
        {
            _filePath = filePath;
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            _writer = new StreamWriter(filePath, append: false, Encoding.UTF8);
            _worker = Task.Run(() => WorkerAsync(_cts.Token));
        }

        public void Enqueue(string line)
        {
            if (_disposed || string.IsNullOrEmpty(line)) return;
            _queue.Enqueue(line);
            _signal.Release();
        }

        private async Task WorkerAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await _signal.WaitAsync(ct);
                    FlushBatch();
                }
            }
            catch (OperationCanceledException) { }
        }

        private void FlushBatch()
        {
            if (_writer == null) return;

            int n = 0;
            while (_queue.TryDequeue(out var line))
            {
                _writer.WriteLine(line);
                n++;
            }

            if (n > 0)
                _writer.Flush();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cts.Cancel();
            try { _worker.Wait(TimeSpan.FromSeconds(5)); } catch { }

            FlushBatch();
            try { _writer?.Dispose(); } catch { }
            _writer = null;
            _cts.Dispose();
            _signal.Dispose();
        }
    }
}
