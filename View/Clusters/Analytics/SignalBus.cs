// ==========================================================================
//  SignalBus.cs — Оркестратор детекторов и приёмников сигналов
// ==========================================================================
//
//  SignalBus:
//    • хранит ClusterHistory (кэш закрытых кластеров);
//    • держит список ISignalDetector (абсорбция, климакс, V-разворот, ...);
//    • держит список ISignalSink (Messenger, CSV-лог, ...);
//    • при добавлении нового закрытого кластера прогоняет все включённые
//      детекторы и рассылает результаты во все приёмники.
//
// ==========================================================================

using System.Collections.Generic;

namespace QScalp.View.ClustersSpace.Analytics
{
  // ==========================================================================

  sealed class SignalBus
  {
    // **********************************************************************

    public ClusterHistory History { get; private set; }

    readonly List<ISignalDetector> detectors;
    readonly List<ISignalSink> sinks;

    // **********************************************************************

    public SignalBus(int historyCapacity)
    {
      History = new ClusterHistory(historyCapacity);
      detectors = new List<ISignalDetector>();
      sinks = new List<ISignalSink>();
    }

    // **********************************************************************

    public SignalBus AddDetector(ISignalDetector detector)
    {
      if(detector != null)
        detectors.Add(detector);
      return this;
    }

    public SignalBus AddSink(ISignalSink sink)
    {
      if(sink != null)
        sinks.Add(sink);
      return this;
    }

    // **********************************************************************

    /// <summary>
    /// Регистрирует только что закрытый кластер и прогоняет детекторы.
    /// Обычно вызывается из ClustersElement в момент закрытия очередного
    /// минутного (time-based) кластера.
    /// </summary>
    public void OnClusterClosed(Cluster cluster, int priceStep)
    {
      var stats = ClusterStats.Compute(cluster, priceStep);
      if(stats == null)
        return;

      History.Add(stats);

      for(int i = 0; i < detectors.Count; i++)
      {
        var d = detectors[i];
        if(!d.Enabled)
          continue;

        Signal s = d.Evaluate(History);
        if(s == null || s.Kind == SignalKind.None)
          continue;

        if(string.IsNullOrEmpty(s.Source))
          s.Source = d.Name;

        for(int j = 0; j < sinks.Count; j++)
          sinks[j].Emit(s);
      }
    }

    // **********************************************************************

    /// <summary>Очистка истории при сбросе графика.</summary>
    public void Reset(int historyCapacity)
    {
      History = new ClusterHistory(historyCapacity);
    }

    // **********************************************************************
  }

  // ==========================================================================
}
