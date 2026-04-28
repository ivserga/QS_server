// ==========================================================================
//  WindowMath.cs — Общие формулы по окну ClusterStats (slope, доли, средние)
// ==========================================================================
//
//  Вынесено отдельно, чтобы детекторы (Distribution / Accumulation /
//  Breakout / DoubleTop / DoubleBottom) могли пользоваться одной и той же
//  реализацией линейной регрессии и подсчёта долей по кластерам.
//
//  Все методы работают на готовом окне ClusterStats[] (последние N кластеров,
//  включая текущий или нет — это решает вызывающий) и не делают копий.
//
// ==========================================================================

using System;
using System.Collections.Generic;

namespace QScalp.View.ClustersSpace.Analytics
{
  // ==========================================================================

  /// <summary>
  /// Общие математические утилиты для анализа окна закрытых кластеров.
  /// Все методы — статические, без состояния.
  /// </summary>
  static class WindowMath
  {
    // **********************************************************************

    /// <summary>
    /// Slope линейной регрессии closePrice по индексам бара,
    /// нормированный на средний close. Положительный = цена растёт,
    /// отрицательный = падает. 0.005 ≈ +0.5% от среднего close за бар.
    /// </summary>
    public static double SlopePctClose(ClusterHistory history, int from, int count)
    {
      if(count < 2 || history == null) return 0;

      double[] ys = new double[count];
      for(int i = 0; i < count; i++)
      {
        var s = history.Last(from + count - 1 - i);
        if(s == null) return 0;
        ys[i] = s.ClosePrice;
      }
      return SlopePct(ys);
    }

    // **********************************************************************

    /// <summary>
    /// Slope линейной регрессии Volume по индексам бара, нормированный на
    /// средний объём. Отрицательный = объём угасает (признак distribution
    /// на вершине / exhaustion в тренде).
    /// </summary>
    public static double SlopePctVolume(ClusterHistory history, int from, int count)
    {
      if(count < 2 || history == null) return 0;

      double[] ys = new double[count];
      for(int i = 0; i < count; i++)
      {
        var s = history.Last(from + count - 1 - i);
        if(s == null) return 0;
        ys[i] = s.Volume;
      }
      return SlopePct(ys);
    }

    // **********************************************************************

    /// <summary>
    /// Максимальный high в окне [from..from+count-1] (where 0 — самый последний бар).
    /// Возвращает int.MinValue, если истории не хватает.
    /// </summary>
    public static int HighestHigh(ClusterHistory history, int from, int count)
    {
      int hi = int.MinValue;
      for(int i = 0; i < count; i++)
      {
        var s = history.Last(from + i);
        if(s == null) break;
        if(s.MaxPrice > hi) hi = s.MaxPrice;
      }
      return hi;
    }

    /// <summary>Минимальный low в окне.</summary>
    public static int LowestLow(ClusterHistory history, int from, int count)
    {
      int lo = int.MaxValue;
      for(int i = 0; i < count; i++)
      {
        var s = history.Last(from + i);
        if(s == null) break;
        if(s.MinPrice < lo) lo = s.MinPrice;
      }
      return lo;
    }

    // **********************************************************************

    /// <summary>
    /// Делит окно на две равные половины и возвращает максимумы первой и второй.
    /// firstHalfHigh / secondHalfHigh — вершины. Если истории не хватает — false.
    /// </summary>
    public static bool TwoHalvesHighs(ClusterHistory history, int count,
      out int firstHalfHigh, out int secondHalfHigh)
    {
      firstHalfHigh = secondHalfHigh = 0;
      if(count < 4 || history.Count < count) return false;

      int half = count / 2;

      // first half = более старые бары, second half = более свежие
      int hi1 = int.MinValue, hi2 = int.MinValue;
      for(int i = 0; i < half; i++)
      {
        var s = history.Last(count - 1 - i); // от старого к молодому в первой половине
        if(s != null && s.MaxPrice > hi1) hi1 = s.MaxPrice;
      }
      for(int i = 0; i < count - half; i++)
      {
        var s = history.Last(i);
        if(s != null && s.MaxPrice > hi2) hi2 = s.MaxPrice;
      }
      firstHalfHigh = hi1;
      secondHalfHigh = hi2;
      return true;
    }

    /// <summary>Минимумы первой и второй половин окна.</summary>
    public static bool TwoHalvesLows(ClusterHistory history, int count,
      out int firstHalfLow, out int secondHalfLow)
    {
      firstHalfLow = secondHalfLow = 0;
      if(count < 4 || history.Count < count) return false;

      int half = count / 2;
      int lo1 = int.MaxValue, lo2 = int.MaxValue;
      for(int i = 0; i < half; i++)
      {
        var s = history.Last(count - 1 - i);
        if(s != null && s.MinPrice < lo1) lo1 = s.MinPrice;
      }
      for(int i = 0; i < count - half; i++)
      {
        var s = history.Last(i);
        if(s != null && s.MinPrice < lo2) lo2 = s.MinPrice;
      }
      firstHalfLow = lo1;
      secondHalfLow = lo2;
      return true;
    }

    // **********************************************************************

    /// <summary>
    /// Доля баров окна, у которых PosPoc &lt; threshold.
    /// Используется для оценки «сколько баров с POC у нижней части бара» —
    /// классический признак distribution.
    /// </summary>
    public static double SharePosPocBelow(ClusterHistory history, int count, double threshold)
    {
      int below = 0, total = 0;
      for(int i = 0; i < count; i++)
      {
        var s = history.Last(i);
        if(s == null) break;
        total++;
        if(s.PosPoc < threshold) below++;
      }
      return total > 0 ? (double)below / total : 0;
    }

    /// <summary>Доля баров с PosPoc &gt; threshold (зеркальный признак для accumulation).</summary>
    public static double SharePosPocAbove(ClusterHistory history, int count, double threshold)
    {
      int above = 0, total = 0;
      for(int i = 0; i < count; i++)
      {
        var s = history.Last(i);
        if(s == null) break;
        total++;
        if(s.PosPoc > threshold) above++;
      }
      return total > 0 ? (double)above / total : 0;
    }

    // **********************************************************************

    /// <summary>Среднее skewness по окну (signed). Отрицательное — хвост вниз.</summary>
    public static double AverageSkewness(ClusterHistory history, int count)
    {
      double sum = 0; int n = 0;
      for(int i = 0; i < count; i++)
      {
        var s = history.Last(i);
        if(s == null) break;
        sum += s.Skewness;
        n++;
      }
      return n > 0 ? sum / n : 0;
    }

    // **********************************************************************

    /// <summary>
    /// Максимальный high в окне БЕЗ последних skipFromEnd баров.
    /// Используется для Breakout: «выше ли последний close max'а ПРЕДЫДУЩИХ N»,
    /// поэтому последний бар (или пара) должны исключаться.
    /// </summary>
    public static int HighestHighExcludingLast(ClusterHistory history, int count, int skipFromEnd)
    {
      int hi = int.MinValue;
      for(int i = skipFromEnd; i < skipFromEnd + count; i++)
      {
        var s = history.Last(i);
        if(s == null) break;
        if(s.MaxPrice > hi) hi = s.MaxPrice;
      }
      return hi;
    }

    /// <summary>Зеркало: минимальный low в окне без последних skipFromEnd баров.</summary>
    public static int LowestLowExcludingLast(ClusterHistory history, int count, int skipFromEnd)
    {
      int lo = int.MaxValue;
      for(int i = skipFromEnd; i < skipFromEnd + count; i++)
      {
        var s = history.Last(i);
        if(s == null) break;
        if(s.MinPrice < lo) lo = s.MinPrice;
      }
      return lo;
    }

    // **********************************************************************

    /// <summary>
    /// Локальные вершины (max'ы) в окне count баров. Бар на позиции i
    /// (от конца) считается локальной вершиной, если его MaxPrice строго
    /// больше MaxPrice всех баров на расстоянии 1..leftRight слева И справа.
    /// Возвращает индексы вершин (от конца), отсортированные от старых к новым.
    /// Самые крайние leftRight баров с обеих сторон не рассматриваются.
    /// </summary>
    public static List<int> FindLocalPeakIndices(ClusterHistory history, int count, int leftRight)
    {
      var result = new List<int>();
      if(history == null || count <= 0 || leftRight <= 0) return result;

      int max = Math.Min(count, history.Count) - leftRight;
      // i = от конца истории; идём от leftRight (свежие) до (max-1) (старые)
      for(int i = leftRight; i < max; i++)
      {
        var s = history.Last(i);
        if(s == null) continue;

        bool peak = true;
        for(int k = 1; k <= leftRight; k++)
        {
          var l = history.Last(i - k); // более свежий
          var r = history.Last(i + k); // более старый
          if(l == null || r == null) { peak = false; break; }
          if(s.MaxPrice <= l.MaxPrice || s.MaxPrice <= r.MaxPrice) { peak = false; break; }
        }
        if(peak) result.Add(i);
      }

      // Сортируем от старых (большой i) к новым (малый i) для удобства.
      result.Sort((a, b) => b.CompareTo(a));
      return result;
    }

    /// <summary>Зеркало: локальные минимумы в окне.</summary>
    public static List<int> FindLocalTroughIndices(ClusterHistory history, int count, int leftRight)
    {
      var result = new List<int>();
      if(history == null || count <= 0 || leftRight <= 0) return result;

      int max = Math.Min(count, history.Count) - leftRight;
      for(int i = leftRight; i < max; i++)
      {
        var s = history.Last(i);
        if(s == null) continue;

        bool trough = true;
        for(int k = 1; k <= leftRight; k++)
        {
          var l = history.Last(i - k);
          var r = history.Last(i + k);
          if(l == null || r == null) { trough = false; break; }
          if(s.MinPrice >= l.MinPrice || s.MinPrice >= r.MinPrice) { trough = false; break; }
        }
        if(trough) result.Add(i);
      }

      result.Sort((a, b) => b.CompareTo(a));
      return result;
    }

    // **********************************************************************

    /// <summary>
    /// Минимальный low среди баров на индексах [from..to] (от конца).
    /// from больше = старее. Возвращает int.MaxValue если данных нет.
    /// </summary>
    public static int LowestLowBetween(ClusterHistory history, int fromOlder, int toNewer)
    {
      int lo = int.MaxValue;
      for(int i = toNewer; i <= fromOlder; i++)
      {
        var s = history.Last(i);
        if(s == null) continue;
        if(s.MinPrice < lo) lo = s.MinPrice;
      }
      return lo;
    }

    /// <summary>Максимальный high среди баров на индексах [from..to] (от конца).</summary>
    public static int HighestHighBetween(ClusterHistory history, int fromOlder, int toNewer)
    {
      int hi = int.MinValue;
      for(int i = toNewer; i <= fromOlder; i++)
      {
        var s = history.Last(i);
        if(s == null) continue;
        if(s.MaxPrice > hi) hi = s.MaxPrice;
      }
      return hi;
    }

    // **********************************************************************

    /// <summary>
    /// Сглаженный «центр объёма» одного бара. Усредняет три точки:
    ///   • PocPrice (дискретный максимум — может прыгать на 1 тик при близких
    ///     объёмах двух соседних уровней),
    ///   • CenterOfMass (взвешенный VWAP всего профиля — гладкий, но «тянет»
    ///     к хвостам распределения),
    ///   • середина Top3-окна (точка наибольшей концентрации трёх соседних
    ///     уровней — устойчива даже когда POC «дрожит»).
    /// Возвращает цену в тех же единицах, что и PocPrice.
    /// Используется детекторами вместо чистого PocPrice, когда сравнение
    /// чувствительно к шагу priceStep (PocMigration, DoubleTop, DoubleBottom).
    /// </summary>
    public static double PocCenter(ClusterStats s)
    {
      if(s == null) return 0;
      double top3Mid = (s.Top3From + s.Top3To) * 0.5;
      return (s.PocPrice + s.CenterOfMass + top3Mid) / 3.0;
    }

    // **********************************************************************

    /// <summary>
    /// Slope линейной регрессии по произвольному массиву, нормированный
    /// к среднему. См. AdvisorRequest.SlopePct для оригинала.
    /// </summary>
    static double SlopePct(double[] ys)
    {
      int n = ys.Length;
      if(n < 2) return 0;

      double mean = 0;
      for(int i = 0; i < n; i++) mean += ys[i];
      mean /= n;
      if(mean <= 0) return 0;

      double xMean = (n - 1) / 2.0;
      double num = 0, den = 0;
      for(int i = 0; i < n; i++)
      {
        double dx = i - xMean;
        num += dx * (ys[i] - mean);
        den += dx * dx;
      }
      if(den == 0) return 0;
      double v = (num / den) / mean;
      if(double.IsNaN(v) || double.IsInfinity(v)) return 0;
      return v;
    }

    // **********************************************************************
  }

  // ==========================================================================
}
