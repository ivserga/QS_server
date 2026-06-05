// ==========================================================================
//  QScalpWsClient.cs — WebSocket-клиент к QScalp.Server.InternalWsServer
// ==========================================================================
//  Одно подключение = одна подписка (так устроен InternalWsServer:
//  ClientSession.CurrentTicker — single). Для нескольких тикеров клиент
//  открывает несколько экземпляров этого класса.
//
//  Возможности:
//    • ConnectAsync — открыть WS-канал (ws://host:port/),
//    • SubscribeAsync(ticker, secKey) — отправить ClientCommand,
//    • Раздаёт события OnTrade / OnSnapshot / OnQuote / OnConnected /
//      OnDisconnected / OnError / OnLog,
//    • Авто-переподключение через ReconnectDelay при разрыве,
//      пока StopAsync не вызван.
// ==========================================================================

using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using QScalp.Shared.Models;
using QScalp.Shared.Protocol;

namespace QScalp.Client.Net
{
    internal sealed class QScalpWsClient : IDisposable
    {
        readonly string _serverUrl;
        readonly string _ticker;
        readonly string _secKey;

        ClientWebSocket _ws;
        CancellationTokenSource _cts;
        Task _runTask;
        readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(3);

        public bool IsRunning => _runTask != null && !_runTask.IsCompleted;
        public bool IsConnected => _ws?.State == WebSocketState.Open;

        // ---- события ----
        public event Action<Trade> OnTrade;
        public event Action<SnapshotPayload> OnSnapshot;
        public event Action<Quote[], Spread> OnQuote;
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;
        public event Action<string> OnLog;

        // ********************************************************************

        public QScalpWsClient(string serverUrl, string ticker, string secKey = null)
        {
            _serverUrl = serverUrl;
            _ticker = ticker;
            _secKey = string.IsNullOrEmpty(secKey) ? ticker : secKey;
        }

        // ********************************************************************

        public void Start()
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            _runTask = Task.Run(() => RunLoopAsync(_cts.Token));
        }

        // ********************************************************************

        public async Task StopAsync()
        {
            if (_cts == null) return;

            _cts.Cancel();

            try
            {
                if (_ws != null && _ws.State == WebSocketState.Open)
                {
                    using (var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                        await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure,
                            "client stop", closeCts.Token);
                }
            }
            catch { /* ignored */ }

            try { if (_runTask != null) await _runTask; }
            catch { /* ignored */ }

            _ws?.Dispose();
            _ws = null;
            _cts.Dispose();
            _cts = null;
            _runTask = null;
        }

        public Task FillTradeTicketAsync(string side, int quantity, double signalStrength, string signalMessage)
        {
            if (_ws == null || _ws.State != WebSocketState.Open)
                throw new InvalidOperationException("WS не подключён.");

            return SendCommandAsync(new ClientCommand
            {
                Type = ClientCommandType.FillTradeTicket,
                Ticker = _ticker,
                SecKey = _secKey,
                Side = side,
                Quantity = quantity,
                SignalStrength = signalStrength,
                SignalMessage = signalMessage
            }, _cts.Token);
        }

        // ********************************************************************

        async Task RunLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    _ws = new ClientWebSocket();
                    OnLog?.Invoke($"[{_ticker}] connecting to {_serverUrl} ...");
                    await _ws.ConnectAsync(new Uri(_serverUrl), ct);
                    OnLog?.Invoke($"[{_ticker}] connected");
                    OnConnected?.Invoke();

                    await SendCommandAsync(new ClientCommand
                    {
                        Type = ClientCommandType.Subscribe,
                        Ticker = _ticker,
                        SecKey = _secKey
                    }, ct);

                    await ReceiveLoopAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(ex.Message);
                }
                finally
                {
                    try { _ws?.Dispose(); } catch { }
                    _ws = null;
                    OnDisconnected?.Invoke();
                }

                if (ct.IsCancellationRequested) break;

                try { await Task.Delay(ReconnectDelay, ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        // ********************************************************************

        async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[16 * 1024];
            var sb = new StringBuilder();

            while (_ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                sb.Clear();

                while (true)
                {
                    WebSocketReceiveResult result;
                    try
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    }
                    catch (WebSocketException)
                    {
                        return;
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        try
                        {
                            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                "remote close", CancellationToken.None);
                        }
                        catch { }
                        return;
                    }

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (result.EndOfMessage) break;
                }

                DispatchMessage(sb.ToString());
            }
        }

        // ********************************************************************

        void DispatchMessage(string json)
        {
            ServerMessage msg;
            try
            {
                msg = ProtocolHelper.DeserializeServerMessage(json);
            }
            catch (Exception ex)
            {
                OnError?.Invoke("parse: " + ex.Message);
                return;
            }
            if (msg == null) return;

            try
            {
                switch (msg.Type)
                {
                    case ServerMessageType.TradeUpdate:
                    {
                        var p = ProtocolHelper.DeserializePayload<TradeUpdatePayload>(msg.Payload);
                        if (p != null) OnTrade?.Invoke(p.Trade);
                        break;
                    }
                    case ServerMessageType.StockUpdate:
                    {
                        var p = ProtocolHelper.DeserializePayload<StockUpdatePayload>(msg.Payload);
                        if (p != null) OnQuote?.Invoke(p.Quotes, p.Spread);
                        break;
                    }
                    case ServerMessageType.Snapshot:
                    {
                        var p = ProtocolHelper.DeserializePayload<SnapshotPayload>(msg.Payload);
                        if (p != null) OnSnapshot?.Invoke(p);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("dispatch: " + ex.Message);
            }
        }

        // ********************************************************************

        async Task SendCommandAsync(ClientCommand cmd, CancellationToken ct)
        {
            string json = ProtocolHelper.SerializeCommand(cmd);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await _sendLock.WaitAsync(ct);
            try
            {
                await _ws.SendAsync(new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text, true, ct);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        // ********************************************************************

        public void Dispose()
        {
            try { StopAsync().GetAwaiter().GetResult(); }
            catch { /* ignored */ }
            _sendLock.Dispose();
        }
    }
}
