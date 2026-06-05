using System;
using System.Threading.Tasks;

using QScalp.Server.Config;
using QScalp.Server.Connector;

namespace QScalp.Server.Broadcasting
{
    /// <summary>
    /// Главный движок сервера: связывает IMarketDataConnector (Massive или Striker), ClientManager,
    /// SubscriptionManager и InternalWsServer.
    /// </summary>
    public class ServerEngine
    {
        private readonly ServerConfig _config;
        private readonly Action<string> _log;
        private readonly Action<ClientInfo[]> _updateUI;

        private IMarketDataConnector _connector;
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
            // 1. Источник данных
            if (IsReplay(_config))
            {
                _log("Режим воспроизведения записи...");
                _connector = new ReplayConnector(
                    _config.ReplaySessionPath,
                    _config.ReplaySpeed,
                    _config.ReplayLoop,
                    _log);
            }
            else if (IsStrikerWs(_config))
            {
                _log("Режим Striker WS API...");
                _connector = new StrikerWsConnector(_config, _log);
            }
            else if (IsStriker(_config))
            {
                _log("Режим Striker (HTTP API)...");
                _connector = new StrikerConnector(_config, _log);
            }
            else
            {
                _log("Режим Massive (WebSocket)...");
                _connector = new MassiveConnector(
                    _config.ApiBaseUrl,
                    _config.WsBaseUrl,
                    _config.ApiKey,
                    _config.DebugMode,
                    _log);
            }

            await _connector.ConnectAsync();

            // 2. Создаём менеджер клиентов
            _clientManager = new ClientManager(_log, _updateUI);

            // 3. Менеджер подписок
            _subscriptionManager = new SubscriptionManager(
                _connector, _clientManager, _config, _log);

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

        private static bool IsReplay(ServerConfig cfg) =>
            string.Equals(cfg?.DataSource?.Trim(), "Replay", StringComparison.OrdinalIgnoreCase);

        private static bool IsStriker(ServerConfig cfg) =>
            string.Equals(cfg?.DataSource?.Trim(), "Striker", StringComparison.OrdinalIgnoreCase);

        private static bool IsStrikerWs(ServerConfig cfg) =>
            string.Equals(cfg?.DataSource?.Trim(), "StrikerWs", StringComparison.OrdinalIgnoreCase);
    }
}
