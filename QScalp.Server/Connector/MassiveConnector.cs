using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using QScalp.Server.Connector.RestApi;
using QScalp.Server.Connector.WebSocket;
using QScalp.Server.Broadcasting;
using QScalp.Shared.Models;

namespace QScalp.Server.Connector
{
    /// <summary>
    /// Подключение к massive.com: одно WS-соединение, динамические подписки на тикеры.
    /// Принимает сырые данные от massive.com и транслирует в формате Shared моделей.
    /// </summary>
    public class MassiveConnector : IDisposable
    {
        private readonly MassiveWebSocketClient _wsClient;
        private readonly ApiClient _apiClient;
        private readonly Action<string> _log;
        private readonly bool _debugMode;

        // Список активных тикеров
        private readonly ConcurrentDictionary<string, bool> _activeTickers
            = new ConcurrentDictionary<string, bool>();

        // Последний NBBO по тикеру (для определения стороны сделки)
        private readonly ConcurrentDictionary<string, SpreadState> _lastSpreads
            = new ConcurrentDictionary<string, SpreadState>();

        // ********************************************************************

        /// <summary>
        /// Вызывается при получении котировки: (ticker, quotes, spread)
        /// </summary>
        public event Action<string, Quote[], Spread> OnQuoteReceived;

        /// <summary>
        /// Вызывается при получении сделки: (ticker, secKey, trade)
        /// </summary>
        public event Action<string, string, Trade> OnTradeReceived;

        // ********************************************************************

        public MassiveConnector(
            string apiBaseUrl,
            string wsBaseUrl,
            string apiKey,
            bool debugMode,
            Action<string> log)
        {
            _log = log;
            _debugMode = debugMode;

            _apiClient = new ApiClient(apiBaseUrl, apiKey);
            if (debugMode) _apiClient.OnLog = msg => log($"[API] {msg}");

            _wsClient = new MassiveWebSocketClient(wsBaseUrl, apiKey, debugMode);
            _wsClient.OnQuote += HandleWsQuote;
            _wsClient.OnTrade += HandleWsTrade;
            _wsClient.OnError += err => _log($"[WS Error] {err}");
            _wsClient.OnConnected += () => _log("Подключён к massive.com");
            _wsClient.OnDisconnected += () => _log("Отключён от massive.com");
            if (debugMode)
                _wsClient.OnRawMessage += msg => _log($"[WS Raw] {msg}");
        }

        // ********************************************************************

        public async Task ConnectAsync()
        {
            await _wsClient.ConnectAsync();
        }

        // ********************************************************************

        public async Task SubscribeTickerAsync(string ticker)
        {
            _activeTickers[ticker] = true;
            await _wsClient.SubscribeAsync(ticker);
            _log($"[WS] Подписка: Q.{ticker}, T.{ticker}");
        }

        // ********************************************************************

        public async Task UnsubscribeTickerAsync(string ticker)
        {
            _activeTickers.TryRemove(ticker, out _);
            _lastSpreads.TryRemove(ticker, out _);
            await _wsClient.UnsubscribeAsync(ticker);
            _log($"[WS] Отписка: Q.{ticker}, T.{ticker}");
        }

        // ********************************************************************

        /// <summary>
        /// Загрузка REST-снапшота за сегодня и заполнение буфера.
        /// </summary>
        public async Task LoadSnapshotAsync(string ticker, TickerBuffer buffer)
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var quotesTask = _apiClient.FetchAllQuotesAsync(ticker, today);
            var tradesTask = _apiClient.FetchAllTradesAsync(ticker, today);
            await Task.WhenAll(quotesTask, tradesTask);

            var quotes = await quotesTask ?? new QuoteResult[0];
            var trades = await tradesTask ?? new TradeResult[0];

            _log($"Снапшот {ticker}: {quotes.Length} котировок, {trades.Length} сделок");

            // Обрабатываем в хронологическом порядке
            var events = DataSynchronizer.Merge(quotes, trades);
            foreach (var evt in events)
            {
                if (evt is DataSynchronizer.QuoteEvent qe)
                {
                    var (q, sp) = ConvertQuote(ticker, qe.Data);
                    buffer.UpdateStock(q, sp);
                }
                else if (evt is DataSynchronizer.TradeEvent te)
                {
                    var trade = ConvertTrade(ticker, buffer.SecKey, te.Data);
                    if (trade.HasValue)
                        buffer.AddTrade(buffer.SecKey, trade.Value);
                }
            }
        }

        // ********************************************************************
        // *               WebSocket event handlers                           *
        // ********************************************************************

        private void HandleWsQuote(WsQuote q)
        {
            var ticker = q.Symbol;
            if (!_activeTickers.ContainsKey(ticker)) return;

            var quoteResult = new QuoteResult
            {
                AskPrice = q.AskPrice,
                AskSize = q.AskSize,
                BidPrice = q.BidPrice,
                BidSize = q.BidSize,
                SipTimestamp = q.Timestamp * 1_000_000
            };

            var (quotes, spread) = ConvertQuote(ticker, quoteResult);
            OnQuoteReceived?.Invoke(ticker, quotes, spread);
        }

        // ********************************************************************

        private void HandleWsTrade(WsTrade t)
        {
            var ticker = t.Symbol;
            if (!_activeTickers.ContainsKey(ticker)) return;

            var tradeResult = new TradeResult
            {
                Price = t.Price,
                Size = t.Size,
                SipTimestamp = t.Timestamp * 1_000_000
            };

            // secKey = ticker (упрощённо для US акций)
            var secKey = ticker;
            var trade = ConvertTrade(ticker, secKey, tradeResult);
            if (trade.HasValue)
                OnTradeReceived?.Invoke(ticker, secKey, trade.Value);
        }

        // ********************************************************************
        // *                   Conversion helpers                              *
        // ********************************************************************

        private (Quote[], Spread) ConvertQuote(string ticker, QuoteResult q)
        {
            // Передаём raw-цену как int (умноженную на RawPriceFactor)
            // для сохранения точности. Клиент пересчитает под свой PriceRatio.
            int askPrice = RawToTransport(q.AskPrice);
            int bidPrice = RawToTransport(q.BidPrice);

            _lastSpreads[ticker] = new SpreadState
            {
                RawAsk = q.AskPrice,
                RawBid = q.BidPrice
            };

            var quotes = new Quote[]
            {
                new Quote(askPrice, (int)q.AskSize, QuoteType.BestAsk),
                new Quote(bidPrice, (int)q.BidSize, QuoteType.BestBid)
            };

            var spread = new Spread(askPrice, bidPrice);
            return (quotes, spread);
        }

        // ********************************************************************

        private Trade? ConvertTrade(string ticker, string secKey, TradeResult tr)
        {
            return new Trade
            {
                RawPrice = tr.Price,
                IntPrice = RawToTransport(tr.Price),
                Quantity = (int)tr.Size,
                Op = TradeOp.Buy,
                DateTime = DateTimeOffset
                    .FromUnixTimeMilliseconds(tr.SipTimestamp / 1_000_000)
                    .DateTime
            };
        }

        // ********************************************************************

        /// <summary>
        /// Конвертация raw-цены в транспортный int.
        /// Используем высокую точность (10^6), чтобы клиент мог
        /// пересчитать под свой PriceRatio без потерь.
        /// </summary>
        private const int RawPriceFactor = 1_000_000;

        private static int RawToTransport(double raw)
        {
            return (int)Math.Round(raw * RawPriceFactor);
        }

        // ********************************************************************

        public void Dispose()
        {
            _wsClient?.Dispose();
            _apiClient?.Dispose();
        }

        // ********************************************************************

        private struct SpreadState
        {
            public double RawAsk;
            public double RawBid;
        }
    }
}
