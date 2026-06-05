// ==========================================================================
//  ClusterStats.cs — Метрики распределения объёма по ценам внутри кластера
// ==========================================================================
//
//  Считает всё, что нужно детекторам для распознавания absorption / climax /
//  разворотов, на основе формы volume-профиля:
//
//    • CenterOfMass (VWAP профиля), PosCom, ComPrice
//    • Value Area (VAL/VAH) — диапазон, вмещающий ValueAreaShare объёма
//    • MeanPrice, StdDev, Skewness, Kurtosis распределения объёма по ценам
//    • LVN-зоны — "вакуумные" участки внутри бара
//    • TopN — N самых объёмных уровней подряд (для климакс-концентрации)
//    • Shape — эвристическая форма профиля (P / b / D / Thin / Trend)
//
//  Вход: Cluster + priceStep.
//  Источник объёмов: Cluster.GetCellVolume(price) — никакого изменения Cluster.
//
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Globalization;

namespace QScalp.View.ClustersSpace.Analytics
{
  // ==========================================================================

  enum ProfileShape
  {
    /// <summary>Недостаточно данных / вырожденный бар.</summary>
    Unknown,
    /// <summary>Нормальное симметричное распределение (D-shape).</summary>
    Balanced,
    /// <summary>Основная масса объёма вверху бара, тонкий хвост внизу — покупатели контролируют.</summary>
    TopHeavy,
    /// <summary>Основная масса объёма внизу бара, тонкий хвост вверху — продавцы контролируют.</summary>
    BottomHeavy,
    /// <summary>Очень узкое, островершинное распределение (климакс / выстрел на уровне).</summary>
    Thin,
    /// <summary>Трендовое распределение: центр массы близко к одной из границ бара.</summary>
    Trending
  }

  // ==========================================================================

  /// <summary>
  /// LVN-диапазон — последовательность ячеек, где объём на уровне заметно
  /// ниже средней "плотности" профиля.
  /// </summary>
  struct LvnRange
  {
    public int From;        // нижняя цена диапазона
    public int To;          // верхняя цена диапазона
    public long Volume;     // суммарный объём в диапазоне

    public LvnRange(int from, int to, long volume)
    {
      this.From = from;
      this.To = to;
      this.Volume = volume;
    }
  }

  // ==========================================================================

  sealed class ClusterStats
  {
    // **********************************************************************

    public Cluster Source { get; private set; }
    public int PriceStep { get; private set; }

    // --- базовые ----------------------------------------------------------

    public int Volume { get; private set; }
    public int Ticks { get; private set; }
    public int Delta { get; private set; }

    public long BuyVolume { get; private set; }
    public long SellVolume { get; private set; }
    public long InsideSpreadVolume { get; private set; }
    public long AggressiveVolume { get { return BuyVolume + SellVolume; } }

    public double InsideSpreadShare
    {
      get { return Volume > 0 ? InsideSpreadVolume / (double)Volume : 0.0; }
    }

    public double AggressiveShare
    {
      get { return Volume > 0 ? AggressiveVolume / (double)Volume : 0.0; }
    }

    public double DeltaShare
    {
      get { return Volume > 0 ? Delta / (double)Volume : 0.0; }
    }

    public double AggressiveDeltaShare
    {
      get { return AggressiveVolume > 0 ? Delta / (double)AggressiveVolume : 0.0; }
    }

    public int OpenPrice { get; private set; }
    public int ClosePrice { get; private set; }
    public int MinPrice { get; private set; }
    public int MaxPrice { get; private set; }

    /// <summary>Высота бара в тиках (MaxPrice - MinPrice).</summary>
    public int Range { get { return MaxPrice - MinPrice; } }

    // --- Value Area -------------------------------------------------------

    /// <summary>Верхняя граница Value Area (цены, вмещающие ValueAreaShare объёма).</summary>
    public int VAH { get; private set; }
    /// <summary>Нижняя граница Value Area.</summary>
    public int VAL { get; private set; }
    /// <summary>Доля объёма, вмещённая в Value Area [0..1] (≈ 0.70 для стандартной VA).</summary>
    public double VaActualShare { get; private set; }

    // --- моменты распределения -------------------------------------------

    /// <summary>Взвешенное среднее цен по объёму (VWAP профиля).</summary>
    public double CenterOfMass { get; private set; }
    /// <summary>Позиция центра масс внутри бара: 0 = Min, 1 = Max. Для range=0 — 0.5.</summary>
    public double PosCom { get; private set; }
    /// <summary>Ближайший тик к CenterOfMass.</summary>
    public int ComPrice { get; private set; }
    /// <summary>Взвешенное стандартное отклонение цен по объёму.</summary>
    public double StdDev { get; private set; }
    /// <summary>Асимметрия (skewness) распределения объёма по ценам. &gt;0 — правый хвост, &lt;0 — левый.</summary>
    public double Skewness { get; private set; }
    /// <summary>Эксцесс (kurtosis). &gt;3 — островершинное, &lt;3 — плоское.</summary>
    public double Kurtosis { get; private set; }

    // --- форма и концентрация --------------------------------------------

    /// <summary>Доля объёма в 3 самых объёмных соседних тиках от общего объёма.</summary>
    public double Top3Share { get; private set; }
    /// <summary>Нижняя граница тройки самых объёмных соседних тиков.</summary>
    public int Top3From { get; private set; }
    /// <summary>Верхняя граница тройки самых объёмных соседних тиков.</summary>
    public int Top3To { get; private set; }

    /// <summary>Эвристическая форма профиля (см. <see cref="ProfileShape"/>).</summary>
    public ProfileShape Shape { get; private set; }

    /// <summary>LVN-диапазоны ("вакуум") внутри бара.</summary>
    public IList<LvnRange> Lvn { get; private set; }

    // **********************************************************************

    const double VaShareTarget = 0.70;

    // **********************************************************************

    ClusterStats() { }

    // **********************************************************************

    /// <summary>
    /// Вычисляет все метрики распределения для закрытого (или текущего) кластера.
    /// Возвращает null, если кластер пустой.
    /// </summary>
    public static ClusterStats Compute(Cluster c, int priceStep)
    {
      if(c == null || c.Volume == 0 || priceStep <= 0)
        return null;

      if(c.MinPrice == int.MaxValue || c.MaxPrice < c.MinPrice)
        return null;

      int min = c.MinPrice;
      int max = c.MaxPrice;
      int range = max - min;

      int nLevels = range / priceStep + 1;
      if(nLevels <= 0)
        return null;

      // ------------------------------------------------------------
      //  Собираем плотный массив объёмов по уровням

      long[] vols = new long[nLevels];
      long total = 0;

      int vaSeedIdx = 0;
      long vaSeedVol = 0;

      for(int i = 0; i < nLevels; i++)
      {
        int price = min + i * priceStep;
        long v = c.GetCellVolume(price);
        vols[i] = v;
        total += v;

        if(v > vaSeedVol)
        {
          vaSeedVol = v;
          vaSeedIdx = i;
        }
      }

      if(total <= 0)
        return null;

      long buyVolume = 0;
      long sellVolume = 0;
      long insideSpreadVolume = 0;
      IList<ClusterPriceLevel> levels = c.GetPriceLevels();
      for(int i = 0; i < levels.Count; i++)
      {
        ClusterPriceLevel level = levels[i];
        buyVolume += level.AskVolume;
        sellVolume += level.BidVolume;
        insideSpreadVolume += level.InsideSpreadVolume;
      }

      var s = new ClusterStats
      {
        Source = c,
        PriceStep = priceStep,
        Volume = c.Volume,
        Ticks = c.Ticks,
        Delta = c.Delta,
        BuyVolume = buyVolume,
        SellVolume = sellVolume,
        InsideSpreadVolume = insideSpreadVolume,
        OpenPrice = c.OpenPrice,
        ClosePrice = c.ClosePrice,
        MinPrice = min,
        MaxPrice = max
      };

      // ------------------------------------------------------------
      //  Value Area: расширяемся от уровня с max объёмом (vaSeed).

      long accum = vaSeedVol;
      int lo = vaSeedIdx;
      int hi = vaSeedIdx;

      while(accum < total * VaShareTarget && (lo > 0 || hi < nLevels - 1))
      {
        long leftPair  = 0;
        long rightPair = 0;

        if(lo > 0)
          leftPair = vols[lo - 1] + (lo > 1 ? vols[lo - 2] : 0);

        if(hi < nLevels - 1)
          rightPair = vols[hi + 1] + (hi < nLevels - 2 ? vols[hi + 2] : 0);

        if(lo == 0)
        {
          hi = Math.Min(hi + 2, nLevels - 1);
        }
        else if(hi == nLevels - 1)
        {
          lo = Math.Max(lo - 2, 0);
        }
        else if(rightPair >= leftPair)
        {
          hi = Math.Min(hi + 2, nLevels - 1);
        }
        else
        {
          lo = Math.Max(lo - 2, 0);
        }

        // пересчёт accum простым способом, чтобы не накопить ошибок:
        accum = 0;
        for(int k = lo; k <= hi; k++)
          accum += vols[k];
      }

      s.VAL = min + lo * priceStep;
      s.VAH = min + hi * priceStep;
      s.VaActualShare = (double)accum / total;

      // ------------------------------------------------------------
      //  Моменты распределения (взвешенные по объёму)

      double sumPv = 0;
      for(int i = 0; i < nLevels; i++)
        sumPv += (double)vols[i] * (min + i * priceStep);

      double mean = sumPv / total;
      s.CenterOfMass = mean;
      s.PosCom = range > 0 ? (mean - min) / range : 0.5;
      s.ComPrice = NearestTickPrice(mean, priceStep, min, max);

      double m2 = 0, m3 = 0, m4 = 0;
      for(int i = 0; i < nLevels; i++)
      {
        if(vols[i] == 0)
          continue;

        double d = (min + i * priceStep) - mean;
        double w = (double)vols[i] / total;
        double d2 = d * d;

        m2 += w * d2;
        m3 += w * d2 * d;
        m4 += w * d2 * d2;
      }

      double std = m2 > 0 ? Math.Sqrt(m2) : 0;
      s.StdDev = std;

      s.Skewness = (std > 0) ? m3 / (std * std * std) : 0;
      s.Kurtosis = (m2 > 0) ? m4 / (m2 * m2) : 0;

      // ------------------------------------------------------------
      //  Top-3 смежных тика: максимальная сумма объёма по окну из 3
      //  подряд идущих ценовых уровней (склеенный сгусток).

      long top3Vol = 0;
      int top3From = min;
      int top3To = min;

      if(nLevels >= 3)
      {
        long win = vols[0] + vols[1] + vols[2];
        top3Vol = win;
        top3From = min;
        top3To = min + 2 * priceStep;

        for(int i = 3; i < nLevels; i++)
        {
          win += vols[i] - vols[i - 3];
          if(win > top3Vol)
          {
            top3Vol = win;
            top3From = min + (i - 2) * priceStep;
            top3To = min + i * priceStep;
          }
        }
      }
      else
      {
        for(int i = 0; i < nLevels; i++)
          top3Vol += vols[i];
        top3From = min;
        top3To = max;
      }

      s.Top3From = top3From;
      s.Top3To = top3To;
      s.Top3Share = (double)top3Vol / total;

      // ------------------------------------------------------------
      //  LVN: ищем диапазоны, где средний объём на уровне < 0.25 от
      //  среднего по всему бару, длиной от 2 тиков.

      double avgPerLevel = (double)total / nLevels;
      double lvnThreshold = avgPerLevel * 0.25;

      var lvn = new List<LvnRange>();
      int runStart = -1;
      long runVol = 0;

      for(int i = 0; i < nLevels; i++)
      {
        if(vols[i] < lvnThreshold)
        {
          if(runStart < 0)
          {
            runStart = i;
            runVol = 0;
          }
          runVol += vols[i];
        }
        else
        {
          if(runStart >= 0 && i - runStart >= 2)
            lvn.Add(new LvnRange(
              min + runStart * priceStep,
              min + (i - 1) * priceStep,
              runVol));

          runStart = -1;
        }
      }

      if(runStart >= 0 && nLevels - runStart >= 2)
        lvn.Add(new LvnRange(
          min + runStart * priceStep,
          min + (nLevels - 1) * priceStep,
          runVol));

      s.Lvn = lvn;

      // ------------------------------------------------------------
      //  Форма профиля (эвристика).

      s.Shape = ClassifyShape(s, range);

      return s;
    }

    // **********************************************************************

    static double Clamp01(double value)
    {
      if(value < 0) return 0;
      if(value > 1) return 1;
      return value;
    }

    // **********************************************************************

    /// <summary>
    /// Объём между ComPrice и границей бара (выше COM — above, ниже — !above).
    /// </summary>
    public long VolumeBeyondCom(bool above)
    {
      long v = 0;

      if(above)
      {
        for(int p = ComPrice + PriceStep; p <= MaxPrice; p += PriceStep)
          v += Source.GetCellVolume(p);
      }
      else
      {
        for(int p = MinPrice; p <= ComPrice - PriceStep; p += PriceStep)
          v += Source.GetCellVolume(p);
      }

      return v;
    }

    /// <summary>Доля объёма за ComPrice к соответствующей границе бара.</summary>
    public double ShareBeyondCom(bool above)
    {
      return Volume > 0 ? (double)VolumeBeyondCom(above) / Volume : 0.0;
    }

    static int NearestTickPrice(double price, int step, int min, int max)
    {
      if(step <= 0) return min;
      int idx = (int)Math.Round((price - min) / (double)step);
      int nLevels = (max - min) / step + 1;
      if(idx < 0) idx = 0;
      if(idx >= nLevels) idx = nLevels - 1;
      return min + idx * step;
    }

    // **********************************************************************

    /// <summary>
    /// Возвращает суммарный объём в нижней/верхней четверти бара.
    /// </summary>
    public long VolumeInQuarter(bool bottom)
    {
      if(Range <= 0)
        return 0;

      int quarter = Math.Max(PriceStep, Range / 4);
      int lo, hi;

      if(bottom) { lo = MinPrice; hi = MinPrice + quarter; }
      else       { lo = MaxPrice - quarter; hi = MaxPrice; }

      long v = 0;
      for(int p = lo; p <= hi; p += PriceStep)
        v += Source.GetCellVolume(p);
      return v;
    }

    /// <summary>Доля объёма в нижней/верхней четверти бара.</summary>
    public double ShareInQuarter(bool bottom)
    {
      return Volume > 0 ? (double)VolumeInQuarter(bottom) / Volume : 0.0;
    }

    // **********************************************************************

    static ProfileShape ClassifyShape(ClusterStats s, int range)
    {
      if(range <= 0 || s.Volume == 0)
        return ProfileShape.Unknown;

      // Узкое, островершинное — концентрация на 3 тиках больше 55%.
      if(s.Top3Share >= 0.55)
        return ProfileShape.Thin;

      // Трендовое: центр массы близко к одной из границ (<20% / >80%).
      double posMass = (s.CenterOfMass - s.MinPrice) / range;

      if(posMass <= 0.20 || posMass >= 0.80)
        return ProfileShape.Trending;

      // Масса объёма вверху → negative skew (хвост тянется вниз по цене), бычий профиль.
      // Масса объёма внизу → positive skew (хвост тянется вверх по цене), медвежий профиль.
      if(s.Skewness <= -0.4) return ProfileShape.TopHeavy;
      if(s.Skewness >=  0.4) return ProfileShape.BottomHeavy;

      return ProfileShape.Balanced;
    }

    // **********************************************************************
  }

  // ==========================================================================
}
