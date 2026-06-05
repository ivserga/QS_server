using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QScalp.Server.Broadcasting;
using QScalp.Shared.Models;
using QScalp.Shared.Protocol;

namespace QScalp.Server.Connector
{
    /// <summary>
    /// Воспроизводит сессию из каталога записи (файлы TICKER.jsonl) или одного .jsonl.
    /// </summary>
    public sealed class ReplayConnector : IMarketDataConnector
    {
        private readonly string _sessionPath;
        private readonly double _speed;
        private readonly bool _loop;
        private readonly Action<string> _log;

        private readonly HashSet<string> _subscribed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _subLock = new object();
        private CancellationTokenSource _cts;
        private Task _replayTask;

        public string SourceName => "Replay";

        public event Action<string, Quote[], Spread> OnQuoteReceived;
        public event Action<string, string, Trade> OnTradeReceived;

        public ReplayConnector(string sessionPath, double speed, bool loop, Action<string> log)
        {
            _sessionPath = sessionPath?.Trim();
            _speed = speed > 0 ? speed : 1.0;
            _loop = loop;
            _log = log;
        }

        public Task ConnectAsync()
        {
            if (string.IsNullOrWhiteSpace(_sessionPath) || !PathExists(_sessionPath))
                throw new InvalidOperationException(
                    $"Каталог или файл воспроизведения не найден: {_sessionPath}");

            _log?.Invoke($"Replay: источник {_sessionPath}, скорость x{_speed:0.##}, loop={_loop}");
            return Task.CompletedTask;
        }

        private static bool PathExists(string path) =>
            File.Exists(path) || Directory.Exists(path);

        public Task SubscribeTickerAsync(string ticker)
        {
            lock (_subLock)
            {
                _subscribed.Add(ticker.Trim().ToUpperInvariant());
                EnsureReplayRunning();
            }
            return Task.CompletedTask;
        }

        public Task UnsubscribeTickerAsync(string ticker)
        {
            lock (_subLock)
            {
                _subscribed.Remove(ticker.Trim().ToUpperInvariant());
            }
            return Task.CompletedTask;
        }

        public Task LoadSnapshotAsync(string ticker, TickerBuffer buffer) =>
            Task.CompletedTask;

        private void EnsureReplayRunning()
        {
            if (_replayTask != null && !_replayTask.IsCompleted)
                return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _replayTask = Task.Run(() => ReplayLoopAsync(_cts.Token));
        }

        private async Task ReplayLoopAsync(CancellationToken ct)
        {
            do
            {
                List<string> tickers;
                lock (_subLock)
                    tickers = _subscribed.ToList();

                if (tickers.Count == 0)
                {
                    await Task.Delay(200, ct);
                    continue;
                }

                var events = LoadEvents(tickers);
                if (events.Count == 0)
                {
                    _log?.Invoke("Replay: нет событий для выбранных тикеров.");
                    if (!_loop) break;
                    await Task.Delay(1000, ct);
                    continue;
                }

                _log?.Invoke($"Replay: {events.Count} событий ({string.Join(", ", tickers)})");

                long prevTs = events[0].Timestamp;
                foreach (var ev in events)
                {
                    ct.ThrowIfCancellationRequested();

                    lock (_subLock)
                    {
                        if (!_subscribed.Contains(ev.Ticker))
                            continue;
                    }

                    if (ev.Timestamp > prevTs)
                    {
                        var delayMs = (int)((ev.Timestamp - prevTs) / _speed);
                        if (delayMs > 0)
                            await Task.Delay(Math.Min(delayMs, 60_000), ct);
                    }
                    prevTs = ev.Timestamp;

                    Dispatch(ev);
                }

                _log?.Invoke("Replay: проход завершён.");
            }
            while (_loop && !ct.IsCancellationRequested);
        }

        private void Dispatch(ReplayEvent ev)
        {
            switch (ev.Type)
            {
                case ServerMessageType.StockUpdate:
                    var stock = ProtocolHelper.DeserializePayload<StockUpdatePayload>(ev.PayloadJson);
                    OnQuoteReceived?.Invoke(ev.Ticker, stock.Quotes, stock.Spread);
                    break;
                case ServerMessageType.TradeUpdate:
                    var trade = ProtocolHelper.DeserializePayload<TradeUpdatePayload>(ev.PayloadJson);
                    OnTradeReceived?.Invoke(ev.Ticker, trade.SecKey, trade.Trade);
                    break;
            }
        }

        private List<ReplayEvent> LoadEvents(IEnumerable<string> tickers)
        {
            var files = ResolveTickerFiles(_sessionPath, tickers);
            var list = new List<ReplayEvent>();

            foreach (var path in files)
            {
                var ticker = Path.GetFileNameWithoutExtension(path);
                foreach (var line in File.ReadLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    ServerMessage msg;
                    try { msg = ProtocolHelper.DeserializeServerMessage(line); }
                    catch { continue; }

                    if (msg == null) continue;
                    if (msg.Type != ServerMessageType.StockUpdate &&
                        msg.Type != ServerMessageType.TradeUpdate)
                        continue;

                    list.Add(new ReplayEvent
                    {
                        Ticker = msg.Ticker ?? ticker,
                        Type = msg.Type,
                        PayloadJson = msg.Payload,
                        Timestamp = msg.Timestamp
                    });
                }
            }

            list.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            return list;
        }

        private static IEnumerable<string> ResolveTickerFiles(string sessionPath, IEnumerable<string> tickers)
        {
            if (File.Exists(sessionPath))
                return new[] { sessionPath };

            var dir = sessionPath;
            var result = new List<string>();
            foreach (var t in tickers)
            {
                var exact = Path.Combine(dir, t + ".jsonl");
                if (File.Exists(exact))
                {
                    result.Add(exact);
                    continue;
                }
                var any = Directory.GetFiles(dir, t + ".jsonl", SearchOption.TopDirectoryOnly)
                    .Concat(Directory.GetFiles(dir, "*.jsonl", SearchOption.TopDirectoryOnly)
                        .Where(f => string.Equals(Path.GetFileNameWithoutExtension(f), t,
                            StringComparison.OrdinalIgnoreCase)))
                    .FirstOrDefault();
                if (any != null)
                    result.Add(any);
            }
            return result.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            _cts?.Dispose();
        }

        private struct ReplayEvent
        {
            public string Ticker;
            public ServerMessageType Type;
            public string PayloadJson;
            public long Timestamp;
        }
    }
}
