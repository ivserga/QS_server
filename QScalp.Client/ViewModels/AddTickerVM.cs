// ==========================================================================
//  AddTickerVM.cs — Модель диалога "Добавить тикер".
// ==========================================================================

using QScalp.Shared.Models;

namespace QScalp.Client.ViewModels
{
    public sealed class AddTickerVM : ViewModelBase
    {
        string _ticker = "";
        string _secKey = "";
        int _priceStep = 10000;
        ClusterBase _clusterBase = ClusterBase.Time;
        int _clusterSize = 60;
        int _historyCapacity = 64;
        bool _logToCsv = false;
        bool _exportClustersToJson = false;

        public string Ticker { get => _ticker; set => Set(ref _ticker, value); }
        public string SecKey { get => _secKey; set => Set(ref _secKey, value); }
        public int PriceStep { get => _priceStep; set => Set(ref _priceStep, value); }
        public ClusterBase ClusterBase { get => _clusterBase; set => Set(ref _clusterBase, value); }
        public int ClusterSize { get => _clusterSize; set => Set(ref _clusterSize, value); }
        public int HistoryCapacity { get => _historyCapacity; set => Set(ref _historyCapacity, value); }
        public bool LogToCsv { get => _logToCsv; set => Set(ref _logToCsv, value); }
        public bool ExportClustersToJson { get => _exportClustersToJson; set => Set(ref _exportClustersToJson, value); }

        public ClusterBase[] AvailableBases { get; } =
        {
            ClusterBase.Time, ClusterBase.Volume, ClusterBase.Range,
            ClusterBase.Ticks, ClusterBase.Delta
        };
    }
}
