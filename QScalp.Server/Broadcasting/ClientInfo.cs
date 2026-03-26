using System;

namespace QScalp.Server.Broadcasting
{
    /// <summary>
    /// Информация о клиенте для отображения в UI.
    /// </summary>
    public class ClientInfo
    {
        public string Address { get; set; }
        public string Ticker { get; set; }
        public string ConnectedAt { get; set; }
        public int MessageCount { get; set; }
    }
}
