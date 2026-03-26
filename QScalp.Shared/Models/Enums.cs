using System;

namespace QScalp.Shared.Models
{
    public enum TradeOp { Cancel, Buy, Sell, Upsize, Downsize, Close, Reverse, Wait }
    public enum QuoteType { Unknown, Free, Spread, Ask, Bid, BestAsk, BestBid }
    public enum ClusterView { Summary, Separate, Delta }
    public enum ClusterFill { Double, SingleDelta, SingleBalance }

    [Flags]
    public enum ClusterBase
    {
        None = 0x00, Time = 0x01, Volume = 0x02,
        Range = 0x04, Ticks = 0x08, Delta = 0x10
    }
}
