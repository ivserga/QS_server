// ==========================================================================
//  ISignalDetector.cs — Контракт детектора паттернов в истории кластеров
// ==========================================================================

namespace QScalp.Client.Clusters.Analytics
{
    internal interface ISignalDetector
    {
        string Name { get; }
        bool Enabled { get; set; }
        Signal Evaluate(ClusterHistory history);
    }

    internal interface ISignalSink
    {
        void Emit(Signal signal);
    }
}
