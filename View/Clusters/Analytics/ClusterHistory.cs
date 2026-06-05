// ==========================================================================
//  ClusterHistory.cs — Кэш последних закрытых кластеров и их метрик
// ==========================================================================
//
//  ClustersElement хранит ограниченное количество кластеров (cfg.u.Clusters
//  или "без ограничения"). Для детекторов удобно иметь собственный, более
//  длинный ring-buffer пар (Cluster, ClusterStats), чтобы вычислять средние
//  метрики по окну и не терять контекст при скроллинге отображения.
//
// ==========================================================================

using System;
using System.Collections.Generic;

namespace QScalp.View.ClustersSpace.Analytics
{
  // ==========================================================================

  sealed class ClusterHistory
  {
    // **********************************************************************

    readonly int capacity;
    readonly LinkedList<ClusterStats> items;

    // **********************************************************************

    public int Count { get { return items.Count; } }
    public int Capacity { get { return capacity; } }

    // **********************************************************************

    public ClusterHistory(int capacity)
    {
      this.capacity = capacity > 0 ? capacity : 64;
      this.items = new LinkedList<ClusterStats>();
    }

    // **********************************************************************

    public void Add(ClusterStats stats)
    {
      if(stats == null)
        return;

      items.AddLast(stats);

      while(items.Count > capacity)
        items.RemoveFirst();
    }

    // **********************************************************************

    /// <summary>
    /// Возвращает кластер с конца истории: Last(0) — самый последний,
    /// Last(1) — предпоследний и т. д. null, если глубины не хватает.
    /// </summary>
    public ClusterStats Last(int indexFromEnd)
    {
      if(indexFromEnd < 0 || indexFromEnd >= items.Count)
        return null;

      var node = items.Last;
      for(int i = 0; i < indexFromEnd; i++)
        node = node.Previous;

      return node.Value;
    }

    // **********************************************************************

    /// <summary>
    /// Средний объём по последним <paramref name="n"/> кластерам, исключая
    /// последний (текущий). Если истории недостаточно — возвращает 0.
    /// </summary>
    public double AverageVolumeBefore(int n)
    {
      if(items.Count < n + 1)
        return 0;

      double sum = 0;
      int cnt = 0;

      var node = items.Last.Previous;
      while(node != null && cnt < n)
      {
        sum += node.Value.Volume;
        node = node.Previous;
        cnt++;
      }

      return cnt > 0 ? sum / cnt : 0;
    }

    /// <summary>
    /// Средний объём по <paramref name="n"/> кластерам, расположенным
    /// перед баром с заданным смещением от конца.
    /// Пример: offset=1 — средний объём ДО предыдущего бара (trap).
    /// Если данных недостаточно — 0.
    /// </summary>
    public double AverageVolumeBeforeFrom(int offsetFromEnd, int n)
    {
      if(offsetFromEnd < 0 || n <= 0)
        return 0;

      if(items.Count < offsetFromEnd + n + 1)
        return 0;

      double sum = 0;
      int cnt = 0;

      var node = items.Last;
      for(int i = 0; i < offsetFromEnd && node != null; i++)
        node = node.Previous;

      if(node == null)
        return 0;

      node = node.Previous;
      while(node != null && cnt < n)
      {
        sum += node.Value.Volume;
        node = node.Previous;
        cnt++;
      }

      return cnt > 0 ? sum / cnt : 0;
    }

    // **********************************************************************

    /// <summary>
    /// Средний range (Max-Min) по последним <paramref name="n"/> кластерам,
    /// исключая последний. Если истории недостаточно — 0.
    /// </summary>
    public double AverageRangeBefore(int n)
    {
      if(items.Count < n + 1)
        return 0;

      double sum = 0;
      int cnt = 0;

      var node = items.Last.Previous;
      while(node != null && cnt < n)
      {
        sum += node.Value.Range;
        node = node.Previous;
        cnt++;
      }

      return cnt > 0 ? sum / cnt : 0;
    }

    // **********************************************************************

    /// <summary>
    /// Количество подряд идущих медвежьих (close &lt; open) кластеров
    /// в истории, начиная с указанного смещения от конца.
    /// </summary>
    public int CountBearishRunFrom(int indexFromEnd)
    {
      int n = 0;
      for(int i = indexFromEnd; i < items.Count; i++)
      {
        var s = Last(i);
        if(s == null)
          break;

        if(s.ClosePrice < s.OpenPrice)
          n++;
        else
          break;
      }
      return n;
    }

    /// <summary>
    /// Количество подряд идущих бычьих (close &gt; open) кластеров от
    /// указанного смещения от конца.
    /// </summary>
    public int CountBullishRunFrom(int indexFromEnd)
    {
      int n = 0;
      for(int i = indexFromEnd; i < items.Count; i++)
      {
        var s = Last(i);
        if(s == null)
          break;

        if(s.ClosePrice > s.OpenPrice)
          n++;
        else
          break;
      }
      return n;
    }

    // **********************************************************************

    /// <summary>
    /// Время самого свежего кластера в истории (для вывода в сигнал).
    /// </summary>
    public DateTime LastTime
    {
      get
      {
        var s = Last(0);
        return s != null ? s.Source.DateTime : DateTime.MinValue;
      }
    }

    // **********************************************************************
  }

  // ==========================================================================
}
