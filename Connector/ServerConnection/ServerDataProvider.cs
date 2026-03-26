// ==========================================================================
//    ServerDataProvider.cs - Провайдер данных через QScalp.Server
// ==========================================================================

using System;
using System.Threading.Tasks;

namespace QScalp.Connector.ServerConnection
{
    /// <summary>
    /// Провайдер данных, подключающийся к QScalp.Server вместо massive.com.
    /// Имеет тот же интерфейс, что и оригинальный DataProvider.
    /// </summary>
    class ServerDataProvider : IDisposable
    {
        // **********************************************************************

        private ServerConnectionClient _client;

        private readonly IDataReceiver _receiver;
        private readonly TermManager _tmgr;

        // **********************************************************************

        public bool IsConnected => _client?.IsConnected ?? false;
        public bool IsError { get; private set; }

        // **********************************************************************

        public ServerDataProvider(IDataReceiver receiver, TermManager tmgr)
        {
            _receiver = receiver;
            _tmgr = tmgr;
        }

        // **********************************************************************

        public void Connect()
        {
            string serverUrl = cfg.u.ServerUrl;
            string ticker = cfg.u.SecCode;
            string secKey = cfg.u.SecCode + cfg.u.ClassCode;

            if (string.IsNullOrEmpty(serverUrl))
            {
                _receiver.PutMessage(new Message("Адрес сервера не задан. Укажите в настройках."));
                return;
            }

            _client = new ServerConnectionClient(serverUrl, _receiver, _tmgr,
                cfg.u.SkipHistoricalData, cfg.u.TradeFilterTicks);

            _client.OnConnected += () =>
                _receiver.PutMessage(new Message("Подключён к серверу"));

            _client.OnDisconnected += () =>
                _receiver.PutMessage(new Message("Отключён от сервера"));

            _client.OnError += err =>
            {
                IsError = true;
                _receiver.PutMessage(new Message($"Ошибка: {err}"));
            };

            _client.OnLog += msg =>
                _receiver.PutMessage(new Message(msg));

            Task.Run(async () =>
            {
                try
                {
                    await _client.ConnectAsync();
                    await _client.SubscribeAsync(ticker, secKey);
                }
                catch (Exception ex)
                {
                    IsError = true;
                    _receiver.PutMessage(new Message($"Ошибка подключения: {ex.Message}"));
                }
            });
        }

        // **********************************************************************

        public void Disconnect()
        {
            if (_client != null)
            {
                Task.Run(async () =>
                {
                    try { await _client.DisconnectAsync(); }
                    catch { }
                }).Wait(TimeSpan.FromSeconds(5));

                _client.Dispose();
                _client = null;
            }
        }

        // **********************************************************************

        /// <summary>
        /// Смена тикера без переподключения.
        /// </summary>
        public void ChangeTicker(string ticker, string secKey)
        {
            if (_client == null || !_client.IsConnected) return;

            Task.Run(async () =>
            {
                try
                {
                    await _client.UnsubscribeAsync();
                    await _client.SubscribeAsync(ticker, secKey);
                }
                catch (Exception ex)
                {
                    _receiver.PutMessage(new Message($"Ошибка смены тикера: {ex.Message}"));
                }
            });
        }

        // **********************************************************************

        public void Dispose()
        {
            Disconnect();
        }
    }
}
