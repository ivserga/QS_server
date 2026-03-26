using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QScalp.Server.Connector.WebSocket
{
    /// <summary>
    /// WebSocket клиент к massive.com.
    /// Одно соединение, динамические подписки на несколько тикеров.
    /// </summary>
    class MassiveWebSocketClient : IDisposable
    {
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private Task _receiveTask;

        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly bool _debugMode;

        private TaskCompletionSource<bool> _authTcs;

        // ********************************************************************

        public event Action<WsQuote> OnQuote;
        public event Action<WsTrade> OnTrade;
        public event Action<string> OnError;
        public event Action<string> OnRawMessage;
        public event Action OnConnected;
        public event Action OnDisconnected;

        public bool IsConnected => _ws?.State == WebSocketState.Open;

        // ********************************************************************

        public MassiveWebSocketClient(string baseUrl, string apiKey, bool debugMode = false)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey;
            _debugMode = debugMode;
        }

        // ********************************************************************

        public async Task ConnectAsync()
        {
            _cts = new CancellationTokenSource();
            _authTcs = new TaskCompletionSource<bool>();

            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(new Uri(_baseUrl), _cts.Token);

            _receiveTask = Task.Run(() => ReceiveLoopAsync());
            await Task.Delay(100);

            // Авторизация
            await SendMessageAsync(new { action = "auth", @params = _apiKey });

            var authResult = await Task.WhenAny(_authTcs.Task, Task.Delay(10000));
            if (authResult != _authTcs.Task || !_authTcs.Task.Result)
                throw new Exception("Authentication failed or timed out");

            OnConnected?.Invoke();
        }

        // ********************************************************************

        public async Task SubscribeAsync(string ticker)
        {
            await SendMessageAsync(new
            {
                action = "subscribe",
                @params = $"Q.{ticker},T.{ticker}"
            });
        }

        // ********************************************************************

        public async Task UnsubscribeAsync(string ticker)
        {
            await SendMessageAsync(new
            {
                action = "unsubscribe",
                @params = $"Q.{ticker},T.{ticker}"
            });
        }

        // ********************************************************************

        private async Task SendMessageAsync(object message)
        {
            if (_ws?.State != WebSocketState.Open) return;

            var json = JsonConvert.SerializeObject(message);
            var bytes = Encoding.UTF8.GetBytes(json);

            await _ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cts.Token);
        }

        // ********************************************************************

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[8192];
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
                            ProcessMessage(sb.ToString());
                            sb.Clear();
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException ex) { OnError?.Invoke($"WebSocket: {ex.Message}"); }
            catch (Exception ex) { OnError?.Invoke($"Unexpected: {ex.Message}"); }
            finally
            {
                OnDisconnected?.Invoke();
            }
        }

        // ********************************************************************

        private void ProcessMessage(string json)
        {
            if (_debugMode)
            {
                var preview = json.Length > 200 ? json.Substring(0, 200) + "..." : json;
                OnRawMessage?.Invoke(preview);
            }

            try
            {
                if (json.TrimStart().StartsWith("["))
                {
                    var messages = JArray.Parse(json);
                    foreach (var msg in messages)
                        ProcessSingleMessage(msg);
                }
                else
                {
                    ProcessSingleMessage(JObject.Parse(json));
                }
            }
            catch (JsonException ex)
            {
                OnError?.Invoke($"JSON parse error: {ex.Message}");
            }
        }

        // ********************************************************************

        private void ProcessSingleMessage(JToken msg)
        {
            var ev = msg["ev"]?.ToString();

            switch (ev)
            {
                case "Q":
                    var quote = msg.ToObject<WsQuote>();
                    if (quote != null) OnQuote?.Invoke(quote);
                    break;

                case "T":
                    var trade = msg.ToObject<WsTrade>();
                    if (trade != null) OnTrade?.Invoke(trade);
                    break;

                case "status":
                    var status = msg["status"]?.ToString();
                    var message = msg["message"]?.ToString();

                    if (_debugMode) OnRawMessage?.Invoke($"Status: {status} - {message}");

                    if (status == "auth_success")
                        _authTcs?.TrySetResult(true);
                    else if (status == "auth_failed")
                    {
                        _authTcs?.TrySetResult(false);
                        OnError?.Invoke($"Auth failed: {message}");
                    }
                    break;
            }
        }

        // ********************************************************************

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

        // ********************************************************************

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _ws?.Dispose();
        }
    }
}
