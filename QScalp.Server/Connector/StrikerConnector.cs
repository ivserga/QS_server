using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
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
    /// Данные из Striker через локальный HTTP API (GETLASTQUOTE = Level I,
    /// опционально GETTIMESALES = лента сделок). Режим опроса.
    /// </summary>
    public sealed class StrikerConnector : IMarketDataConnector, ITradeTicketConnector
    {
        private const int RawPriceFactor = 1_000_000;

        private readonly ServerConfig _config;
        private readonly Action<string> _log;
        private readonly HttpClient _http;
        private readonly ConcurrentDictionary<string, bool> _activeTickers
            = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, SpreadState> _lastSpreads
            = new ConcurrentDictionary<string, SpreadState>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Дедуп ленты сделок: т.к. GETTIMESALES отдаёт время с точностью до
        /// секунды, разные сделки одной секунды нельзя различать по timestamp.
        /// Поэтому для каждой пары (тикер, секунда) храним сколько строк уже
        /// отдано наружу. На следующем опросе пропускаем ровно столько первых
        /// строк этой секунды и эмитим только хвост.
        /// </summary>
        private readonly ConcurrentDictionary<string, TimeSalesCursor> _tsCursor
            = new ConcurrentDictionary<string, TimeSalesCursor>(StringComparer.OrdinalIgnoreCase);

        private CancellationTokenSource _cts;
        private Task _pollLoop;

        public string SourceName => "Striker HTTP";

        public event Action<string, Quote[], Spread> OnQuoteReceived;
        public event Action<string, string, Trade> OnTradeReceived;

        public StrikerConnector(ServerConfig config, Action<string> log)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        public async Task ConnectAsync()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            var baseUrl = _config.StrikerHttpBaseUrl?.Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrl))
                throw new InvalidOperationException("StrikerHttpBaseUrl пустой.");

            try
            {
                var ver = await HttpGetPlainAsync(baseUrl, "GETVERSION").ConfigureAwait(false);
                if (_config.DebugMode)
                    _log($"[Striker] GETVERSION: {ver.Replace("\r\n", " | ")}");
                if (!ver.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
                    _log($"[Striker] Предупреждение: неожиданный ответ GETVERSION: {ver}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Не удалось связаться со Striker по {baseUrl}. Включён ли HTTP API и запущен терминал? {ex.Message}", ex);
            }

            _pollLoop = Task.Run(() => PollLoopAsync(_cts.Token), _cts.Token);
            _log("Подключён к Striker (HTTP polling).");
        }

        public Task SubscribeTickerAsync(string ticker)
        {
            if (string.IsNullOrWhiteSpace(ticker)) return Task.CompletedTask;
            _activeTickers[ticker.Trim().ToUpperInvariant()] = true;
            _log($"[Striker] Подписка (polling): {ticker}");
            return Task.CompletedTask;
        }

        public Task UnsubscribeTickerAsync(string ticker)
        {
            if (string.IsNullOrWhiteSpace(ticker)) return Task.CompletedTask;
            _activeTickers.TryRemove(ticker.Trim().ToUpperInvariant(), out _);
            _lastSpreads.TryRemove(ticker, out _);
            _tsCursor.TryRemove(ticker, out _);
            _log($"[Striker] Отписка: {ticker}");
            return Task.CompletedTask;
        }

        public Task LoadSnapshotAsync(string ticker, TickerBuffer buffer)
        {
            _log($"[Striker] LoadSnapshot для {ticker} пропущен (REST-снапшот у Striker HTTP API не реализован).");
            return Task.CompletedTask;
        }

        public async Task FillTradeTicketAsync(
            string ticker,
            string side,
            int quantity,
            double signalStrength,
            string signalMessage)
        {
            var baseUrl = _config.StrikerHttpBaseUrl?.Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrl))
                throw new InvalidOperationException("StrikerHttpBaseUrl пустой.");

            if (string.IsNullOrWhiteSpace(ticker))
                throw new ArgumentException("ticker пустой.", nameof(ticker));
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), "quantity должен быть > 0.");

            var normalizedSide = string.Equals(side, "Sell", StringComparison.OrdinalIgnoreCase)
                ? "Sell"
                : "Buy";

            var action = normalizedSide == "Sell" ? 2 : 1; // MT.Trading.OrderAction: Buy=1, Sell=2
            var symbol = ticker.Trim().ToUpperInvariant();

            // FILLTRADETICKET принимает JSON MT.Trading.Order. Заполняем простой
            // market day order с одной ногой. Striker только открывает Trade Ticket;
            // реальная отправка заявки остаётся за пользователем в терминале.
            var order = new JObject
            {
                ["CommandType"] = 1, // MT.Trading.TradeCommandType.NewOrder
                ["Symbol"] = symbol,
                ["CommandFromJSONPaste"] = "QScalp.Client",
                ["XMLlegs"] = new JArray(
                    new JObject
                    {
                        ["Symbol"] = symbol,
                        ["Quantity"] = quantity,
                        ["Action"] = action,
                        ["OrderType"] = 1, // MT.Trading.OrderType.Market
                        ["TIF"] = 1        // MT.Trading.OrderTimeInForce.Day
                    })
            };

            var url = $"{baseUrl}/REQ?{Uri.EscapeDataString("FILLTRADETICKET")}";
            var json = order.ToString(Formatting.None);
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var resp = await _http.PostAsync(url, content).ConfigureAwait(false))
            {
                var text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException($"HTTP {(int)resp.StatusCode}: {text}");
                if (!text.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(text);
            }

            _log($"[Striker] Trade Ticket заполнен: {normalizedSide} {quantity} {symbol} " +
                 $"(signal={signalStrength:0.###}, {signalMessage})");
        }

        public void Dispose()
        {
            try
            {
                _cts?.Cancel();
                _pollLoop?.Wait(TimeSpan.FromSeconds(3));
            }
            catch { /* ignore */ }
            _cts?.Dispose();
            _http.Dispose();
        }

        // --------------------------------------------------------------------

        private async Task PollLoopAsync(CancellationToken ct)
        {
            var est = GetEasternTimeZone();
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var tickers = _activeTickers.Keys.ToArray();
                    if (tickers.Length > 0)
                    {
                        await PollQuotesAsync(tickers, ct).ConfigureAwait(false);
                        if (_config.StrikerEnableTimeAndSales)
                            await PollTimeSalesAsync(tickers, est, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _log($"[Striker] Ошибка цикла опроса: {ex.Message}");
                }

                try
                {
                    await Task.Delay(Math.Max(50, _config.StrikerQuotePollMs), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task PollQuotesAsync(string[] tickers, CancellationToken ct)
        {
            var baseUrl = _config.StrikerHttpBaseUrl.Trim().TrimEnd('/');
            var joined = string.Join(",", tickers.Select(t => t.Trim().ToUpperInvariant()));
            var query = $"GETLASTQUOTE({joined})";
            var body = await HttpGetPlainAsync(baseUrl, query, ct).ConfigureAwait(false);

            foreach (var line in ParseOkBodyLines(body))
            {
                var parsed = ParseLastQuoteLine(line);
                if (parsed == null) continue;
                var (sym, quotes, spread) = parsed.Value;
                if (!_activeTickers.ContainsKey(sym)) continue;
                OnQuoteReceived?.Invoke(sym, quotes, spread);
            }
        }

        private async Task PollTimeSalesAsync(string[] tickers, TimeZoneInfo est, CancellationToken ct)
        {
            var baseUrl = _config.StrikerHttpBaseUrl.Trim().TrimEnd('/');
            var nowUtc = DateTime.UtcNow;
            var nowEastern = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, est);

            foreach (var ticker in tickers)
            {
                if (ct.IsCancellationRequested) break;

                var end = nowEastern;
                var cursor = _tsCursor.GetOrAdd(ticker, _ => new TimeSalesCursor());

                // Окно опроса: с запасом, перекрывающим возможную задержку записи в
                // tick-store Striker. Если состояние "холодное" — берём 30 сек назад,
                // чтобы стартовать без всплеска истории; иначе — 5 сек до последней
                // увиденной секунды (этого хватает для отлова поздно записавшихся
                // тиков той же или соседних секунд).
                DateTime start;
                if (cursor.LastSecondEastern == DateTime.MinValue)
                    start = end.AddSeconds(-30);
                else
                    start = cursor.LastSecondEastern.AddSeconds(-5);

                if ((end - start).TotalHours > 6)
                    start = end.AddHours(-6);

                var startS = start.ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                var endS = end.ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                var q = $"GETTIMESALES({ticker},{startS},{endS})";
                string body;
                try
                {
                    body = await HttpGetPlainAsync(baseUrl, q, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (_config.DebugMode)
                        _log($"[Striker] GETTIMESALES {ticker}: {ex.Message}");
                    continue;
                }

                EmitNewTrades(ticker, body, est, cursor);
            }
        }

        /// <summary>
        /// API возвращает CSV-строки в порядке хранения, тиковый стор Striker
        /// упорядочен по времени → строки в одной секунде идут в стабильном
        /// порядке. Дедуп — позиционный по каждой секунде:
        ///   - группируем строки по полю секунды;
        ///   - для каждой секунды пропускаем `cursor.EmittedAt[second]` первых
        ///     строк и эмитим хвост, после чего обновляем счётчик до общего
        ///     числа строк, увиденных в этой секунде;
        ///   - для секунд старше «безопасного» порога (мы их перекрывали
        ///     start-окном) тоже работает корректно — просто 0 новых.
        /// </summary>
        private void EmitNewTrades(string ticker, string body, TimeZoneInfo est, TimeSalesCursor cursor)
        {
            var perSecond = new SortedDictionary<DateTime, List<Trade>>();
            foreach (var line in ParseOkBodyLines(body))
            {
                if (!TryParseTimeSalesLine(line, est, out var lineTimeEst, out var trade))
                    continue;
                if (!perSecond.TryGetValue(lineTimeEst, out var list))
                {
                    list = new List<Trade>();
                    perSecond[lineTimeEst] = list;
                }
                list.Add(trade);
            }

            // Сбрасываем устаревшие счётчики (всё, что старше окна перекрытия —
            // нам уже не интересно, чтобы словарь не рос вечно).
            if (perSecond.Count > 0)
            {
                var oldestSeen = perSecond.Keys.First();
                cursor.TrimOlderThan(oldestSeen.AddSeconds(-2));
            }

            DateTime newLastSecond = cursor.LastSecondEastern;

            foreach (var kv in perSecond)
            {
                var second = kv.Key;
                var trades = kv.Value;

                int already = cursor.GetEmittedCount(second);
                if (trades.Count <= already) continue;

                for (int i = already; i < trades.Count; i++)
                    OnTradeReceived?.Invoke(ticker, ticker, trades[i]);

                cursor.SetEmittedCount(second, trades.Count);
                if (second > newLastSecond) newLastSecond = second;
            }

            cursor.LastSecondEastern = newLastSecond;
        }

        private async Task<string> HttpGetPlainAsync(string baseUrl, string reqQuery, CancellationToken ct = default)
        {
            var enc = Uri.EscapeDataString(reqQuery);
            var uri = $"{baseUrl}/REQ?{enc}";
            using (var resp = await _http.GetAsync(uri, ct).ConfigureAwait(false))
            {
                var text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException($"HTTP {(int)resp.StatusCode}: {text}");
                return text;
            }
        }

        private static IEnumerable<string> ParseOkBodyLines(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) yield break;
            var lines = body.Replace("\r\n", "\n").Split('\n');
            var i = 0;
            if (lines.Length > 0 && lines[0].Trim().Equals("OK", StringComparison.OrdinalIgnoreCase))
                i = 1;
            for (; i < lines.Length; i++)
            {
                var line = lines[i]?.Trim();
                if (!string.IsNullOrEmpty(line))
                    yield return line;
            }
        }

        /// <summary>
        /// Строка: SYMBOL,MM/dd/yyyy,HH:mm:ss,last,bid,ask,change,unknown,volume,high,low,bidSize,askSize,lastVol,...
        /// </summary>
        private (string sym, Quote[] quotes, Spread spread)? ParseLastQuoteLine(string line)
        {
            var parts = line.Split(',');
            if (parts.Length < 13) return null;

            var sym = parts[0].Trim().ToUpperInvariant();
            if (!DateTime.TryParseExact(
                    parts[1] + " " + parts[2], "MM/dd/yyyy HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                return null;

            double last, bid, ask;
            if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out last)) return null;
            if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out bid)) return null;
            if (!double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out ask)) return null;

            // Striker отдаёт BidSize/AskSize как Single (float). int.TryParse на
            // строке вида "100.0" / "2.5" вернёт false и обнулит размер — это и
            // выглядело как «нет количества у bid». Парсим как double и округляем.
            double bidSzD, askSzD;
            if (!double.TryParse(parts[11], NumberStyles.Float, CultureInfo.InvariantCulture, out bidSzD)) bidSzD = 0;
            if (!double.TryParse(parts[12], NumberStyles.Float, CultureInfo.InvariantCulture, out askSzD)) askSzD = 0;
            int bidSz = (int)Math.Round(bidSzD);
            int askSz = (int)Math.Round(askSzD);

            _lastSpreads[sym] = new SpreadState { RawAsk = ask, RawBid = bid };

            int askP = RawToTransport(ask);
            int bidP = RawToTransport(bid);
            var quotes = new[]
            {
                new Quote(askP, askSz, QuoteType.BestAsk),
                new Quote(bidP, bidSz, QuoteType.BestBid)
            };
            var spread = new Spread(askP, bidP);
            return (sym, quotes, spread);
        }

        /// <summary>
        /// Строка: MM/dd/yyyy,HH:mm:ss,price,bid,ask,size
        /// </summary>
        private static bool TryParseTimeSalesLine(
            string line,
            TimeZoneInfo est,
            out DateTime lineTimeEastern,
            out Trade trade)
        {
            lineTimeEastern = default;
            trade = default;
            var parts = line.Split(',');
            if (parts.Length < 6) return false;

            if (!DateTime.TryParseExact(
                    parts[0] + " " + parts[1], "MM/dd/yyyy HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var localEst))
                return false;

            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var price)) return false;
            if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var bid)) bid = 0;
            if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var ask)) ask = 0;
            if (!int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var qty)) qty = 0;

            lineTimeEastern = localEst;
            var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localEst, DateTimeKind.Unspecified), est);
            trade = new Trade
            {
                RawPrice = price,
                IntPrice = RawToTransport(price),
                Quantity = qty,
                Op = ResolveTradeOp(price, bid, ask),
                DateTime = utc
            };
            return true;
        }

        private static TradeOp ResolveTradeOp(double price, double bid, double ask)
        {
            if (price <= 0 || bid <= 0 || ask <= 0 || ask < bid)
                return TradeOp.Wait;

            // GETTIMESALES даёт bid/ask на момент сделки, но не даёт
            // aggressor side. Классическое приближение: print на ask = buyer
            // initiated, print на bid = seller initiated. Внутри спреда сторону
            // честно оставляем нейтральной, чтобы не рисовать ложную дельту.
            const double eps = 1e-9;
            if (price >= ask - eps) return TradeOp.Buy;
            if (price <= bid + eps) return TradeOp.Sell;
            return TradeOp.Wait;
        }

        private static int RawToTransport(double raw) =>
            (int)Math.Round(raw * RawPriceFactor);

        private static TimeZoneInfo GetEasternTimeZone()
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }

        private struct SpreadState
        {
            public double RawAsk;
            public double RawBid;
        }

        /// <summary>
        /// Состояние позиционного дедупа TIMESALES для одного тикера.
        /// Хранит, сколько строк уже эмитнули на каждую (Eastern) секунду.
        /// </summary>
        private sealed class TimeSalesCursor
        {
            public DateTime LastSecondEastern { get; set; } = DateTime.MinValue;

            private readonly SortedDictionary<DateTime, int> _emitted = new SortedDictionary<DateTime, int>();
            private readonly object _lock = new object();

            public int GetEmittedCount(DateTime second)
            {
                lock (_lock)
                    return _emitted.TryGetValue(second, out var n) ? n : 0;
            }

            public void SetEmittedCount(DateTime second, int count)
            {
                lock (_lock)
                    _emitted[second] = count;
            }

            public void TrimOlderThan(DateTime cutoff)
            {
                lock (_lock)
                {
                    var stale = _emitted.Keys.Where(k => k < cutoff).ToList();
                    foreach (var k in stale) _emitted.Remove(k);
                }
            }
        }
    }
}
