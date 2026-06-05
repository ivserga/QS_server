using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using QScalp.Server.Broadcasting;
using QScalp.Server.Config;
using QScalp.Shared.Models;

namespace QScalp.Server.Connector
{
    /// <summary>
    /// Источник данных через локальный WebSocket API Medved Trader / Striker.
    /// Подключается к ws://127.0.0.1:&lt;port&gt;, авторизуется паролем, стартует
    /// нужный data streamer и подписывается на L1. Один L1Update приносит и
    /// bid/ask, и факт сделки (price+size). Сделки сразу транслируются в
    /// OnTradeReceived, спред — в OnQuoteReceived.
    /// </summary>
    public sealed class StrikerWsConnector : IMarketDataConnector
    {
        private const int RawPriceFactor = 1_000_000;

        private readonly ServerConfig _config;
        private readonly Action<string> _log;

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private Task _receiveTask;
        private readonly object _sendLock = new object();

        private string _streamerId;
        private long _reqIdCounter;

        private readonly ConcurrentDictionary<string, bool> _subscribed
            = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, SpreadState> _lastSpreads
            = new ConcurrentDictionary<string, SpreadState>(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> _pending
            = new ConcurrentDictionary<string, TaskCompletionSource<JObject>>(StringComparer.Ordinal);

        private TaskCompletionSource<bool> _streamerReadyTcs;

        public string SourceName => "Striker WS";

        public event Action<string, Quote[], Spread> OnQuoteReceived;
        public event Action<string, string, Trade> OnTradeReceived;

        // ********************************************************************

        public StrikerWsConnector(ServerConfig config, Action<string> log)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        // ********************************************************************

        public async Task ConnectAsync()
        {
            var url = _config.StrikerWsBaseUrl?.Trim();
            if (string.IsNullOrEmpty(url))
                throw new InvalidOperationException("StrikerWsBaseUrl пустой (например ws://127.0.0.1:9700).");

            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();

            try
            {
                await _ws.ConnectAsync(new Uri(url), _cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Не удалось подключиться к Striker WS API ({url}). Включён ли WS API в настройках Striker и совпадает ли порт? {ex.Message}", ex);
            }

            _receiveTask = Task.Run(ReceiveLoopAsync);

            // 1. Логин
            var connectResp = await SendCommandAsync(new
            {
                cmd = "connect",
                pwd = _config.StrikerWsPassword ?? string.Empty
            }, TimeSpan.FromSeconds(15)).ConfigureAwait(false);

            if (connectResp.Value<string>("success") != "OK")
            {
                var reason = connectResp["result"]?.Value<string>("reason") ?? connectResp["result"]?.ToString();
                throw new InvalidOperationException("Striker WS connect отклонён: " + reason);
            }
            _log($"[StrikerWS] connect OK ({connectResp["result"]?["ver"]?.ToString()})");

            // 2. Определяем streamerID
            _streamerId = _config.StrikerWsStreamerId?.Trim();
            if (string.IsNullOrEmpty(_streamerId))
            {
                var enumResp = await SendCommandAsync(new { cmd = "enumDataStreamers" },
                    TimeSpan.FromSeconds(10)).ConfigureAwait(false);

                var arr = enumResp["result"] as JArray;
                if (arr != null)
                {
                    foreach (var s in arr)
                    {
                        var name = s.Value<string>("name");
                        var sid = s.Value<string>("streamerID");
                        var isSet = s.Value<bool?>("isSet") ?? false;
                        _log($"[StrikerWS] streamer: {sid,-12} {name,-30} isSet={isSet}");
                        if (string.IsNullOrEmpty(_streamerId) && isSet)
                            _streamerId = sid;
                    }
                }

                if (string.IsNullOrEmpty(_streamerId))
                    throw new InvalidOperationException(
                        "В Striker нет настроенных data streamers (isSet=true). Зайдите в Striker → настройте источник котировок.");
                _log($"[StrikerWS] выбран streamer: {_streamerId}");
            }

            // 3. Стартуем стример
            _streamerReadyTcs = new TaskCompletionSource<bool>();
            var startResp = await SendCommandAsync(new
            {
                cmd = "startDataStreamer",
                streamerID = _streamerId
            }, TimeSpan.FromSeconds(15)).ConfigureAwait(false);

            if (startResp.Value<string>("success") != "OK")
            {
                var reason = startResp["result"]?.ToString();
                throw new InvalidOperationException("startDataStreamer отклонён: " + reason);
            }

            // Если уже работал — стейт мы можем не получить, считаем готовым по success=OK.
            // Если "starting" — ждём dataStreamerStateUpdate с IdleOrStreaming.
            var resultText = startResp["result"]?.ToString() ?? string.Empty;
            if (resultText.IndexOf("starting", StringComparison.OrdinalIgnoreCase) < 0)
            {
                _streamerReadyTcs.TrySetResult(true);
            }

            var ready = await Task.WhenAny(
                _streamerReadyTcs.Task,
                Task.Delay(TimeSpan.FromSeconds(30), _cts.Token)).ConfigureAwait(false);

            if (ready != _streamerReadyTcs.Task)
                throw new InvalidOperationException("Streamer не вышел в IdleOrStreaming за 30 сек.");

            _log($"[StrikerWS] streamer {_streamerId} готов.");
        }

        // ********************************************************************

        public async Task SubscribeTickerAsync(string ticker)
        {
            if (string.IsNullOrWhiteSpace(ticker)) return;
            var t = ticker.Trim().ToUpperInvariant();
            if (!_subscribed.TryAdd(t, true)) return;

            try
            {
                await SendCommandAsync(new
                {
                    cmd = "subscribe",
                    streamerID = _streamerId,
                    sub = "L1",
                    sym = new[] { t }
                }, TimeSpan.FromSeconds(10)).ConfigureAwait(false);

                _log($"[StrikerWS] subscribe L1: {t}");
            }
            catch (Exception ex)
            {
                _subscribed.TryRemove(t, out _);
                _log($"[StrikerWS] subscribe {t} ошибка: {ex.Message}");
            }
        }

        // ********************************************************************

        public async Task UnsubscribeTickerAsync(string ticker)
        {
            if (string.IsNullOrWhiteSpace(ticker)) return;
            var t = ticker.Trim().ToUpperInvariant();
            _subscribed.TryRemove(t, out _);
            _lastSpreads.TryRemove(t, out _);

            if (_ws?.State != WebSocketState.Open) return;

            try
            {
                await SendCommandAsync(new
                {
                    cmd = "unsubscribe",
                    streamerID = _streamerId,
                    sub = "L1",
                    sym = new[] { t }
                }, TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                _log($"[StrikerWS] unsubscribe: {t}");
            }
            catch (Exception ex)
            {
                _log($"[StrikerWS] unsubscribe {t} ошибка: {ex.Message}");
            }
        }

        // ********************************************************************

        public Task LoadSnapshotAsync(string ticker, TickerBuffer buffer)
        {
            // Через WS API можно дёрнуть getHistoricalData, но это отдельная задача.
            _log($"[StrikerWS] LoadSnapshot для {ticker} пропущен (не реализован).");
            return Task.CompletedTask;
        }

        // ********************************************************************

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }

            try
            {
                if (_ws?.State == WebSocketState.Open)
                {
                    _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "stop", CancellationToken.None)
                        .Wait(TimeSpan.FromSeconds(2));
                }
            }
            catch { /* ignore */ }

            try { _ws?.Dispose(); } catch { }
            try { _cts?.Dispose(); } catch { }

            foreach (var kv in _pending)
                kv.Value.TrySetCanceled();
            _pending.Clear();
        }

        // ====================================================================
        // Низкоуровневые методы
        // ====================================================================

        private async Task<JObject> SendCommandAsync(object body, TimeSpan timeout)
        {
            var reqId = System.Threading.Interlocked.Increment(ref _reqIdCounter).ToString();
            var jo = JObject.FromObject(body);
            jo["reqID"] = reqId;

            var tcs = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[reqId] = tcs;

            try
            {
                await SendRawAsync(jo.ToString(Formatting.None)).ConfigureAwait(false);

                var winner = await Task.WhenAny(tcs.Task, Task.Delay(timeout, _cts.Token)).ConfigureAwait(false);
                if (winner != tcs.Task)
                    throw new TimeoutException($"WS таймаут для cmd={jo.Value<string>("cmd")}");

                return tcs.Task.Result;
            }
            finally
            {
                _pending.TryRemove(reqId, out _);
            }
        }

        private async Task SendRawAsync(string json)
        {
            if (_ws == null || _ws.State != WebSocketState.Open)
                throw new InvalidOperationException("WS не открыт.");

            if (_config.DebugMode)
                _log($"[StrikerWS] >> {Trim(json, 300)}");

            var bytes = Encoding.UTF8.GetBytes(json);
            // ClientWebSocket.SendAsync не реентерабельный — сериализуем
            // через семафор/локу на отдельном Task.
            await Task.Run(async () =>
            {
                bool taken = false;
                try
                {
                    System.Threading.Monitor.Enter(_sendLock, ref taken);
                    await _ws.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        true,
                        _cts.Token).ConfigureAwait(false);
                }
                finally
                {
                    if (taken) System.Threading.Monitor.Exit(_sendLock);
                }
            }).ConfigureAwait(false);
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[16 * 1024];
            var sb = new StringBuilder();
            try
            {
                while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    var r = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token)
                        .ConfigureAwait(false);

                    if (r.MessageType == WebSocketMessageType.Close) break;
                    if (r.MessageType != WebSocketMessageType.Text)
                        continue;

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
                    if (!r.EndOfMessage) continue;

                    var msg = sb.ToString();
                    sb.Clear();
                    HandleIncoming(msg);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _log($"[StrikerWS] receive loop: {ex.Message}");
            }
        }

        private void HandleIncoming(string json)
        {
            if (_config.DebugMode)
                _log($"[StrikerWS] << {Trim(json, 400)}");

            JObject obj;
            try { obj = JObject.Parse(json); }
            catch { return; }

            // Если пришёл ответ на команду — у него есть reqID, мы его ждём.
            var reqId = obj.Value<string>("reqID");
            if (!string.IsNullOrEmpty(reqId) &&
                _pending.TryGetValue(reqId, out var tcs))
            {
                tcs.TrySetResult(obj);
                // не return: некоторые "ответы" могут совпадать с потоковыми
                // обновлениями; в API это разделено по cmd.
            }

            var cmd = obj.Value<string>("cmd");
            switch (cmd)
            {
                case "L1Update":
                    ProcessL1Update(obj["result"] as JObject);
                    break;

                case "dataStreamerStateUpdate":
                    var state = (obj["result"] as JObject)?.Value<string>("state");
                    if (string.Equals(state, "IdleOrStreaming", StringComparison.OrdinalIgnoreCase))
                        _streamerReadyTcs?.TrySetResult(true);
                    break;

                case "dataStreamerStatusUpdate":
                    if (_config.DebugMode)
                        _log($"[StrikerWS] streamer status: {obj["result"]}");
                    break;

                case "closing":
                    _log("[StrikerWS] Striker сообщает о закрытии приложения.");
                    break;
            }
        }

        private void ProcessL1Update(JObject r)
        {
            if (r == null) return;

            var sym = r.Value<string>("sym");
            if (string.IsNullOrEmpty(sym)) return;
            sym = sym.ToUpperInvariant();

            if (!_subscribed.ContainsKey(sym)) return;

            double bid = r.Value<double?>("bid") ?? double.NaN;
            double ask = r.Value<double?>("ask") ?? double.NaN;
            double price = r.Value<double?>("price") ?? double.NaN;
            double size = r.Value<double?>("size") ?? 0d;
            DateTime ts = r.Value<DateTime?>("timeStamp") ?? DateTime.UtcNow;
            ts = DateTime.SpecifyKind(ts, DateTimeKind.Utc);

            // 1. Спред / L1
            if (!double.IsNaN(bid) && !double.IsNaN(ask) && bid > 0 && ask > 0)
            {
                int bidP = RawToInt(bid);
                int askP = RawToInt(ask);

                _lastSpreads[sym] = new SpreadState { RawBid = bid, RawAsk = ask };

                var quotes = new[]
                {
                    new Quote(askP, 0, QuoteType.BestAsk),
                    new Quote(bidP, 0, QuoteType.BestBid)
                };
                OnQuoteReceived?.Invoke(sym, quotes, new Spread(askP, bidP));
            }

            // 2. Сделка (size > 0 — это принт)
            if (size > 0d && !double.IsNaN(price) && price > 0d)
            {
                var op = TradeOp.Wait;
                if (_lastSpreads.TryGetValue(sym, out var sp))
                {
                    if (price >= sp.RawAsk) op = TradeOp.Buy;
                    else if (price <= sp.RawBid) op = TradeOp.Sell;
                }

                var trade = new Trade
                {
                    RawPrice = price,
                    IntPrice = RawToInt(price),
                    Quantity = (int)Math.Round(size),
                    Op = op,
                    DateTime = ts
                };
                OnTradeReceived?.Invoke(sym, sym, trade);
            }
        }

        // ********************************************************************

        private static int RawToInt(double raw) =>
            (int)Math.Round(raw * RawPriceFactor);

        private static string Trim(string s, int n) =>
            s == null ? null : (s.Length > n ? s.Substring(0, n) + "…" : s);

        private struct SpreadState
        {
            public double RawBid;
            public double RawAsk;
        }
    }
}
