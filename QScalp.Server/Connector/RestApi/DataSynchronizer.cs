using System.Collections.Generic;
using System.Linq;

namespace QScalp.Server.Connector.RestApi
{
    static class DataSynchronizer
    {
        public abstract class MarketEvent
        {
            public long Timestamp { get; set; }
        }

        public class QuoteEvent : MarketEvent
        {
            public QuoteResult Data { get; set; }
        }

        public class TradeEvent : MarketEvent
        {
            public TradeResult Data { get; set; }
        }

        public static IEnumerable<MarketEvent> Merge(QuoteResult[] quotes, TradeResult[] trades)
        {
            var events = new List<MarketEvent>();

            if (quotes != null)
                foreach (var q in quotes)
                    events.Add(new QuoteEvent { Timestamp = q.SipTimestamp, Data = q });

            if (trades != null)
                foreach (var t in trades)
                    events.Add(new TradeEvent { Timestamp = t.SipTimestamp, Data = t });

            return events.OrderBy(e => e.Timestamp);
        }
    }
}
