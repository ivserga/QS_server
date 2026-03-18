// ==============================================================================
//  TradeLogWindow.xaml.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ==============================================================================

using System.Windows;
using QScalp.TradeLogSpace;

namespace QScalp.Windows
{
  public partial class TradeLogWindow : Window
  {
    // **********************************************************************

    TradeLog log;

    // **********************************************************************

    public TradeLogWindow(TradeLog log)
    {
      InitializeComponent();
      this.log = log;
      trades.ItemsSource = log.Records;
    }

    // **********************************************************************

    public void Update()
    {
      log.Commit();

      if(trades.Items.Count > 0)
        trades.ScrollIntoView(trades.Items[trades.Items.Count - 1]);

      tradesCount.Text = log.TradesCount.ToString("N", cfg.BaseCulture);
      turnover.Text = log.Turnover.ToString("N", cfg.BaseCulture);

      overallPerLot.Text = Price.GetString(log.OverallResultPerLot);
      averagePerLot.Text = Price.GetString(log.AverageResultPerLot);

      overall.Text = Price.GetString(log.OverallResult);
      average.Text = Price.GetString(log.AverageResult);
    }

    // **********************************************************************
  }
}
