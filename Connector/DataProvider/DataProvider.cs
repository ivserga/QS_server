// =========================================================================
//   DataProvider.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =========================================================================

using System;
using System.Threading.Tasks;
using QScalp.Connector.RestApi;
using QScalp.Connector.WebSocket;

namespace QScalp.Connector
{
  class DataProvider : IDisposable
  {
    // **********************************************************************

    private WebSocketDataProvider _wsProvider;

    private readonly IDataReceiver _receiver;
    private readonly TermManager _tmgr;

    // **********************************************************************

    // Публичные свойства для совместимости с UI
    public bool IsConnected => _wsProvider?.IsConnected ?? false;
    public bool IsError => _wsProvider?.IsError ?? false;

    // **********************************************************************

    public DataProvider(IDataReceiver receiver, TermManager tmgr)
    {
      _receiver = receiver;
      _tmgr = tmgr;
    }

    // **********************************************************************

    public void Connect()
    {
      // Читаем настройки из конфига
      string apiBaseUrl = cfg.u.ApiBaseUrl;
      string wsBaseUrl = cfg.u.WsBaseUrl;
      string apiKey = cfg.u.ApiKey;
      string ticker = cfg.u.SecCode;
      string secKey = cfg.u.SecCode + cfg.u.ClassCode;
      bool debugMode = cfg.u.WsDebugMode;
      bool skipHistoricalData = cfg.u.SkipHistoricalData;

      // Создаем WebSocket провайдер
      _wsProvider = new WebSocketDataProvider(
        apiBaseUrl,
        wsBaseUrl,
        apiKey,
        ticker,
        secKey,
        _receiver,
        _tmgr,
        debugMode,
        skipHistoricalData);

      // Запускаем асинхронно (загрузка snapshot + WebSocket подключение)
      Task.Run(async () =>
      {
        try
        {
          await _wsProvider.StartAsync();
        }
        catch (Exception ex)
        {
          _receiver.PutMessage(new Message($"DataProvider error: {ex.Message}"));
        }
      });
    }

    // **********************************************************************

    public void Disconnect()
    {
      if (_wsProvider != null)
      {
        Task.Run(async () =>
        {
          try
          {
            await _wsProvider.StopAsync();
          }
          catch
          {
            // Игнорируем ошибки при остановке
          }
        }).Wait(TimeSpan.FromSeconds(5));
        
        _wsProvider.Dispose();
        _wsProvider = null;
      }
    }

    // **********************************************************************

    public void Dispose()
    {
      Disconnect();
    }

    // **********************************************************************
  }
}
