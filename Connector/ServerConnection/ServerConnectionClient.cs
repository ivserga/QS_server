// ==========================================================================
//    ServerConnectionClient.cs - Подключение клиента к QScalp.Server
// ==========================================================================

using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace QScalp.Connector.ServerConnection
{
    /// <summary>
    /// WebSocket клиент для подключения к QScalp.Server.
    /// Заменяет прямое подключение к massive.com.
    /// </summary>
    class ServerConnectionClient : IDisposable
    {
        // **********************************************************************

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private Task _receiveTask;

        private readonly string _serverUrl;
        private readonly IDataReceiver _receiver;
        private readonly TermManager _tmgr;
        private readonly bool _skipHistoricalData;
        private readonly int _filterTicks;

        // **********************************************************************

        public event Action<string> OnError;
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnLog;

        public bool IsConnected => _ws?.State == WebSocketState.Open;

        // **********************************************************************

        public ServerConnectionClient(string serverUrl, IDataReceiver receiver, TermManager tmgr,
            bool skipHistoricalData, int filterTicks)
        {
            _serverUrl = NormalizeUrl(serverUrl);
            _receiver = receiver;
            _tmgr = tmgr;
            _skipHistoricalData = skipHistoricalData;
            _filterTicks = filterTicks;
        }

        /// <summary>
        /// http:// → ws://, https:// → wss://
        /// Пользователь может ввести любой вариант.
        /// </summary>
        private static string NormalizeUrl(string url)
        {
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                return "ws://" + url.Substring(7);
            if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return "wss://" + url.Substring(8);
            if (!url.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                return "ws://" + url;
            return url;
        }

        // **********************************************************************

        public async Task ConnectAsync()
        {
            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();

            OnLog?.Invoke($"Подключение к серверу: {_serverUrl}");
            await _ws.ConnectAsync(new Uri(_serverUrl), _cts.Token);

            _receiveTask = Task.Run(() => ReceiveLoopAsync());
            OnConnected?.Invoke();
            OnLog?.Invoke("Подключён к серверу");
        }

        // **********************************************************************

        /// <summary>
        /// Подписка на тикер (отправляет команду серверу)
        /// </summary>
        public async Task SubscribeAsync(string ticker, string secKey)
        {
            var cmd = new
            {
                type = 0, // Subscribe
                ticker = ticker,
                secKey = secKey
            };
            await SendAsync(JsonConvert.SerializeObject(cmd));
            OnLog?.Invoke($"Подписка на {ticker}");
        }

        // **********************************************************************

        /// <summary>
        /// Отписка от тикера
        /// </summary>
        public async Task UnsubscribeAsync()
        {
            var cmd = new
            {
                type = 1 // Unsubscribe
            };
            await SendAsync(JsonConvert.SerializeObject(cmd));
        }

        // **********************************************************************

        private async Task SendAsync(string json)
        {
            if (_ws?.State != WebSocketState.Open) return;

            var bytes = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cts.Token);
        }

        // **********************************************************************

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[65536];
            var sb = new StringBuilder();

            try
            {
                while (_ws.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    var result = await _ws.ReceiveAsync(
                        new ArraySegment<byte>(buffer), _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close) break;

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                        if (result.EndOfMessage)
                        {
                            ProcessServerMessage(sb.ToString());
                            sb.Clear();
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException ex)
            {
                OnError?.Invoke($"WebSocket: {ex.Message}");
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Error: {ex.Message}");
            }
            finally
            {
                OnDisconnected?.Invoke();
            }
        }

        // **********************************************************************

        private void ProcessServerMessage(string json)
        {
            try
            {
                var msg = JsonConvert.DeserializeObject<ServerMsg>(json);
                if (msg == null) return;

                switch (msg.Type)
                {
                    case 0: // StockUpdate
                        ProcessStockUpdate(msg.Payload);
                        break;

                    case 1: // TradeUpdate
                        ProcessTradeUpdate(msg.Payload);
                        break;

                    case 3: // Snapshot
                        ProcessSnapshot(msg.Payload);
                        break;

                    case 5: // ServerMessage (text)
                        ProcessTextMessage(msg.Payload);
                        break;
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Message parse error: {ex.Message}");
            }
        }

        // **********************************************************************

        private const int ServerPriceFactor = 1_000_000;

        private void ProcessStockUpdate(string payload)
        {
            var data = JsonConvert.DeserializeObject<StockPayload>(payload);
            if (data == null) return;

            var quotes = ConvertQuotes(data.Quotes);

            // Пересчитываем spread из транспортного формата в PriceRatio клиента
            double rawAsk = (double)data.Spread.A / ServerPriceFactor;
            double rawBid = (double)data.Spread.B / ServerPriceFactor;
            var spread = new Spread(Price.GetInt(rawAsk), Price.GetInt(rawBid));

            _tmgr.PutSpread(spread);
            _receiver.PutStock(quotes, spread);
        }

        // **********************************************************************

        private void ProcessTradeUpdate(string payload)
        {
            var data = JsonConvert.DeserializeObject<TradePayload>(payload);
            if (data == null) return;

            var trade = ConvertTrade(data.Trade);

            trade.IntPrice = Price.GetInt(trade.RawPrice);

            // NBBO-фильтр — точная копия логики из WebSocketDataProvider.ProcessTrade:
            // _filterTicks < 0 → фильтр отключён, все сделки проходят
            // _filterTicks >= 0 → отсекаем за пределами NBBO ± _filterTicks
            if (_filterTicks >= 0)
            {
                int ask = _tmgr.AskPrice;
                int bid = _tmgr.BidPrice;
                if (ask > 0 && bid > 0 &&
                    (trade.IntPrice > ask + _filterTicks || trade.IntPrice < bid - _filterTicks))
                    return;
            }

            _tmgr.PutLastPrice(trade.IntPrice);
            _receiver.PutTrade(data.SecKey, trade);
        }

        // **********************************************************************

        private void ProcessSnapshot(string payload)
        {
            var data = JsonConvert.DeserializeObject<SnapshotData>(payload);
            if (data == null) return;

            OnLog?.Invoke($"Снапшот: {data.RecentTrades?.Length ?? 0} сделок");

            // Котировки (всегда загружаем для отображения стакана)
            if (data.Quotes != null && data.Quotes.Length > 0)
            {
                var quotes = ConvertQuotes(data.Quotes);
                double rawAsk = (double)data.Spread.A / ServerPriceFactor;
                double rawBid = (double)data.Spread.B / ServerPriceFactor;
                var spread = new Spread(Price.GetInt(rawAsk), Price.GetInt(rawBid));
                _tmgr.PutSpread(spread);
                _receiver.PutStock(quotes, spread);
            }

            // Сделки (пропускаем если SkipHistoricalData)
            if (_skipHistoricalData)
            {
                OnLog?.Invoke("Исторические данные пропущены (SkipHistoricalData)");
            }
            else if (data.RecentTrades != null)
            {
                foreach (var td in data.RecentTrades)
                {
                    var trade = ConvertTrade(td.Trade);
                    _tmgr.PutLastPrice(trade.IntPrice);
                    _receiver.PutTrade(td.SecKey, trade);
                }
            }

            OnLog?.Invoke("Снапшот обработан");
        }

        // **********************************************************************

        private void ProcessTextMessage(string payload)
        {
            var data = JsonConvert.DeserializeObject<TextMsg>(payload);
            if (data != null)
                _receiver.PutMessage(new Message(data.Txt));
        }

        // **********************************************************************
        // *                        Conversion helpers                          *
        // **********************************************************************

        private Quote[] ConvertQuotes(SharedQuote[] src)
        {
            if (src == null) return new Quote[0];

            var result = new Quote[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                // P приходит в транспортном формате (raw * 10^6),
                // пересчитываем в PriceRatio клиента
                double rawPrice = (double)src[i].P / ServerPriceFactor;
                int localPrice = Price.GetInt(rawPrice);
                result[i] = new Quote(localPrice, src[i].V, (QuoteType)src[i].T);
            }
            return result;
        }

        private Trade ConvertTrade(SharedTrade src)
        {
            return new Trade
            {
                IntPrice = Price.GetInt(src.Rp),
                RawPrice = src.Rp,
                Quantity = src.Q,
                Op = (TradeOp)src.Op,
                DateTime = src.Dt
            };
        }

        // **********************************************************************

        public async Task DisconnectAsync()
        {
            _cts?.Cancel();
            if (_ws?.State == WebSocketState.Open)
            {
                try
                {
                    await _ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Disconnect",
                        CancellationToken.None);
                }
                catch { }
            }
        }

        // **********************************************************************

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _ws?.Dispose();
        }

        // **********************************************************************
        // *                     Internal DTOs for deserialization              *
        // **********************************************************************

        private class ServerMsg
        {
            [JsonProperty("type")] public int Type { get; set; }
            [JsonProperty("ticker")] public string Ticker { get; set; }
            [JsonProperty("payload")] public string Payload { get; set; }
        }

        private class StockPayload
        {
            [JsonProperty("quotes")] public SharedQuote[] Quotes { get; set; }
            [JsonProperty("spread")] public SharedSpread Spread { get; set; }
        }

        private class TradePayload
        {
            [JsonProperty("secKey")] public string SecKey { get; set; }
            [JsonProperty("trade")] public SharedTrade Trade { get; set; }
        }

        private class SnapshotData
        {
            [JsonProperty("config")] public ConfigData Config { get; set; }
            [JsonProperty("quotes")] public SharedQuote[] Quotes { get; set; }
            [JsonProperty("spread")] public SharedSpread Spread { get; set; }
            [JsonProperty("trades")] public TradePayload[] RecentTrades { get; set; }
        }

        private class ConfigData
        {
            [JsonProperty("ticker")] public string Ticker { get; set; }
            [JsonProperty("secKey")] public string SecKey { get; set; }
            [JsonProperty("priceRatio")] public int PriceRatio { get; set; }
            [JsonProperty("priceStep")] public int PriceStep { get; set; }
        }

        private class TextMsg
        {
            [JsonProperty("dt")] public DateTime Dt { get; set; }
            [JsonProperty("txt")] public string Txt { get; set; }
        }

        private class SharedQuote
        {
            [JsonProperty("p")] public int P { get; set; }
            [JsonProperty("v")] public int V { get; set; }
            [JsonProperty("t")] public int T { get; set; }
        }

        private class SharedSpread
        {
            [JsonProperty("a")] public int A { get; set; }
            [JsonProperty("b")] public int B { get; set; }
        }

        private class SharedTrade
        {
            [JsonProperty("ip")] public int Ip { get; set; }
            [JsonProperty("rp")] public double Rp { get; set; }
            [JsonProperty("q")] public int Q { get; set; }
            [JsonProperty("op")] public int Op { get; set; }
            [JsonProperty("dt")] public DateTime Dt { get; set; }
        }
    }
}
