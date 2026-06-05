// ==========================================================================
//  CCell.cs — Headless-версия ячейки кластера для клиента-анализатора.
// ==========================================================================
//  Хранит только данные (Buy/Sell volume) — никакой WPF-визуализации.
// ==========================================================================

namespace QScalp.Client.Clusters
{
    internal sealed class CCell
    {
        public int BuyVolume { get; private set; }
        public int SellVolume { get; private set; }
        public int NeutralVolume { get; private set; }

        public void AddBuy(int volume) { BuyVolume += volume; }
        public void AddSell(int volume) { SellVolume += volume; }
        public void AddNeutral(int volume) { NeutralVolume += volume; }
    }
}
