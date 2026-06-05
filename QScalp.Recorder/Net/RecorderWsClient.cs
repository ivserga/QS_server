using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using QScalp.Shared.Protocol;

namespace QScalp.Recorder.Net
{
    /// <summary>Минимальный WS-клиент: одно подключение — один тикер.</summary>
    public sealed class RecorderWsClient : IDisposable
    {
        private readonly string _serverUrl;
        private readonly string _ticker;
        private readonly string _secKey;
        private readonly bool _recordSnapshots;

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private Task _runTask;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        public event Action<string> OnRecordableMessage;
        public event Action<string> OnLog;
        public event Action<string> OnError;

        public RecorderWsClient(string serverUrl, string ticker, string secKey, bool recordSnapshots)
        {
            _serverUrl = serverUrl;
            _ticker = ticker.Trim().ToUpperInvariant();
            _secKey = string.IsNullOrWhiteSpace(secKey) ? _ticker : secKey.Trim();
            _recordSnapshots = recordSnapshots;
        }

        public void Start()
        {
            if (_runTask != null && !_runTask.IsCompleted) return;
            _cts = new CancellationTokenSource();
            _runTask = Task.Run(() => RunLoopAsync(_cts.Token));
        }

        public async Task StopAsync()
        {
            if (_cts == null) return;
            _cts.Cancel();
            try
            {
                if (_ws?.State == WebSocketState.Open)
                {
                    using (var t = new CancellationTokenSource(2000))
                        await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "stop", t.Token);
                }
            }
            catch { }
            try { if (_runTask != null) await _runTask; } catch { }
            _ws?.Dispose();
            _ws = null;
            _cts.Dispose();
            _cts = null;
            _runTask = null;
        }

        private async Task RunLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    _ws = new ClientWebSocket();
                    Log($"[{_ticker}] connect {_serverUrl}");
                    await _ws.ConnectAsync(new Uri(_serverUrl), ct);
                    Log($"[{_ticker}] subscribed");

                    await SendAsync(new ClientCommand
                    {
                        Type = ClientCommandType.Subscribe,
                        Ticker = _ticker,
                        SecKey = _secKey
                    }, ct);

                    await ReceiveLoopAsync(ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { OnError?.Invoke(ex.Message); }
                finally
                {
                    try { _ws?.Dispose(); } catch { }
                    _ws = null;
                }

                if (ct.IsCancellationRequested) break;
                try { await Task.Delay(3000, ct); } catch (OperationCanceledException) { break; }
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[16 * 1024];
            var sb = new StringBuilder();

            while (_ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                sb.Clear();
                while (true)
                {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (result.EndOfMessage) break;
                }

                TryRecord(sb.ToString());
            }
        }

        private void TryRecord(string json)
        {
            ServerMessage msg;
            try { msg = ProtocolHelper.DeserializeServerMessage(json); }
            catch { return; }

            if (msg == null) return;

            switch (msg.Type)
            {
                case ServerMessageType.StockUpdate:
                case ServerMessageType.TradeUpdate:
                    OnRecordableMessage?.Invoke(json);
                    break;
                case ServerMessageType.Snapshot when _recordSnapshots:
                    OnRecordableMessage?.Invoke(json);
                    break;
            }
        }

        private async Task SendAsync(ClientCommand cmd, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(ProtocolHelper.SerializeCommand(cmd));
            await _sendLock.WaitAsync(ct);
            try
            {
                await _ws.SendAsync(new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text, true, ct);
            }
            finally { _sendLock.Release(); }
        }

        private void Log(string s) => OnLog?.Invoke(s);

        public void Dispose()
        {
            try { StopAsync().GetAwaiter().GetResult(); } catch { }
            _sendLock.Dispose();
        }
    }
}
