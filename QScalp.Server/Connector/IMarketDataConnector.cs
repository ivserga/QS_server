using System;
using System.Threading.Tasks;
using QScalp.Server.Broadcasting;
using QScalp.Shared.Models;

namespace QScalp.Server.Connector
{
    /// <summary>
    /// Источник рыночных данных для сервера (Massive WebSocket, Striker HTTP и т.д.).
    /// </summary>
    public interface IMarketDataConnector : IDisposable
    {
        string SourceName { get; }

        event Action<string, Quote[], Spread> OnQuoteReceived;
        event Action<string, string, Trade> OnTradeReceived;

        Task ConnectAsync();
        Task SubscribeTickerAsync(string ticker);
        Task UnsubscribeTickerAsync(string ticker);
        Task LoadSnapshotAsync(string ticker, TickerBuffer buffer);
    }

    /// <summary>
    /// Дополнительная возможность источника: заполнить торговый тикет в терминале.
    /// HTTP API Striker не отправляет ордер напрямую, а только открывает/заполняет
    /// Trade Ticket для ручного подтверждения пользователем.
    /// </summary>
    public interface ITradeTicketConnector
    {
        Task FillTradeTicketAsync(string ticker, string side, int quantity, double signalStrength, string signalMessage);
    }
}
