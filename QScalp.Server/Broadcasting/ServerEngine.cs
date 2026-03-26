using System;
using System.Threading.Tasks;

using QScalp.Server.Config;
using QScalp.Server.Connector;

namespace QScalp.Server.Broadcasting
{
    /// <summary>
    /// Главный движок сервера: связывает MassiveConnector, ClientManager,
    /// SubscriptionManager и InternalWsServer.
    /// </summary>
    public class ServerEngine
    {
        private readonly ServerConfig _config;
        private readonly Action<string> _log;
        private readonly Action<ClientInfo[]> _updateUI;

        private MassiveConnector _connector;
        private ClientManager _clientManager;
        private SubscriptionManager _subscriptionManager;
        private InternalWsServer _wsServer;

        // ********************************************************************

        public ServerEngine(
            ServerConfig config,
            Action<string> log,
            Action<ClientInfo[]> updateUI)
        {
            _config = config;
            _log = log;
            _updateUI = updateUI;
        }

        // ********************************************************************

        public async Task StartAsync()
        {
            // 1. Подключаемся к massive.com
            _log("Подключение к massive.com...");
            _connector = new MassiveConnector(
                _config.ApiBaseUrl,
                _config.WsBaseUrl,
                _config.ApiKey,
                _config.DebugMode,
                _log);
            await _connector.ConnectAsync();

            // 2. Создаём менеджер клиентов
            _clientManager = new ClientManager(_log, _updateUI);

            // 3. Менеджер подписок
            _subscriptionManager = new SubscriptionManager(
                _connector, _clientManager, _log);

            // 4. Запускаем внутренний WS-сервер для клиентов
            _wsServer = new InternalWsServer(
                _config.ListenUrl,
                _config.MaxClients,
                _clientManager,
                _subscriptionManager,
                _log);
            _wsServer.Start();
        }

        // ********************************************************************

        public async Task StopAsync()
        {
            if (_wsServer != null)
                await _wsServer.StopAsync();

            _connector?.Dispose();

            _log("Все соединения закрыты.");
        }
    }
}
