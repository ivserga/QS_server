// ====================================================================
//    Price.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// ====================================================================

using System;

namespace QScalp
{
  static class Price
  {
    // **********************************************************************

    public static int Floor(int price)
    {
      return price / cfg.u.PriceStep * cfg.u.PriceStep;
    }

    // **********************************************************************

    public static int Ceil(int price)
    {
      return ((price - 1) / cfg.u.PriceStep + 1) * cfg.u.PriceStep;
    }

    // **********************************************************************

    public static int GetInt(double raw)
    {
      return (int)Math.Round(raw * cfg.u.PriceRatio);
    }

    // **********************************************************************

    public static int GetInt(double raw, int ratio)
    {
      return (int)Math.Round(raw * ratio);
    }

    // **********************************************************************

    public static double GetRaw(int price)
    {
      return (double)price / cfg.u.PriceRatio;
    }

    // **********************************************************************

    public static double GetRaw(int price, int ratio)
    {
      return (double)price / ratio;
    }

    // **********************************************************************

    public static string GetString(int price)
    {
      return ((double)price / cfg.u.PriceRatio).ToString("N", cfg.PriceFormat);
    }

    // **********************************************************************

    public static string GetString(int price, int ratio)
    {
      return ((double)price / ratio).ToString("N" + (int)Math.Log10(ratio));
    }

    // **********************************************************************
  }
}
