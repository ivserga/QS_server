using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using QScalp.Server.Config;
using QScalp.Server.Connector;
using QScalp.Shared.Models;
using QScalp.Shared.Protocol;

namespace QScalp.Server.Broadcasting
{
    /// <summary>
    /// Управляет подписками клиентов на тикеры.
    /// Динамически подписывается/отписывается у источника данных (Massive / Striker).
    /// Хранит буфер данных (снапшот) для каждого тикера.
    /// </summary>
    public class SubscriptionManager
    {
        private readonly IMarketDataConnector _connector;
        private readonly ClientManager _clientManager;
        private readonly ServerConfig _config;
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
            IMarketDataConnector connector,
            ClientManager clientManager,
            ServerConfig config,
            Action<string> log)
        {
            _connector = connector;
            _clientManager = clientManager;
            _config = config;
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

            // Создать буфер, если это первая подписка на этот тикер
            if (!_buffers.ContainsKey(ticker))
            {
                _buffers[ticker] = new TickerBuffer(ticker, secKey);

                // Подписка у источника (WS Massive или polling Striker).
                await _connector.SubscribeTickerAsync(ticker);
                _log($"Подписка на {_connector.SourceName}: {ticker}");

                // Тяжёлая REST-загрузка истории дёргается ТОЛЬКО если в конфиге
                // явно включён LoadSnapshotFromApi. По умолчанию выключено,
                // т. к. это ресурсоёмко по сети.
                if (_config != null && _config.LoadSnapshotFromApi)
                {
                    _log($"Загрузка снапшота для {ticker} (REST API)...");
                    await _connector.LoadSnapshotAsync(ticker, _buffers[ticker]);
                    _log($"Снапшот {ticker} загружен");
                }
                else
                {
                    _log($"Авто-загрузка снапшота для {ticker} отключена " +
                         $"(ServerConfig.LoadSnapshotFromApi=false).");
                }
            }

            // Отправлять снапшот клиенту автоматически — тоже под флагом.
            // По умолчанию — нет: клиент получает только live-стрим, что
            // существенно экономит трафик.
            if (_config != null && _config.AutoSnapshotOnSubscribe)
            {
                await SendSnapshotAsync(client, ticker);
            }

            _clientManager.NotifyUI();
        }

        // ********************************************************************

        /// <summary>
        /// Отправляет клиенту накопленный снапшот по запросу. Если в конфиге
        /// разрешено (LoadSnapshotOnDemand &amp;&amp; LoadSnapshotFromApi) —
        /// перед отправкой делает REST-дозагрузку из API.
        /// </summary>
        public async Task SendSnapshotOnDemandAsync(ClientSession client)
        {
            var ticker = client.CurrentTicker;
            if (string.IsNullOrEmpty(ticker)) return;

            if (_config != null && !_config.LoadSnapshotOnDemand)
            {
                _log($"RequestSnapshot от {client.Id} отклонён: " +
                     "ServerConfig.LoadSnapshotOnDemand=false.");
                return;
            }

            if (!_buffers.TryGetValue(ticker, out var buf))
            {
                _log($"RequestSnapshot от {client.Id} проигнорирован: " +
                     $"буфер тикера {ticker} ещё не создан.");
                return;
            }

            // Опциональная дозагрузка истории по REST — только если оба флага
            // включены: клиент явно попросил, и админ сервера разрешил.
            if (_config != null && _config.LoadSnapshotFromApi)
            {
                try
                {
                    _log($"REST-дозагрузка снапшота для {ticker} по запросу клиента {client.Id}...");
                    await _connector.LoadSnapshotAsync(ticker, buf);
                }
                catch (Exception ex)
                {
                    _log($"Ошибка REST-дозагрузки {ticker}: {ex.Message}");
                }
            }

            await SendSnapshotAsync(client, ticker);
        }

        public async Task FillTradeTicketAsync(ClientSession client, ClientCommand cmd)
        {
            if (!( _connector is ITradeTicketConnector tradeTicketConnector))
            {
                _log($"FillTradeTicket от {client.Id} отклонён: источник {_connector.SourceName} не поддерживает Trade Ticket.");
                return;
            }

            var ticker = string.IsNullOrWhiteSpace(cmd.Ticker)
                ? client.CurrentTicker
                : cmd.Ticker.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(ticker))
            {
                _log($"FillTradeTicket от {client.Id} отклонён: тикер не задан.");
                return;
            }

            if (cmd.Quantity <= 0)
            {
                _log($"FillTradeTicket {ticker} от {client.Id} отклонён: quantity={cmd.Quantity}.");
                return;
            }

            try
            {
                await tradeTicketConnector.FillTradeTicketAsync(
                    ticker,
                    cmd.Side,
                    cmd.Quantity,
                    cmd.SignalStrength,
                    cmd.SignalMessage);
            }
            catch (Exception ex)
            {
                _log($"FillTradeTicket {ticker} ошибка: {ex.Message}");
            }
        }

        // ********************************************************************

        private async Task SendSnapshotAsync(ClientSession client, string ticker)
        {
            if (!_buffers.TryGetValue(ticker, out var buf)) return;

            var snapshot = buf.GetSnapshot();
            var msg = ProtocolHelper.CreateMessage(
                ServerMessageType.Snapshot, ticker, snapshot);
            await client.SendAsync(ProtocolHelper.Serialize(msg));
        }

        // ********************************************************************

        public async Task UnsubscribeClientAsync(ClientSession client)
        {
            var ticker = client.CurrentTicker;
            if (ticker == null) return;

            client.CurrentTicker = null;

            // Если больше нет подписчиков — отписаться у источника
            if (!_clientManager.HasOtherSubscribers(ticker, client.Id))
            {
                await _connector.UnsubscribeTickerAsync(ticker);
                _buffers.TryRemove(ticker, out _);
                _log($"Отписка от {_connector.SourceName}: {ticker} (нет подписчиков)");
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
