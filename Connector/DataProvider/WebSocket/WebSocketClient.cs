// ==========================================================================
//    WebSocketClient.cs - WebSocket клиент для real-time данных
// ==========================================================================

using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QScalp.Connector.WebSocket
{
    /// <summary>
    /// WebSocket клиент для получения real-time quotes и trades.
    /// Подключается к единому endpoint и подписывается на Q и T через сообщения.
    /// </summary>
    class WebSocketClient : IDisposable
    {
        // **********************************************************************

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private Task _receiveTask;
        
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _ticker;
        private readonly bool _debugMode;

        public string BaseUrl => _baseUrl;

        // **********************************************************************
        
        /// <summary>
        /// Событие получения quote
        /// </summary>
        public event Action<WsQuote> OnQuote;
        
        /// <summary>
        /// Событие получения trade
        /// </summary>
        public event Action<WsTrade> OnTrade;
        
        /// <summary>
        /// Событие ошибки
        /// </summary>
        public event Action<string> OnError;
        
        /// <summary>
        /// Событие получения raw сообщения (для отладки)
        /// </summary>
        public event Action<string> OnRawMessage;
        
        /// <summary>
        /// Событие подключения
        /// </summary>
        public event Action OnConnected;
        
        /// <summary>
        /// Событие отключения
        /// </summary>
        public event Action OnDisconnected;

        // **********************************************************************

        public bool IsConnected => _ws?.State == WebSocketState.Open;

        // **********************************************************************

        public WebSocketClient(string baseUrl, string apiKey, string ticker, bool debugMode = false)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey;
            _ticker = ticker;
            _debugMode = debugMode;
        }

        // **********************************************************************

        // Для ожидания авторизации
        private TaskCompletionSource<bool> _authTcs;
        private bool _isAuthenticated;

        /// <summary>
        /// Подключение к WebSocket и подписка на quotes и trades
        /// </summary>
        public async Task ConnectAsync()
        {
            _cts = new CancellationTokenSource();
            _authTcs = new TaskCompletionSource<bool>();
            _isAuthenticated = false;
            
            try
            {
                _ws = new ClientWebSocket();
                
                if (_debugMode)
                    OnRawMessage?.Invoke($"Connecting to: {_baseUrl}");
                
                await _ws.ConnectAsync(new Uri(_baseUrl), _cts.Token);
                
                if (_debugMode)
                    OnRawMessage?.Invoke("TCP connected, starting receive loop...");
                
                _receiveTask = Task.Run(() => ReceiveLoopAsync());
                
                await Task.Delay(100);
                
                if (_debugMode)
                    OnRawMessage?.Invoke("Sending auth...");
                
                await AuthenticateAsync();
                
                var authResult = await Task.WhenAny(_authTcs.Task, Task.Delay(10000));
                if (authResult != _authTcs.Task || !_authTcs.Task.Result)
                {
                    throw new Exception("Authentication failed or timed out");
                }
                
                if (_debugMode)
                    OnRawMessage?.Invoke("Auth OK, subscribing...");
                
                await SubscribeAsync();
                
                if (_debugMode)
                    OnRawMessage?.Invoke($"Subscribed to Q.{_ticker}, T.{_ticker}");
                
                OnConnected?.Invoke();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"WebSocket connect error: {ex.Message}");
                throw;
            }
        }

        // **********************************************************************

        /// <summary>
        /// Авторизация на WebSocket сервере
        /// </summary>
        private async Task AuthenticateAsync()
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                OnError?.Invoke("API key is not set");
                _authTcs.TrySetResult(false);
                return;
            }
            
            var authMessage = new WsSubscribe 
            { 
                Action = "auth", 
                Params = _apiKey 
            };
            await SendMessageAsync(authMessage);
            
            System.Diagnostics.Debug.WriteLine($"[WS] Auth message sent");
        }

        // **********************************************************************

        /// <summary>
        /// Подписка на quotes (Q) и trades (T) для тикера
        /// </summary>
        private async Task SubscribeAsync()
        {
            // Подписываемся на quotes и trades одним сообщением
            var subscribe = new WsSubscribe 
            { 
                Action = "subscribe", 
                Params = $"Q.{_ticker},T.{_ticker}" 
            };
            await SendMessageAsync(subscribe);
            
            System.Diagnostics.Debug.WriteLine($"[WS] Subscribed to Q.{_ticker} and T.{_ticker}");
        }

        // **********************************************************************

        /// <summary>
        /// Отправка сообщения в WebSocket
        /// </summary>
        private async Task SendMessageAsync(object message)
        {
            if (_ws?.State != WebSocketState.Open)
                return;
            
            var json = JsonConvert.SerializeObject(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            System.Diagnostics.Debug.WriteLine($"[WS] Sending: {json}");
            
            await _ws.SendAsync(
                new ArraySegment<byte>(bytes), 
                WebSocketMessageType.Text, 
                true, 
                _cts.Token);
        }

        // **********************************************************************

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[8192];
            var messageBuilder = new StringBuilder();
            
            try
            {
                while (_ws.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    var result = await _ws.ReceiveAsync(
                        new ArraySegment<byte>(buffer), 
                        _cts.Token);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        System.Diagnostics.Debug.WriteLine("[WS] Connection closed by server");
                        break;
                    }
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        messageBuilder.Append(chunk);
                        
                        if (result.EndOfMessage)
                        {
                            var message = messageBuilder.ToString();
                            messageBuilder.Clear();
                            
                            ProcessMessage(message);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Нормальное завершение
            }
            catch (WebSocketException ex)
            {
                OnError?.Invoke($"WebSocket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"WebSocket unexpected error: {ex.Message}");
            }
            finally
            {
                OnDisconnected?.Invoke();
            }
        }

        // **********************************************************************

        private int _rawMsgCount;
        
        private void ProcessMessage(string json)
        {
            _rawMsgCount++;
            
            // Логируем только в debug mode
            if (_debugMode && (_rawMsgCount <= 5 || _rawMsgCount % 100 == 0))
            {
                var preview = json.Length > 200 ? json.Substring(0, 200) + "..." : json;
                System.Diagnostics.Debug.WriteLine($"[WS] Raw #{_rawMsgCount}: {preview}");
                OnRawMessage?.Invoke($"Raw #{_rawMsgCount}: {preview}");
            }
            
            try
            {
                // Сервер может отправлять массив сообщений или одиночное сообщение
                if (json.TrimStart().StartsWith("["))
                {
                    var messages = JArray.Parse(json);
                    foreach (var msg in messages)
                    {
                        ProcessSingleMessage(msg);
                    }
                }
                else
                {
                    var msg = JObject.Parse(json);
                    ProcessSingleMessage(msg);
                }
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WS] JSON parse error: {ex.Message}, json: {json}");
                OnError?.Invoke($"JSON parse error: {ex.Message}");
            }
        }

        // **********************************************************************

        private void ProcessSingleMessage(JToken msg)
        {
            var ev = msg["ev"]?.ToString();
            
            switch (ev)
            {
                case "Q":
                    var quote = msg.ToObject<WsQuote>();
                    if (quote != null)
                        OnQuote?.Invoke(quote);
                    break;
                    
                case "T":
                    var trade = msg.ToObject<WsTrade>();
                    if (trade != null)
                        OnTrade?.Invoke(trade);
                    break;
                    
                case "status":
                    var status = msg.ToObject<WsStatus>();
                    
                    if (_debugMode)
                        OnRawMessage?.Invoke($"Status: {status?.Status} - {status?.Message}");
                    
                    if (status?.Status == "auth_success")
                    {
                        _isAuthenticated = true;
                        _authTcs?.TrySetResult(true);
                    }
                    else if (status?.Status == "auth_failed" || 
                             (status?.Status == "error" && status?.Message?.Contains("auth") == true))
                    {
                        _authTcs?.TrySetResult(false);
                        OnError?.Invoke($"Authentication failed: {status?.Message}");
                    }
                    break;
                    
                default:
                    if (_debugMode)
                        OnRawMessage?.Invoke($"Unknown event: {ev}, msg: {msg}");
                    break;
            }
        }

        // **********************************************************************

        /// <summary>
        /// Отключение от WebSocket
        /// </summary>
        public async Task DisconnectAsync()
        {
            _cts?.Cancel();
            
            // Закрываем WebSocket
            if (_ws?.State == WebSocketState.Open)
            {
                try
                {
                    await _ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, 
                        "Client disconnect", 
                        CancellationToken.None);
                }
                catch
                {
                    // Игнорируем ошибки при закрытии
                }
            }
            
            // Ждем завершения receive task
            if (_receiveTask != null)
            {
                try
                {
                    await Task.WhenAny(_receiveTask, Task.Delay(5000));
                }
                catch
                {
                    // Игнорируем
                }
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
    }
}
