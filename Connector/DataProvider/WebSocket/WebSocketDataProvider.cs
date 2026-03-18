// ==========================================================================
//    WebSocketDataProvider.cs - Провайдер данных через WebSocket + REST snapshot
// ==========================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using QScalp.Connector.RestApi;

namespace QScalp.Connector.WebSocket
{
    /// <summary>
    /// Провайдер данных, который:
    /// 1. Загружает данные за текущий день через REST API (snapshot с начала торговой сессии)
    /// 2. Подключается к WebSocket для real-time обновлений
    /// 3. Синхронизирует данные и передает в IDataReceiver
    /// </summary>
    class WebSocketDataProvider : IDisposable
    {
        // **********************************************************************

        private readonly ApiClient _apiClient;
        private readonly WebSocketClient _wsClient;
        private readonly IDataReceiver _receiver;
        private readonly TermManager _tmgr;
        private readonly string _ticker;
        private readonly string _secKey;
        private readonly bool _debugMode;
        private readonly bool _skipHistoricalData;
        private StreamWriter _debugLog;
        
        // Синхронизация: последние обработанные sequence numbers
        private int _lastQuoteSequence;
        private int _lastTradeSequence;
        private long _lastQuoteTimestamp;
        private long _lastTradeTimestamp;
        
        // Очередь для буферизации WebSocket сообщений во время загрузки snapshot
        private readonly Queue<object> _wsBuffer = new Queue<object>();
        private bool _snapshotLoaded;
        private readonly object _syncLock = new object();
        
        // Fallback polling (если WebSocket недоступен)
        private CancellationTokenSource _pollCts;
        private Task _pollTask;
        private bool _usePolling;
        private readonly int _pollIntervalMs = 500; // Интервал polling в ms

        // **********************************************************************

        public bool IsConnected { get; private set; }
        public bool IsError { get; private set; }

        // **********************************************************************

        public WebSocketDataProvider(
            string apiBaseUrl,
            string wsBaseUrl,
            string apiKey,
            string ticker,
            string secKey,
            IDataReceiver receiver,
            TermManager tmgr,
            bool debugMode = false,
            bool skipHistoricalData = false)
        {
            _ticker = ticker;
            _secKey = secKey;
            _receiver = receiver;
            _tmgr = tmgr;
            _debugMode = debugMode;
            _skipHistoricalData = skipHistoricalData;
            
            if (_debugMode)
            {
                try
                {
                    _debugLog = new StreamWriter(cfg.WsDebugLogFile, false, Encoding.UTF8) { AutoFlush = true };
                    Log("=== Debug log started ===");
                }
                catch { }
            }
            
            _apiClient = new ApiClient(apiBaseUrl, apiKey);
            if (_debugMode)
                _apiClient.OnLog = msg => Log(msg);
            _wsClient = new WebSocketClient(wsBaseUrl, apiKey, ticker, debugMode);
            
            // Подписываемся на WebSocket события
            _wsClient.OnQuote += HandleWsQuote;
            _wsClient.OnTrade += HandleWsTrade;
            _wsClient.OnError += HandleWsError;
            _wsClient.OnConnected += HandleWsConnected;
            _wsClient.OnDisconnected += HandleWsDisconnected;
            
            if (_debugMode)
                _wsClient.OnRawMessage += HandleWsRawMessage;
        }

        // **********************************************************************

        private void Log(string msg)
        {
            try { _debugLog?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {msg}"); } catch { }
        }

        private void Dbg(string msg)
        {
            Log(msg);
            _receiver.PutMessage(new Message(msg));
        }

        // **********************************************************************

        /// <summary>
        /// Запуск: загрузка snapshot + подключение к WebSocket (с fallback на polling)
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                Dbg($"Starting data provider for {_ticker}...");
                
                if (_debugMode)
                {
                    Dbg($"[DBG] API: {_apiClient.BaseUrl}");
                    Dbg($"[DBG] WS: {_wsClient.BaseUrl}");
                    Dbg($"[DBG] SkipHistoricalData={_skipHistoricalData}");
                }
                
                await LoadTodaySnapshotAsync();
                
                if (_debugMode)
                    Dbg("[DBG] Snapshot phase complete, connecting WebSocket...");
                
                try
                {
                    await _wsClient.ConnectAsync();
                    
                    ProcessBufferedMessages();
                    
                    Dbg("WebSocket connected - real-time mode");
                }
                catch (Exception wsEx)
                {
                    Dbg($"WebSocket unavailable: {wsEx.Message}");
                    Dbg("Falling back to REST API polling mode...");
                    
                    _usePolling = true;
                    StartPolling();
                }
                
                IsConnected = true;
                
                if (_debugMode)
                    Dbg($"[DBG] Data provider ready (mode: {(_usePolling ? "polling" : "websocket")})");
            }
            catch (Exception ex)
            {
                IsError = true;
                Dbg($"Data provider start error: {ex.Message}");
                throw;
            }
        }

        // **********************************************************************

        /// <summary>
        /// Запуск polling (fallback режим)
        /// </summary>
        private void StartPolling()
        {
            _pollCts = new CancellationTokenSource();
            _pollTask = Task.Run(() => PollLoopAsync(_pollCts.Token));
        }

        // **********************************************************************

        /// <summary>
        /// Цикл polling REST API (fallback режим)
        /// </summary>
        private async Task PollLoopAsync(CancellationToken ct)
        {
            Dbg($"Polling started (interval: {_pollIntervalMs}ms)");
            
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Запрашиваем новые данные с последнего timestamp
                    string qTs = _lastQuoteTimestamp > 0 ? _lastQuoteTimestamp.ToString() : DateTime.UtcNow.ToString("yyyy-MM-dd");
                    string tTs = _lastTradeTimestamp > 0 ? _lastTradeTimestamp.ToString() : DateTime.UtcNow.ToString("yyyy-MM-dd");
                    
                    var quotesTask = _apiClient.GetQuotesAsync(_ticker, qTs);
                    var tradesTask = _apiClient.GetTradesAsync(_ticker, tTs);
                    
                    await Task.WhenAll(quotesTask, tradesTask);
                    
                    var quotesResponse = await quotesTask;
                    var tradesResponse = await tradesTask;
                    
                    // Фильтруем и обрабатываем новые данные
                    var newQuotes = FilterNewQuotes(quotesResponse);
                    var newTrades = FilterNewTrades(tradesResponse);
                    
                    if (newQuotes.Length > 0 || newTrades.Length > 0)
                    {
                        ProcessSnapshot(newQuotes, newTrades);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Dbg($"Polling error: {ex.Message}");
                }
                
                try
                {
                    await Task.Delay(_pollIntervalMs, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        // **********************************************************************

        private QuoteResult[] FilterNewQuotes(QuotesResponse response)
        {
            if (response?.Status != "OK" || response.Results == null)
                return new QuoteResult[0];

            var newQuotes = response.Results
                .Where(q => q.SipTimestamp > _lastQuoteTimestamp)
                .ToArray();

            if (newQuotes.Length > 0)
                _lastQuoteTimestamp = newQuotes.Max(q => q.SipTimestamp);

            return newQuotes;
        }

        // **********************************************************************

        private TradeResult[] FilterNewTrades(TradesResponse response)
        {
            if (response?.Status != "OK" || response.Results == null)
                return new TradeResult[0];

            var newTrades = response.Results
                .Where(t => t.SequenceNumber > _lastTradeSequence)
                .ToArray();

            if (newTrades.Length > 0)
            {
                _lastTradeSequence = newTrades.Max(t => t.SequenceNumber);
                _lastTradeTimestamp = newTrades.Max(t => t.SipTimestamp);
            }

            return newTrades;
        }

        // **********************************************************************

        /// <summary>
        /// Загрузка всех данных за текущий день через REST API
        /// </summary>
        private async Task LoadTodaySnapshotAsync()
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            
            Dbg($"Loading today's snapshot: {today}...");
            
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                
                Task<QuoteResult[]> quotesTask;
                Task<TradeResult[]> tradesTask;
                
                if (_skipHistoricalData)
                {
                    if (_debugMode)
                        Dbg("[DBG] Historical data loading DISABLED, skipping quotes and trades");
                    quotesTask = Task.FromResult<QuoteResult[]>(new QuoteResult[0]);
                    tradesTask = Task.FromResult<TradeResult[]>(new TradeResult[0]);
                }
                else
                {
                    if (_debugMode)
                        Dbg($"[DBG] Fetching quotes for {_ticker} date={today}...");
                    quotesTask = _apiClient.FetchAllQuotesAsync(_ticker, today);
                    
                    if (_debugMode)
                        Dbg($"[DBG] Fetching trades for {_ticker} date={today}...");
                    tradesTask = _apiClient.FetchAllTradesAsync(_ticker, today);
                }
                
                await Task.WhenAll(quotesTask, tradesTask);
                
                var quotes = await quotesTask;
                var trades = await tradesTask;
                
                sw.Stop();
                
                if (_skipHistoricalData)
                    Dbg($"Snapshot skipped (historical data disabled) [{sw.ElapsedMilliseconds}ms]");
                else
                    Dbg($"Snapshot loaded: {quotes?.Length ?? 0} quotes, {trades?.Length ?? 0} trades [{sw.ElapsedMilliseconds}ms]");
                
                if ((quotes != null && quotes.Length > 0) || (trades != null && trades.Length > 0))
                {
                    if (_debugMode)
                        Dbg($"[DBG] Processing snapshot events...");
                    
                    ProcessSnapshot(quotes ?? new QuoteResult[0], trades ?? new TradeResult[0]);
                }
                
                if (trades != null && trades.Length > 0)
                {
                    var minPrice = trades.Min(t => t.Price);
                    var maxPrice = trades.Max(t => t.Price);
                    var firstTime = DateTimeOffset.FromUnixTimeMilliseconds(trades.Min(t => t.SipTimestamp) / 1_000_000).DateTime;
                    var lastTime = DateTimeOffset.FromUnixTimeMilliseconds(trades.Max(t => t.SipTimestamp) / 1_000_000).DateTime;
                    
                    Dbg($"Price range: {minPrice:F2} - {maxPrice:F2}");
                    Dbg($"Time range: {firstTime:HH:mm:ss} - {lastTime:HH:mm:ss} UTC");
                }
            }
            catch (Exception ex)
            {
                Dbg($"Snapshot load error: {ex.Message}");
                throw;
            }
            finally
            {
                lock (_syncLock)
                {
                    _snapshotLoaded = true;
                }
            }
        }

        // **********************************************************************

        /// <summary>
        /// Обработка snapshot данных в хронологическом порядке
        /// </summary>
        private void ProcessSnapshot(QuoteResult[] quotes, TradeResult[] trades)
        {
            var events = DataSynchronizer.Merge(quotes, trades);
            
            int processedQuotes = 0, processedTrades = 0;
            
            foreach (var evt in events)
            {
                if (evt is DataSynchronizer.QuoteEvent qe)
                {
                    ProcessQuote(qe.Data);
                    _lastQuoteSequence = qe.Data.SequenceNumber;
                    _lastQuoteTimestamp = qe.Data.SipTimestamp;
                    processedQuotes++;
                }
                else if (evt is DataSynchronizer.TradeEvent te)
                {
                    ProcessTrade(te.Data);
                    _lastTradeSequence = te.Data.SequenceNumber;
                    _lastTradeTimestamp = te.Data.SipTimestamp;
                    processedTrades++;
                }
            }
            
            if (_debugMode)
                Dbg($"[DBG] Snapshot processed: {processedQuotes} quotes, {processedTrades} trades dispatched");
        }

        // **********************************************************************

        /// <summary>
        /// Обработка буферизованных WebSocket сообщений
        /// </summary>
        private void ProcessBufferedMessages()
        {
            lock (_syncLock)
            {
                if (_debugMode)
                    Dbg($"[DBG] Processing {_wsBuffer.Count} buffered WebSocket messages...");
                
                while (_wsBuffer.Count > 0)
                {
                    var msg = _wsBuffer.Dequeue();
                    
                    if (msg is WsQuote quote)
                    {
                        ProcessWsQuoteInternal(quote);
                    }
                    else if (msg is WsTrade trade)
                    {
                        ProcessWsTradeInternal(trade);
                    }
                }
            }
        }

        // **********************************************************************
        // *                    WebSocket Event Handlers                        *
        // **********************************************************************

        // Счетчик для логирования (только в debug mode)
        private int _wsQuoteCount;
        private int _wsTradeCount;
        private int _filteredTradeCount;

        private void HandleWsQuote(WsQuote quote)
        {
            _wsQuoteCount++;
            
            // Логируем только в debug mode
            if (_debugMode && (_wsQuoteCount <= 3 || _wsQuoteCount % 100 == 0))
            {
                Dbg($"[WS] Quote #{_wsQuoteCount}: {quote.Symbol} bid={quote.BidPrice} ask={quote.AskPrice} seq={quote.SequenceNumber}");
            }
            
            lock (_syncLock)
            {
                if (!_snapshotLoaded)
                {
                    _wsBuffer.Enqueue(quote);
                    return;
                }
            }
            
            ProcessWsQuoteInternal(quote);
        }

        // **********************************************************************

        private void HandleWsTrade(WsTrade trade)
        {
            _wsTradeCount++;
            
            // Логируем только в debug mode
            if (_debugMode && (_wsTradeCount <= 3 || _wsTradeCount % 100 == 0))
            {
                Dbg($"[WS] Trade #{_wsTradeCount}: {trade.Symbol} price={trade.Price} size={trade.Size} seq={trade.SequenceNumber}");
            }
            
            lock (_syncLock)
            {
                if (!_snapshotLoaded)
                {
                    _wsBuffer.Enqueue(trade);
                    return;
                }
            }
            
            ProcessWsTradeInternal(trade);
        }

        // **********************************************************************

        private void ProcessWsQuoteInternal(WsQuote q)
        {
            // WebSocket sequence numbers могут не совпадать с REST API
            // Просто обрабатываем все входящие данные (без фильтрации по sequence)
            _lastQuoteSequence = q.SequenceNumber;
            _lastQuoteTimestamp = q.Timestamp * 1_000_000; // WS timestamp в ms, конвертируем в ns
            
            // Конвертируем WsQuote в формат REST API для единообразия
            var quoteResult = new QuoteResult
            {
                AskPrice = q.AskPrice,
                AskSize = q.AskSize,
                BidPrice = q.BidPrice,
                BidSize = q.BidSize,
                AskExchange = q.AskExchange,
                BidExchange = q.BidExchange,
                SipTimestamp = q.Timestamp * 1_000_000, // ms -> ns
                SequenceNumber = q.SequenceNumber,
                Tape = q.Tape
            };
            
            ProcessQuote(quoteResult);
        }

        // **********************************************************************

        private void ProcessWsTradeInternal(WsTrade t)
        {
            // WebSocket sequence numbers могут не совпадать с REST API
            // Просто обрабатываем все входящие данные (без фильтрации по sequence)
            _lastTradeSequence = t.SequenceNumber;
            _lastTradeTimestamp = t.Timestamp * 1_000_000; // WS timestamp в ms, конвертируем в ns
            
            // Конвертируем WsTrade в формат REST API для единообразия
            var tradeResult = new TradeResult
            {
                Price = t.Price,
                Size = t.Size,
                Exchange = t.Exchange,
                Id = t.TradeId,
                SipTimestamp = t.Timestamp * 1_000_000, // ms -> ns
                SequenceNumber = t.SequenceNumber,
                Tape = t.Tape,
                Conditions = t.Conditions,
                TrfId = t.TrfId,
                TrfTimestamp = t.TrfTimestamp.HasValue ? t.TrfTimestamp.Value * 1_000_000 : (long?)null
            };
            
            ProcessTrade(tradeResult);
        }

        // **********************************************************************

        private void HandleWsRawMessage(string rawMsg)
        {
            Dbg($"[WS] {rawMsg}");
        }

        // **********************************************************************

        private void HandleWsError(string error)
        {
            IsError = true;
            Dbg($"WebSocket error: {error}");
        }

        // **********************************************************************

        private void HandleWsConnected()
        {
            Dbg("WebSocket connected");
        }

        // **********************************************************************

        private void HandleWsDisconnected()
        {
            IsConnected = false;
            Dbg("WebSocket disconnected");
        }

        // **********************************************************************
        // *                    Data Processing (shared)                        *
        // **********************************************************************

        private void ProcessQuote(QuoteResult q)
        {
            int askPrice = Price.GetInt(q.AskPrice);
            int bidPrice = Price.GetInt(q.BidPrice);
            int askSize = (int)q.AskSize;
            int bidSize = (int)q.BidSize;

            // NBBO: только лучшие bid/ask
            var quoteArray = new Quote[]
            {
                new Quote(askPrice, askSize, QuoteType.BestAsk),
                new Quote(bidPrice, bidSize, QuoteType.BestBid)
            };

            var spread = new Spread(askPrice, bidPrice);
            
            _tmgr.PutSpread(spread);
            _receiver.PutStock(quoteArray, spread);
        }

        // **********************************************************************

        private void ProcessTrade(TradeResult tr)
        {
            int intPrice = Price.GetInt(tr.Price);

            int ask = _tmgr.AskPrice;
            int bid = _tmgr.BidPrice;

            if (ask > 0 && bid > 0 && (intPrice > ask || intPrice < bid))
            {
                _filteredTradeCount++;
                if (_debugMode && (_filteredTradeCount <= 5 || _filteredTradeCount % 500 == 0))
                {
                    Dbg($"[Filter] Trade #{_filteredTradeCount} skipped: price={tr.Price} " +
                        $"outside NBBO [{Price.GetRaw(bid):F2} - {Price.GetRaw(ask):F2}]");
                }
                return;
            }

            var trade = new Trade
            {
                RawPrice = tr.Price,
                IntPrice = intPrice,
                Quantity = (int)tr.Size,
                Op = TradeOp.Buy,
                DateTime = DateTimeOffset
                    .FromUnixTimeMilliseconds(tr.SipTimestamp / 1_000_000)
                    .DateTime
            };

            _tmgr.PutLastPrice(trade.IntPrice);
            _receiver.PutTrade(_secKey, trade);
        }

        // **********************************************************************
        // *                          Stop / Dispose                            *
        // **********************************************************************

        public async Task StopAsync()
        {
            // Останавливаем polling если используется
            if (_usePolling)
            {
                _pollCts?.Cancel();
                if (_pollTask != null)
                {
                    try
                    {
                        await Task.WhenAny(_pollTask, Task.Delay(5000));
                    }
                    catch { }
                }
            }
            else
            {
                await _wsClient.DisconnectAsync();
            }
            
            IsConnected = false;
        }

        // **********************************************************************

        public void Dispose()
        {
            Log("=== Debug log closed ===");
            _pollCts?.Cancel();
            _pollCts?.Dispose();
            _wsClient?.Dispose();
            _apiClient?.Dispose();
            try { _debugLog?.Close(); } catch { }
            _debugLog = null;
        }

        // **********************************************************************
    }
}
