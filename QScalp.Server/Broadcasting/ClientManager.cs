using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

using QScalp.Shared.Protocol;

namespace QScalp.Server.Broadcasting
{
    /// <summary>
    /// Управление всеми подключёнными клиентами: добавление, удаление, рассылка.
    /// </summary>
    public class ClientManager
    {
        private readonly ConcurrentDictionary<string, ClientSession> _clients
            = new ConcurrentDictionary<string, ClientSession>();

        private readonly Action<string> _log;
        private readonly Action<ClientInfo[]> _updateUI;

        // ********************************************************************

        public ClientManager(Action<string> log, Action<ClientInfo[]> updateUI)
        {
            _log = log;
            _updateUI = updateUI;
        }

        // ********************************************************************

        public void AddClient(ClientSession client)
        {
            _clients[client.Id] = client;
            _log($"Клиент подключён: {client.RemoteAddress} [{client.Id}]");
            NotifyUI();
        }

        // ********************************************************************

        public void RemoveClient(ClientSession client)
        {
            _clients.TryRemove(client.Id, out _);
            _log($"Клиент отключён: {client.RemoteAddress} [{client.Id}]");
            client.Dispose();
            NotifyUI();
        }

        // ********************************************************************

        public ClientSession[] GetClientsForTicker(string ticker)
        {
            return _clients.Values
                .Where(c => c.IsOpen && c.CurrentTicker == ticker)
                .ToArray();
        }

        // ********************************************************************

        public string[] GetActiveTickersExcept(string ticker, string excludeClientId)
        {
            return _clients.Values
                .Where(c => c.IsOpen && c.Id != excludeClientId && c.CurrentTicker == ticker)
                .Select(c => c.CurrentTicker)
                .Distinct()
                .ToArray();
        }

        /// <summary>
        /// Проверяет, есть ли ещё клиенты, подписанные на данный тикер (кроме указанного).
        /// </summary>
        public bool HasOtherSubscribers(string ticker, string excludeClientId)
        {
            return _clients.Values.Any(c =>
                c.IsOpen && c.Id != excludeClientId && c.CurrentTicker == ticker);
        }

        // ********************************************************************

        public async Task BroadcastToTickerAsync(string ticker, string json)
        {
            var clients = GetClientsForTicker(ticker);
            var tasks = clients.Select(c => SafeSendAsync(c, json));
            await Task.WhenAll(tasks);
        }

        // ********************************************************************

        private async Task SafeSendAsync(ClientSession client, string json)
        {
            try
            {
                await client.SendAsync(json);
            }
            catch (Exception ex)
            {
                _log($"Ошибка отправки клиенту {client.Id}: {ex.Message}");
            }
        }

        // ********************************************************************

        public async Task DisconnectAllAsync()
        {
            var tasks = _clients.Values.Select(async c =>
            {
                try { await c.CloseAsync(); }
                catch { }
                c.Dispose();
            });
            await Task.WhenAll(tasks);
            _clients.Clear();
            NotifyUI();
        }

        // ********************************************************************

        public int Count => _clients.Count;

        public void NotifyUI()
        {
            var info = _clients.Values
                .Select(c => c.ToInfo())
                .ToArray();
            _updateUI(info);
        }
    }
}
