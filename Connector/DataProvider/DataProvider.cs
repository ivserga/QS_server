// =========================================================================
//   DataProvider.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =========================================================================

using System;
using System.Threading.Tasks;
using QScalp.Connector.RestApi;
using QScalp.Connector.WebSocket;
using QScalp.Connector.ServerConnection;

namespace QScalp.Connector
{
  class DataProvider : IDisposable
  {
    // **********************************************************************

    private WebSocketDataProvider _wsProvider;
    private ServerDataProvider _serverProvider;

    private readonly IDataReceiver _receiver;
    private readonly TermManager _tmgr;

    // **********************************************************************

    public bool IsConnected =>
      cfg.u.UseServer
        ? (_serverProvider?.IsConnected ?? false)
        : (_wsProvider?.IsConnected ?? false);

    public bool IsError =>
      cfg.u.UseServer
        ? (_serverProvider?.IsError ?? false)
        : (_wsProvider?.IsError ?? false);

    // **********************************************************************

    public DataProvider(IDataReceiver receiver, TermManager tmgr)
    {
      _receiver = receiver;
      _tmgr = tmgr;
    }

    // **********************************************************************

    public void Connect()
    {
      if (cfg.u.UseServer)
      {
        ConnectViaServer();
      }
      else
      {
        ConnectDirect();
      }
    }

    // **********************************************************************

    private void ConnectViaServer()
    {
      _serverProvider = new ServerDataProvider(_receiver, _tmgr);
      _serverProvider.Connect();
    }

    // **********************************************************************

    private void ConnectDirect()
    {
      string apiBaseUrl = cfg.u.ApiBaseUrl;
      string wsBaseUrl = cfg.u.WsBaseUrl;
      string apiKey = cfg.u.ApiKey;
      string ticker = cfg.u.SecCode;
      string secKey = cfg.u.SecCode + cfg.u.ClassCode;
      bool debugMode = cfg.u.WsDebugMode;
      bool skipHistoricalData = cfg.u.SkipHistoricalData;
      int filterTicks = cfg.u.TradeFilterTicks;

      _wsProvider = new WebSocketDataProvider(
        apiBaseUrl,
        wsBaseUrl,
        apiKey,
        ticker,
        secKey,
        _receiver,
        _tmgr,
        debugMode,
        skipHistoricalData,
        filterTicks);

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
      if (_serverProvider != null)
      {
        _serverProvider.Dispose();
        _serverProvider = null;
      }

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
