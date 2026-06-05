// ==========================================================================
//  ClusterPriceLevel.cs - Snapshot of one price level inside a cluster.
// ==========================================================================

using Newtonsoft.Json;

namespace QScalp.View.ClustersSpace
{
  sealed class ClusterPriceLevel
  {
    // **********************************************************************

    public ClusterPriceLevel(int price, int bidVolume, int askVolume, int insideSpreadVolume)
    {
      Price = price;
      BidVolume = bidVolume;
      AskVolume = askVolume;
      InsideSpreadVolume = insideSpreadVolume;
    }

    // **********************************************************************

    [JsonProperty("price_int")]
    public int Price { get; private set; }

    [JsonProperty("price")]
    public double RawPrice { get { return QScalp.Price.GetRaw(Price); } }

    [JsonProperty("price_text")]
    public string PriceText { get { return QScalp.Price.GetString(Price); } }

    [JsonProperty("bid")]
    public int BidVolume { get; private set; }

    [JsonProperty("ask")]
    public int AskVolume { get; private set; }

    [JsonProperty("inside_spread")]
    public int InsideSpreadVolume { get; private set; }

    [JsonProperty("delta")]
    public int Delta { get { return AskVolume - BidVolume; } }

    [JsonProperty("total")]
    public int Total { get { return BidVolume + AskVolume + InsideSpreadVolume; } }

    // **********************************************************************
  }
}
