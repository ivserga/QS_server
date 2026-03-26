using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QScalp.Server.Broadcasting
{
    /// <summary>
    /// Сессия одного подключённого клиента.
    /// </summary>
    public class ClientSession : IDisposable
    {
        private readonly WebSocket _ws;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        public string Id { get; }
        public string RemoteAddress { get; }
        public DateTime ConnectedAt { get; }
        public string CurrentTicker { get; set; }
        public int MessageCount { get; private set; }

        public bool IsOpen => _ws.State == WebSocketState.Open;

        // ********************************************************************

        public ClientSession(WebSocket ws, string remoteAddress)
        {
            _ws = ws;
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            RemoteAddress = remoteAddress;
            ConnectedAt = DateTime.Now;
        }

        // ********************************************************************

        public async Task SendAsync(string json)
        {
            if (_ws.State != WebSocketState.Open)
                return;

            var bytes = Encoding.UTF8.GetBytes(json);
            await _sendLock.WaitAsync(_cts.Token);
            try
            {
                await _ws.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cts.Token);
                MessageCount++;
            }
            finally
            {
                _sendLock.Release();
            }
        }

        // ********************************************************************

        public async Task<string> ReceiveAsync()
        {
            var buffer = new byte[8192];
            var sb = new StringBuilder();

            while (true)
            {
                var result = await _ws.ReceiveAsync(
                    new ArraySegment<byte>(buffer), _cts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                    return null;

                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                    return sb.ToString();
            }
        }

        // ********************************************************************

        public async Task CloseAsync()
        {
            if (_ws.State == WebSocketState.Open)
            {
                try
                {
                    await _ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Server shutting down",
                        CancellationToken.None);
                }
                catch { }
            }
        }

        // ********************************************************************

        public ClientInfo ToInfo()
        {
            return new ClientInfo
            {
                Address = RemoteAddress,
                Ticker = CurrentTicker ?? "(нет)",
                ConnectedAt = ConnectedAt.ToString("HH:mm:ss"),
                MessageCount = MessageCount
            };
        }

        // ********************************************************************

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _sendLock.Dispose();
            _ws.Dispose();
        }
    }
}
