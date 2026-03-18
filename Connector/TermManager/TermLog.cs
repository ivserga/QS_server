// ======================================================================
//    TermLog.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// ======================================================================

using System;
using System.IO;
using System.Text;

namespace QScalp.Connector
{
  class TermLog
  {
    // **********************************************************************

    IDataReceiver dataReceiver;
    TermManager tmgr;

    StreamWriter sw;

    // **********************************************************************

    public TermLog(TermManager tmgr, IDataReceiver dataReceiver)
    {
      this.tmgr = tmgr;
      this.dataReceiver = dataReceiver;
    }

    // **********************************************************************

    public void Open()
    {
      if(cfg.u.EnableQuikLog)
        try
        {
          sw = new StreamWriter(cfg.LogFile, true, Encoding.UTF8);
          sw.WriteLine();
        }
        catch(Exception e)
        {
          dataReceiver.PutMessage(new Message(
            "Ошибка инициализации файла протокола работы:\n" + e.Message));
        }
      else
        sw = null;
    }

    // **********************************************************************

    public void Close()
    {
      if(sw != null)
        lock(sw)
          try
          {
            sw.Close();
            sw = null;
          }
          catch { }
    }

    // **********************************************************************

    public void Put(string str)
    {
      if(sw != null)
        try
        {
          lock(sw)
            sw.WriteLine(DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss.fff") + " >;"
              + tmgr.QueueLength + ";" + tmgr.Position.ByOrders + ";"
              + tmgr.AskPrice + ";" + tmgr.BidPrice + ";;" + str);
        }
        catch(Exception e)
        {
          dataReceiver.PutMessage(new Message(
            "Ошибка записи файла протокола работы:\n" + e.Message));
        }
    }

    // **********************************************************************
  }
}
