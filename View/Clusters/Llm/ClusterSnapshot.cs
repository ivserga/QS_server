// ==========================================================================
//  ClusterSnapshot.cs — лёгкий кэш закрытого бара для pruning summary.
// ==========================================================================
//  Хранит только поля, нужные для построения синтетической сводки
//  пропущенных баров (без тяжёлого price_levels[]). Используется
//  ClusterLlmSession-ом при превышении LlmMaxContextTokens.
// ==========================================================================

using System.Globalization;
using System.Text;

namespace QScalp.View.ClustersSpace.Llm
{
  struct ClusterSnapshot
  {
    public int Index;
    public System.DateTime StartTime;
    public int TotalVolume;
    public int Delta;
    public int ClosePrice;
    public double RawClosePrice;

    public ClusterSnapshot(int index, Cluster cluster)
    {
      Index = index;
      StartTime = cluster.DateTime;
      TotalVolume = cluster.Volume;
      Delta = cluster.Delta;
      ClosePrice = cluster.ClosePrice;
      RawClosePrice = Price.GetRaw(cluster.ClosePrice);
    }

    public string FormatSummary()
    {
      return new StringBuilder(64)
        .Append('#').Append(Index.ToString(CultureInfo.InvariantCulture))
        .Append(" t=").Append(StartTime.ToString("HH:mm", CultureInfo.InvariantCulture))
        .Append(" vol=").Append(TotalVolume.ToString(CultureInfo.InvariantCulture))
        .Append(" delta=").Append(Delta.ToString(CultureInfo.InvariantCulture))
        .Append(" close=").Append(RawClosePrice.ToString("0.######", CultureInfo.InvariantCulture))
        .ToString();
    }
  }
}
