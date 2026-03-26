using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using QScalp.Server.Connector;
using QScalp.Shared.Models;
using QScalp.Shared.Protocol;

namespace QScalp.Server.Broadcasting
{
    /// <summary>
    /// Управляет подписками клиентов на тикеры.
    /// Динамически подписывается/отписывается на massive.com.
    /// Хранит буфер данных (снапшот) для каждого тикера.
    /// </summary>
    public class SubscriptionManager
    {
        private readonly MassiveConnector _connector;
        private readonly ClientManager _clientManager;
        private readonly Action<string> _log;

        // Буфер последних данных по тикеру (для снапшота новых клиентов)
        private readonly ConcurrentDictionary<string, TickerBuffer> _buffers
            = new ConcurrentDictionary<string, TickerBuffer>();

        // Упорядоченная очередь broadcast —
        // гарантирует, что котировки и сделки приходят клиенту в правильном порядке
        private readonly ConcurrentQueue<BroadcastItem> _broadcastQueue
            = new ConcurrentQueue<BroadcastItem>();
        private readonly SemaphoreSlim _broadcastSignal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _broadcastCts = new CancellationTokenSource();
        private Task _broadcastTask;

        private struct BroadcastItem
        {
            public string Ticker;
            public string Json;
        }

        // ********************************************************************

        public SubscriptionManager(
            MassiveConnector connector,
            ClientManager clientManager,
            Action<string> log)
        {
            _connector = connector;
            _clientManager = clientManager;
            _log = log;

            _connector.OnQuoteReceived += HandleQuote;
            _connector.OnTradeReceived += HandleTrade;

            _broadcastTask = Task.Run(() => BroadcastLoopAsync(_broadcastCts.Token));
        }

        // ********************************************************************

        /// <summary>
        /// Фоновый цикл, рассылающий сообщения из очереди строго по порядку (FIFO).
        /// </summary>
        private async Task BroadcastLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await _broadcastSignal.WaitAsync(ct);

                    BroadcastItem item;
                    if (_broadcastQueue.TryDequeue(out item))
                    {
                        try
                        {
                            await _clientManager.BroadcastToTickerAsync(item.Ticker, item.Json);
                        }
                        catch (Exception ex)
                        {
                            _log($"Broadcast error: {ex.Message}");
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        private void EnqueueBroadcast(string ticker, string json)
        {
            _broadcastQueue.Enqueue(new BroadcastItem { Ticker = ticker, Json = json });
            _broadcastSignal.Release();
        }

        // ********************************************************************

        public async Task SubscribeClientAsync(ClientSession client, string ticker, string secKey)
        {
            // Отписать от старого тикера
            if (client.CurrentTicker != null && client.CurrentTicker != ticker)
            {
                await UnsubscribeClientAsync(client);
            }

            client.CurrentTicker = ticker;
            _log($"Клиент {client.Id} подписался на {ticker}");

            // Создать буфер если первая подписка на этот тикер
            if (!_buffers.ContainsKey(ticker))
            {
                _buffers[ticker] = new TickerBuffer(ticker, secKey);

                // Подписаться на massive.com
                await _connector.SubscribeTickerAsync(ticker);
                _log($"Подписка на massive.com: {ticker}");

                // Загрузить снапшот
                _log($"Загрузка снапшота для {ticker}...");
                await _connector.LoadSnapshotAsync(ticker, _buffers[ticker]);
                _log($"Снапшот {ticker} загружен");
            }

            // Отправить снапшот клиенту
            var buf = _buffers[ticker];
            var snapshot = buf.GetSnapshot();
            var msg = ProtocolHelper.CreateMessage(
                ServerMessageType.Snapshot, ticker, snapshot);
            await client.SendAsync(ProtocolHelper.Serialize(msg));

            _clientManager.NotifyUI();
        }

        // ********************************************************************

        public async Task UnsubscribeClientAsync(ClientSession client)
        {
            var ticker = client.CurrentTicker;
            if (ticker == null) return;

            client.CurrentTicker = null;

            // Если больше нет подписчиков — отписаться от massive.com
            if (!_clientManager.HasOtherSubscribers(ticker, client.Id))
            {
                await _connector.UnsubscribeTickerAsync(ticker);
                _buffers.TryRemove(ticker, out _);
                _log($"Отписка от massive.com: {ticker} (нет подписчиков)");
            }

            _clientManager.NotifyUI();
        }

        // ********************************************************************

        private void HandleQuote(string ticker, Quote[] quotes, Spread spread)
        {
            if (_buffers.TryGetValue(ticker, out var buf))
            {
                buf.UpdateStock(quotes, spread);
            }

            var payload = new StockUpdatePayload { Quotes = quotes, Spread = spread };
            var msg = ProtocolHelper.CreateMessage(
                ServerMessageType.StockUpdate, ticker, payload);
            var json = ProtocolHelper.Serialize(msg);

            EnqueueBroadcast(ticker, json);
        }

        // ********************************************************************

        private void HandleTrade(string ticker, string ignoredSecKey, Trade trade)
        {
            TickerBuffer buf;
            _buffers.TryGetValue(ticker, out buf);

            var actualSecKey = buf?.SecKey ?? ticker;

            if (buf != null)
            {
                buf.AddTrade(actualSecKey, trade);
            }

            var payload = new TradeUpdatePayload { SecKey = actualSecKey, Trade = trade };
            var msg = ProtocolHelper.CreateMessage(
                ServerMessageType.TradeUpdate, ticker, payload);
            var json = ProtocolHelper.Serialize(msg);

            EnqueueBroadcast(ticker, json);
        }
    }

    // ************************************************************************
    // *                        TickerBuffer                                   *
    // ************************************************************************

    /// <summary>
    /// Буфер данных по одному тикеру для отправки снапшота новым клиентам.
    /// </summary>
    public class TickerBuffer
    {
        public string Ticker { get; }
        public string SecKey { get; }

        private Quote[] _lastQuotes;
        private Spread _lastSpread;
        private readonly List<TradeUpdatePayload> _recentTrades = new List<TradeUpdatePayload>();
        private readonly object _lock = new object();

        private const int MaxBufferedTrades = 50000;

        // ********************************************************************

        public TickerBuffer(string ticker, string secKey)
        {
            Ticker = ticker;
            SecKey = secKey;
        }

        // ********************************************************************

        public void UpdateStock(Quote[] quotes, Spread spread)
        {
            lock (_lock)
            {
                _lastQuotes = quotes;
                _lastSpread = spread;
            }
        }

        // ********************************************************************

        public void AddTrade(string secKey, Trade trade)
        {
            lock (_lock)
            {
                _recentTrades.Add(new TradeUpdatePayload { SecKey = secKey, Trade = trade });
                if (_recentTrades.Count > MaxBufferedTrades)
                    _recentTrades.RemoveRange(0, _recentTrades.Count - MaxBufferedTrades);
            }
        }

        // ********************************************************************

        public SnapshotPayload GetSnapshot()
        {
            lock (_lock)
            {
                return new SnapshotPayload
                {
                    Config = new TickerConfigPayload
                    {
                        Ticker = Ticker,
                        SecKey = SecKey
                    },
                    Quotes = _lastQuotes,
                    Spread = _lastSpread,
                    RecentTrades = _recentTrades.ToArray()
                };
            }
        }
    }
}
