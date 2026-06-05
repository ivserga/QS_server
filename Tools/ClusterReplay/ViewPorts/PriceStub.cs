using System.Globalization;

namespace QScalp
{
  static class Price
  {
    public static double GetRaw(int price) => price / (double)ReplayConfig.PriceRatio;

    public static string GetString(int price)
    {
      return GetRaw(price).ToString("0.##", CultureInfo.InvariantCulture);
    }
  }

  static class ReplayConfig
  {
    public static int PriceRatio = 100;
    public static int PriceStep = 1;
  }
}
