// =======================================================================
//    TradeLog.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =======================================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;

namespace QScalp.TradeLogSpace
{
  public class TradeLog
  {
    // **********************************************************************

    Queue<TradeLogRec> recQueue;

    TradeLogRec lastRec;
    DateTime lastOpen, lastClose;

    int cTurnover;

    bool updated;

    // **********************************************************************

    public ObservableCollection<TradeLogRec> Records;

    public int LastResult { get; protected set; }

    public int TradesCount { get; protected set; }
    public int Turnover { get; protected set; }

    public int OverallResult { get; protected set; }
    public int AverageResult { get { return TradesCount == 0 ? 0 : OverallResult / TradesCount; } }

    public int OverallResultPerLot { get { return Turnover == 0 ? 0 : OverallResult * TradesCount * 2 / Turnover; } }
    public int AverageResultPerLot { get { return Turnover == 0 ? 0 : OverallResult * 2 / Turnover; } }

    public bool Updated { get { return updated && !(updated = false); } }

    // **********************************************************************

    public TradeLog()
    {
      recQueue = new Queue<TradeLogRec>();

      lastRec = new TradeLogRec(1);
      lastRec.SetClose(new DateTime(), 0, 0);

      Records = new ObservableCollection<TradeLogRec>();

      updated = true;
    }

    // **********************************************************************

    public void AddOpen(DateTime dateTime, int quantity, int price)
    {
      cTurnover += Math.Abs(quantity);

      if(dateTime.Ticks - lastOpen.Ticks >= cfg.u.SingleTradeTimeout
        * TimeSpan.TicksPerSecond || lastRec.CloseExist)
      {
        lastRec = new TradeLogRec(cfg.u.PriceRatio);
        lastRec.SetOpen(dateTime, quantity, quantity * price);

        lock(recQueue)
          recQueue.Enqueue(lastRec);
      }
      else
        lastRec.AddOpen(quantity, quantity * price);

      lastOpen = dateTime;

      updated = true;
    }

    // **********************************************************************

    public void AddClose(DateTime dateTime, int quantity, int price)
    {
      cTurnover += Math.Abs(quantity);

      if(dateTime.Ticks - lastClose.Ticks >= cfg.u.SingleTradeTimeout
        * TimeSpan.TicksPerSecond && lastRec.CloseExist)
      {
        lastRec = new TradeLogRec(cfg.u.PriceRatio);
        lastRec.SetClose(dateTime, quantity, quantity * price);

        lock(recQueue)
          recQueue.Enqueue(lastRec);
      }
      else if(lastRec.CloseExist)
        lastRec.AddClose(quantity, quantity * price);
      else
        lastRec.SetClose(dateTime, quantity, quantity * price);

      lastClose = dateTime;

      updated = true;
    }

    // **********************************************************************

    public void AddClose(DateTime dateTime, int quantity, int price, int result)
    {
      AddClose(dateTime, quantity, price);
      lastRec.SetResult(result);

      LastResult = result;
      OverallResult += result;

      Turnover += cTurnover;
      cTurnover = 0;

      TradesCount++;
    }

    // **********************************************************************

    public void Commit()
    {
      if(Records.Count > 0)
        Records[Records.Count - 1].NotifyObservers();

      lock(recQueue)
        while(recQueue.Count > 0)
          Records.Add(recQueue.Dequeue());
    }

    // **********************************************************************

    public void Clear()
    {
      if(!lastRec.ResultExist)
        lastRec.SetResult(0);

      lastOpen = new DateTime();
      lastClose = new DateTime();

      LastResult = 0;
      OverallResult = 0;
      TradesCount = 0;
      Turnover = 0;
      cTurnover = 0;

      updated = true;
    }

    // **********************************************************************

    public void Flush(string fileName)
    {
      try
      {
        bool newLogFile = !File.Exists(fileName);

        using(StreamWriter sw = new StreamWriter(fileName, true, Encoding.UTF8))
        {
          if(newLogFile)
          {
            sw.WriteLine(TradeLogRec.CsvHeader1);
            sw.WriteLine(TradeLogRec.CsvHeader2);
          }

          foreach(TradeLogRec rec in Records)
            sw.WriteLine(rec.CsvValue);
        }
      }
      catch(Exception e)
      {
        if(MessageBox.Show("Ошибка при сохранении торгового журнала:\n"
          + e.Message + "\n\nСохранить в другом месте?", cfg.ProgName,
          MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.Yes,
          MessageBoxOptions.ServiceNotification) == MessageBoxResult.Yes)
        {
          System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog();

          sfd.Title = "Сохранение торгового журнала";
          sfd.Filter = "*." + cfg.TradeLogFileExt + "|*." + cfg.TradeLogFileExt;
          sfd.RestoreDirectory = true;
          sfd.OverwritePrompt = false;

          sfd.InitialDirectory = Path.GetDirectoryName(fileName);
          sfd.FileName = Path.GetFileName(fileName);

          if(sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            Flush(sfd.FileName);
        }
      }
    }

    // **********************************************************************
  }
}
