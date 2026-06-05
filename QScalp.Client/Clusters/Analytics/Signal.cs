// ==========================================================================
//  Signal.cs — Базовая модель торгового сигнала, выдаваемого детекторами
// ==========================================================================

using System;

namespace QScalp.Client.Clusters.Analytics
{
    internal enum SignalKind
    {
        None,
        AbsorptionSell,
        AbsorptionBuy,
        SellingClimax,
        BuyingClimax,
        BalanceAfterClimax,
        VReversalUp,
        VReversalDown,
        HvnRejectUp,
        HvnRejectDown,
        Distribution,
        Accumulation,
        BreakoutUp,
        BreakoutDown,
        DoubleTop,
        DoubleBottom,
        OrphanCloseUp,
        OrphanCloseDown,

        // ClusterAnalyzer (legacy 3-кластерный анализ):
        BearishDivergence,
        BullishDivergence,
        BearishClimax,
        BullishClimax,
        ResistanceRejection,
        SupportRejection
    }

    internal enum SignalDirection { None, Up, Down }

    internal sealed class Signal
    {
        public DateTime Time;
        public SignalKind Kind;
        public SignalDirection Direction;
        public int Price;
        public double Strength;
        public string Message;
        public string Details;
        public string Source;
    }
}
