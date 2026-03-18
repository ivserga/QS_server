// ==========================================================================
//    DataSynchronizer.cs - Синхронизация quotes и trades по timestamp
// ==========================================================================

using System.Collections.Generic;
using System.Linq;

namespace QScalp.Connector.RestApi
{
    /// <summary>
    /// Объединяет quotes и trades в единый поток событий, 
    /// отсортированный по sip_timestamp.
    /// КРИТИЧНО для правильной отрисовки кластеров!
    /// </summary>
    static class DataSynchronizer
    {
        // **********************************************************************
        // *                         Event Classes                              *
        // **********************************************************************

        /// <summary>
        /// Базовый класс для всех рыночных событий
        /// </summary>
        public abstract class MarketEvent
        {
            /// <summary>
            /// sip_timestamp в наносекундах
            /// </summary>
            public long Timestamp { get; set; }
        }

        /// <summary>
        /// Событие котировки (NBBO)
        /// </summary>
        public class QuoteEvent : MarketEvent
        {
            public QuoteResult Data { get; set; }
        }

        /// <summary>
        /// Событие сделки
        /// </summary>
        public class TradeEvent : MarketEvent
        {
            public TradeResult Data { get; set; }
        }

        // **********************************************************************
        // *                            Merge                                   *
        // **********************************************************************

        /// <summary>
        /// Объединяет и сортирует quotes + trades по timestamp.
        /// Гарантирует: событие с меньшим timestamp обрабатывается раньше.
        /// </summary>
        /// <param name="quotes">Массив котировок (может быть null)</param>
        /// <param name="trades">Массив сделок (может быть null)</param>
        /// <returns>Отсортированная последовательность событий</returns>
        public static IEnumerable<MarketEvent> Merge(
            QuoteResult[] quotes, 
            TradeResult[] trades)
        {
            var events = new List<MarketEvent>();

            // Добавляем котировки
            if (quotes != null)
            {
                foreach (var q in quotes)
                {
                    events.Add(new QuoteEvent 
                    { 
                        Timestamp = q.SipTimestamp, 
                        Data = q 
                    });
                }
            }

            // Добавляем сделки
            if (trades != null)
            {
                foreach (var t in trades)
                {
                    events.Add(new TradeEvent 
                    { 
                        Timestamp = t.SipTimestamp, 
                        Data = t 
                    });
                }
            }

            // Сортировка по времени — гарантирует правильный порядок для кластеров
            return events.OrderBy(e => e.Timestamp);
        }

        // **********************************************************************
    }
}
