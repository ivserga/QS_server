using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using QScalp.Shared.Protocol;

namespace QScalp.Server.Broadcasting
{
    /// <summary>
    /// WebSocket-сервер для клиентов QScalp.
    /// Использует HttpListener для приёма WebSocket-соединений.
    /// </summary>
    public class InternalWsServer
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private readonly string _listenUrl;
        private readonly int _maxClients;
        private readonly ClientManager _clientManager;
        private readonly SubscriptionManager _subscriptionManager;
        private readonly Action<string> _log;

        // ********************************************************************

        public InternalWsServer(
            string listenUrl,
            int maxClients,
            ClientManager clientManager,
            SubscriptionManager subscriptionManager,
            Action<string> log)
        {
            _listenUrl = listenUrl;
            _maxClients = maxClients;
            _clientManager = clientManager;
            _subscriptionManager = subscriptionManager;
            _log = log;
        }

        // ********************************************************************

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add(_listenUrl);
            _listener.Start();
            _log($"WebSocket сервер слушает: {_listenUrl}");

            Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        // ********************************************************************

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();

                    if (!context.Request.IsWebSocketRequest)
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                        continue;
                    }

                    if (_clientManager.Count >= _maxClients)
                    {
                        _log($"Отклонено подключение: лимит клиентов ({_maxClients})");
                        context.Response.StatusCode = 503;
                        context.Response.Close();
                        continue;
                    }

                    var wsContext = await context.AcceptWebSocketAsync(null);
                    var remoteAddr = context.Request.RemoteEndPoint?.ToString() ?? "unknown";
                    var client = new ClientSession(wsContext.WebSocket, remoteAddr);

                    _clientManager.AddClient(client);

                    _ = Task.Run(() => HandleClientAsync(client, ct));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log($"Ошибка accept: {ex.Message}");
                }
            }
        }

        // ********************************************************************

        private async Task HandleClientAsync(ClientSession client, CancellationToken ct)
        {
            try
            {
                while (client.IsOpen && !ct.IsCancellationRequested)
                {
                    var json = await client.ReceiveAsync();
                    if (json == null) break;

                    await ProcessClientMessage(client, json);
                }
            }
            catch (Exception ex)
            {
                _log($"Ошибка клиента {client.Id}: {ex.Message}");
            }
            finally
            {
                await _subscriptionManager.UnsubscribeClientAsync(client);
                _clientManager.RemoveClient(client);
            }
        }

        // ********************************************************************

        private async Task ProcessClientMessage(ClientSession client, string json)
        {
            try
            {
                var cmd = ProtocolHelper.DeserializeCommand(json);
                if (cmd == null) return;

                switch (cmd.Type)
                {
                    case ClientCommandType.Subscribe:
                        if (!string.IsNullOrEmpty(cmd.Ticker))
                        {
                            await _subscriptionManager.SubscribeClientAsync(
                                client, cmd.Ticker, cmd.SecKey ?? cmd.Ticker);
                        }
                        break;

                    case ClientCommandType.Unsubscribe:
                        await _subscriptionManager.UnsubscribeClientAsync(client);
                        break;

                    case ClientCommandType.RequestSnapshot:
                        if (!string.IsNullOrEmpty(client.CurrentTicker))
                        {
                            await _subscriptionManager.SubscribeClientAsync(
                                client, client.CurrentTicker, cmd.SecKey ?? client.CurrentTicker);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _log($"Ошибка обработки команды от {client.Id}: {ex.Message}");
            }
        }

        // ********************************************************************

        public async Task StopAsync()
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
            await _clientManager.DisconnectAllAsync();
        }
    }
}
