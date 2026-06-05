// ==========================================================================
//  ClusterPriceLevel.cs — Снимок одного ценового уровня внутри кластера.
// ==========================================================================
//  Формат полей совпадает с QScalp.View.ClustersSpace.ClusterPriceLevel
//  для сравнения JSON-экспорта Client и WPF.
// ==========================================================================

using Newtonsoft.Json;

namespace QScalp.Client.Clusters
{
    internal sealed class ClusterPriceLevel
    {
        readonly int _priceStep;

        public ClusterPriceLevel(int price, int bidVolume, int askVolume, int insideSpreadVolume, int priceStep)
        {
            Price = price;
            BidVolume = bidVolume;
            AskVolume = askVolume;
            InsideSpreadVolume = insideSpreadVolume;
            _priceStep = priceStep;
        }

        [JsonProperty("price_int")]
        public int Price { get; private set; }

        [JsonProperty("price")]
        public double RawPrice => IntPrice.GetRaw(Price);

        [JsonProperty("price_text")]
        public string PriceText => IntPrice.GetString(Price, _priceStep);

        [JsonProperty("bid")]
        public int BidVolume { get; private set; }

        [JsonProperty("ask")]
        public int AskVolume { get; private set; }

        [JsonProperty("inside_spread")]
        public int InsideSpreadVolume { get; private set; }

        [JsonProperty("delta")]
        public int Delta => AskVolume - BidVolume;

        [JsonProperty("total")]
        public int Total => BidVolume + AskVolume + InsideSpreadVolume;
    }
}
