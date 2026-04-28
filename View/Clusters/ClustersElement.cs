// ==========================================================================
//  ClustersElement.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ==========================================================================

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using QScalp.View.ClustersSpace;
using QScalp.View.ClustersSpace.Analytics;
using QScalp.View.ClustersSpace.Analytics.Detectors;
using QScalp.View.ClustersSpace.Analytics.Sinks;

namespace QScalp.View
{
  sealed class ClustersElement : FrameworkElement, IVObsoletable, IVScrollable, ITradesHandler
  {
    // **********************************************************************

    ViewManager vmgr;

    HGridLines hGrid;

    Cluster cCluster;
    Legend cLegend;

    int nClusters;
    int displayClusters;

    // Детекторы паттернов распределения объёма (absorption / climax / V-reversal)
    SignalBus signalBus;
    AbsorptionDetector absorptionDet;
    ClimaxDetector climaxDet;
    BalanceAfterClimaxDetector balanceDet;
    VReversalDetector vReversalDet;
    PocMigrationDetector pocMigrationDet;
    HvnRejectDetector hvnRejectDet;
    DistributionDetector distributionDet;
    AccumulationDetector accumulationDet;
    BreakoutDetector breakoutDet;
    DoubleTopDetector doubleTopDet;
    DoubleBottomDetector doubleBottomDet;
    OrphanCloseDetector orphanCloseDet;

    // Горизонтальный скроллинг
    double hScrollOffset;
    double maxHScrollOffset;

    ContainerVisual clusters, legends;

    // **********************************************************************

    VisualCollection children;

    protected override int VisualChildrenCount { get { return children.Count; } }
    protected override Visual GetVisualChild(int index) { return children[index]; }

    // **********************************************************************

    public bool Obsolete { get; private set; }

    /// <summary>
    /// Фактическое количество кластеров (для режима "без ограничения")
    /// </summary>
    public int ClusterCount { get { return clusters.Children.Count; } }

    /// <summary>
    /// Количество отображаемых кластеров (ширина области)
    /// </summary>
    public int DisplayClusterCount { get { return displayClusters; } }

    // **********************************************************************

    public ClustersElement(ViewManager vmgr)
    {
      this.vmgr = vmgr;

      ClipToBounds = true;

      children = new VisualCollection(this);
      children.Add(hGrid = new HGridLines(vmgr, true));
      children.Add(clusters = new ContainerVisual());
      children.Add(legends = new ContainerVisual());

      BuildSignalBus();

      Rebuild();
    }

    // **********************************************************************

    /// <summary>
    /// Поднимает конвейер анализа распределения объёма: историю кластеров,
    /// набор детекторов и приёмники сигналов (Messenger + CSV-лог).
    /// Параметры детекторов берутся из cfg.u.*, так что их можно менять без
    /// перекомпиляции.
    /// </summary>
    void BuildSignalBus()
    {
      signalBus = new SignalBus(cfg.u.SignalsHistoryCapacity);

      absorptionDet = new AbsorptionDetector
      {
        Enabled          = cfg.u.AbsorptionEnabled,
        MinPocShare      = cfg.u.AbsorptionMinPocShare,
        EdgeThreshold    = cfg.u.AbsorptionEdgeThreshold,
        TailMaxShare     = cfg.u.AbsorptionTailMaxShare,
        VolumeMultiplier = cfg.u.AbsorptionVolumeMultiplier,
        AverageWindow    = cfg.u.AbsorptionAverageWindow
      };

      climaxDet = new ClimaxDetector
      {
        Enabled              = cfg.u.ClimaxEnabled,
        MinTop3Share         = cfg.u.ClimaxMinTop3Share,
        VolumeMultiplier     = cfg.u.ClimaxVolumeMultiplier,
        MinDensityMultiplier = cfg.u.ClimaxMinDensityMultiplier,
        EdgePosPocTop        = cfg.u.ClimaxEdgePosPocTop,
        EdgePosPocBottom     = cfg.u.ClimaxEdgePosPocBottom,
        AverageWindow        = cfg.u.ClimaxAverageWindow,
        CooldownBars         = cfg.u.ClimaxCooldownBars
      };

      balanceDet = new BalanceAfterClimaxDetector
      {
        Enabled            = cfg.u.BalanceEnabled,
        BalanceVolumeShare = cfg.u.BalanceVolumeShare,
        BalanceRangeShare  = cfg.u.BalanceRangeShare,
        MaxDeltaShare      = cfg.u.BalanceMaxDeltaShare,
        ClimaxMinTop3Share = cfg.u.BalanceClimaxMinTop3Share,
        ClimaxDensityMult  = cfg.u.BalanceClimaxDensityMult,
        AverageWindow      = cfg.u.BalanceAverageWindow
      };

      vReversalDet = new VReversalDetector
      {
        Enabled                  = cfg.u.VReversalEnabled,
        MinRun                   = cfg.u.VReversalMinRun,
        AbsorptionEdge           = cfg.u.VReversalAbsorptionEdge,
        AbsorptionPocShare       = cfg.u.VReversalAbsorptionPocShare,
        MinBodyRatio             = cfg.u.VReversalMinBodyRatio,
        MinCenterOfMassShift     = cfg.u.VReversalMinCenterOfMassShift
      };

      pocMigrationDet = new PocMigrationDetector
      {
        Enabled                  = cfg.u.PocMigrationEnabled,
        MinShiftTicks            = cfg.u.PocMigrationMinShiftTicks,
        VolumeMinRatio           = cfg.u.PocMigrationVolumeMinRatio,
        AverageWindow            = cfg.u.PocMigrationAverageWindow,
        CooldownBars             = cfg.u.PocMigrationCooldownBars,
        SmoothConfirmRatio       = cfg.u.PocMigrationSmoothConfirmRatio,
        MinDirectionalConfirm    = cfg.u.PocMigrationMinDirectionalConfirm,
        CloseRelativePocTolTicks = cfg.u.PocMigrationCloseRelativePocTolTicks
      };

      hvnRejectDet = new HvnRejectDetector
      {
        Enabled          = cfg.u.HvnRejectEnabled,
        LookbackBars     = cfg.u.HvnRejectLookbackBars,
        MinHvnShare      = cfg.u.HvnRejectMinHvnShare,
        MinReclaimTicks  = cfg.u.HvnRejectMinReclaimTicks,
        MaxTailShare     = cfg.u.HvnRejectMaxTailShare,
        CooldownBars     = cfg.u.HvnRejectCooldownBars
      };

      distributionDet = new DistributionDetector
      {
        Enabled              = cfg.u.DistributionEnabled,
        Lookback             = cfg.u.DistributionLookback,
        MinSwingDiffTicks    = cfg.u.DistributionMinSwingDiffTicks,
        MaxVolumeSlope       = cfg.u.DistributionMaxVolumeSlope,
        MinHighAgeBars       = cfg.u.DistributionMinHighAgeBars,
        UseClusterUplift     = cfg.u.DistributionUseClusterUplift,
        PosPocLowThreshold   = cfg.u.DistributionPosPocLowThreshold,
        MinShareLowPosPoc    = cfg.u.DistributionMinShareLowPosPoc,
        MinStrength          = cfg.u.DistributionMinStrength,
        CooldownBars         = cfg.u.DistributionCooldownBars
      };

      accumulationDet = new AccumulationDetector
      {
        Enabled              = cfg.u.AccumulationEnabled,
        Lookback             = cfg.u.AccumulationLookback,
        MinSwingDiffTicks    = cfg.u.AccumulationMinSwingDiffTicks,
        MinVolumeSlope       = cfg.u.AccumulationMinVolumeSlope,
        MinLowAgeBars        = cfg.u.AccumulationMinLowAgeBars,
        UseClusterUplift     = cfg.u.AccumulationUseClusterUplift,
        PosPocHighThreshold  = cfg.u.AccumulationPosPocHighThreshold,
        MinShareHighPosPoc   = cfg.u.AccumulationMinShareHighPosPoc,
        MinStrength          = cfg.u.AccumulationMinStrength,
        CooldownBars         = cfg.u.AccumulationCooldownBars
      };

      breakoutDet = new BreakoutDetector
      {
        Enabled            = cfg.u.BreakoutEnabled,
        LookbackBars       = cfg.u.BreakoutLookbackBars,
        MinBreakoutTicks   = cfg.u.BreakoutMinBreakoutTicks,
        MinBodyRatio       = cfg.u.BreakoutMinBodyRatio,
        MinVolumeRatio     = cfg.u.BreakoutMinVolumeRatio,
        UseClusterUplift   = cfg.u.BreakoutUseClusterUplift,
        PosPocFavorable    = cfg.u.BreakoutPosPocFavorable,
        MaxTop3Share       = cfg.u.BreakoutMaxTop3Share,
        MinStrength        = cfg.u.BreakoutMinStrength,
        CooldownBars       = cfg.u.BreakoutCooldownBars
      };

      doubleTopDet = new DoubleTopDetector
      {
        Enabled              = cfg.u.DoubleTopEnabled,
        Lookback             = cfg.u.DoubleTopLookback,
        PeakLeftRight        = cfg.u.DoubleTopPeakLeftRight,
        MaxPeakDiffTicks     = cfg.u.DoubleTopMaxPeakDiffTicks,
        MinBarsBetweenPeaks  = cfg.u.DoubleTopMinBarsBetweenPeaks,
        MinCorrectionTicks   = cfg.u.DoubleTopMinCorrectionTicks,
        MinSecondPeakAgeBars = cfg.u.DoubleTopMinSecondPeakAgeBars,
        MaxSecondPeakAgeBars = cfg.u.DoubleTopMaxSecondPeakAgeBars,
        MinPostPeakDropTicks = cfg.u.DoubleTopMinPostPeakDropTicks,
        UseClusterUplift     = cfg.u.DoubleTopUseClusterUplift,
        MaxVolumeRatio       = cfg.u.DoubleTopMaxVolumeRatio,
        PocCenterTolTicks    = cfg.u.DoubleTopPocCenterTolTicks,
        MinStrength          = cfg.u.DoubleTopMinStrength,
        CooldownBars         = cfg.u.DoubleTopCooldownBars
      };

      doubleBottomDet = new DoubleBottomDetector
      {
        Enabled                = cfg.u.DoubleBottomEnabled,
        Lookback               = cfg.u.DoubleBottomLookback,
        TroughLeftRight        = cfg.u.DoubleBottomTroughLeftRight,
        MaxTroughDiffTicks     = cfg.u.DoubleBottomMaxTroughDiffTicks,
        MinBarsBetweenTroughs  = cfg.u.DoubleBottomMinBarsBetweenTroughs,
        MinReboundTicks        = cfg.u.DoubleBottomMinReboundTicks,
        MinSecondTroughAgeBars = cfg.u.DoubleBottomMinSecondTroughAgeBars,
        MaxSecondTroughAgeBars = cfg.u.DoubleBottomMaxSecondTroughAgeBars,
        MinPostTroughRiseTicks = cfg.u.DoubleBottomMinPostTroughRiseTicks,
        UseClusterUplift       = cfg.u.DoubleBottomUseClusterUplift,
        MaxVolumeRatio         = cfg.u.DoubleBottomMaxVolumeRatio,
        PocCenterTolTicks      = cfg.u.DoubleBottomPocCenterTolTicks,
        MinStrength            = cfg.u.DoubleBottomMinStrength,
        CooldownBars           = cfg.u.DoubleBottomCooldownBars
      };

      orphanCloseDet = new OrphanCloseDetector
      {
        Enabled        = cfg.u.OrphanCloseEnabled,
        MinRangeTicks  = cfg.u.OrphanCloseMinRangeTicks,
        MinGapShare    = cfg.u.OrphanCloseMinGapShare,
        AverageWindow  = cfg.u.OrphanCloseAverageWindow,
        MinVolumeRatio = cfg.u.OrphanCloseMinVolumeRatio,
        MinStrength    = cfg.u.OrphanCloseMinStrength,
        CooldownBars   = cfg.u.OrphanCloseCooldownBars
      };

      signalBus
        .AddDetector(absorptionDet)
        .AddDetector(climaxDet)
        .AddDetector(balanceDet)
        .AddDetector(vReversalDet)
        .AddDetector(pocMigrationDet)
        .AddDetector(hvnRejectDet)
        .AddDetector(distributionDet)
        .AddDetector(accumulationDet)
        .AddDetector(breakoutDet)
        .AddDetector(doubleTopDet)
        .AddDetector(doubleBottomDet)
        .AddSink(new MessengerSink(vmgr));

      if(cfg.u.SignalsLogToCsv)
        signalBus.AddSink(new SignalLogSink(cfg.SignalsLogFile));
    }

    // **********************************************************************

    public void UpdateWidth()
    {
      if(cfg.u.ClusterWidth < CCell.MinWidth)
        cfg.u.ClusterWidth = CCell.MinWidth;

      // При nClusters <= 0 (режим "без ограничения") — ширина области = 2/3 окна
      if(nClusters <= 0 && vmgr.Width > 0)
      {
        double targetWidth = vmgr.Width * 2.0 / 3.0;
        displayClusters = Math.Max(1, (int)Math.Floor((targetWidth - cfg.s.ClusterHPadding) / cfg.u.ClusterWidth));
      }

      int widthClusters = displayClusters > 0 ? displayClusters : 1;

      Width = cfg.u.ClusterWidth * widthClusters + cfg.s.ClusterHPadding;
    }

    // **********************************************************************

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
      if(sizeInfo.WidthChanged)
        RebuildClusters();

      hGrid.SetWidth(ActualWidth);
      RebuildLegends();

      base.OnRenderSizeChanged(sizeInfo);
    }

    // **********************************************************************

    void RebuildClusters()
    {
      int i = displayClusters - clusters.Children.Count;

      foreach(Cluster c in clusters.Children)
      {
        c.Offset = new Vector(cfg.u.ClusterWidth * i++, 0);
        c.Rebuild();
      }
    }

    // **********************************************************************

    void RebuildLegends()
    {
      int i = displayClusters - legends.Children.Count;

      foreach(Legend l in legends.Children)
      {
        l.Offset = new Vector(cfg.u.ClusterWidth * i++, 0);
        l.Rebuild();
      }
    }

    // **********************************************************************

    public void Rebuild()
    {
      vmgr.TradesQueue.UnregisterHandler(this);
      vmgr.UnregisterObject(this);

      nClusters = cfg.u.Clusters;
      if(nClusters > 0)
        displayClusters = nClusters;
      // При nClusters <= 0 displayClusters вычисляется в UpdateWidth() на основе 2/3 ширины окна

      // Удаляем лишние кластеры только если задано ограничение (nClusters > 0)
      if(nClusters > 0 && clusters.Children.Count > nClusters)
      {
        int removeCount = clusters.Children.Count - nClusters;

        clusters.Children.RemoveRange(0, removeCount);
        legends.Children.RemoveRange(0, removeCount);
      }

      UpdateWidth();

      // Регистрируем обработчики всегда (и при nClusters <= 0 тоже — режим "без ограничения")
      vmgr.RegisterObject(this);
      vmgr.TradesQueue.RegisterHandler(this, cfg.u.SecCode + cfg.u.ClassCode);

      hGrid.Rebuild();
      RebuildClusters();
      RebuildLegends();

      UpdateOffset();

      if(clusters.Children.Count == 0)
      {
        cCluster = new Cluster(vmgr, DateTime.MaxValue);
        cLegend = new Legend(vmgr, cCluster);
      }
    }

    // **********************************************************************

    /// <summary>
    /// Полностью очищает все кластеры (для перезагрузки данных)
    /// </summary>
    public void Clear()
    {
      clusters.Children.Clear();
      legends.Children.Clear();

      cCluster = new Cluster(vmgr, DateTime.MaxValue);
      cLegend = new Legend(vmgr, cCluster);

      // Сбрасываем историю сигналов вместе с графиком.
      if(signalBus != null)
        signalBus.Reset(cfg.u.SignalsHistoryCapacity);

      // Сбрасываем горизонтальный скроллинг
      hScrollOffset = 0;
      UpdateOffset();

      Obsolete = false;
    }

    // **********************************************************************

    void CreateCluster(DateTime dateTime)
    {
      // nClusters <= 0 означает "без ограничения" — не удаляем старые кластеры
      if(nClusters > 0)
      {
        int m = nClusters - 1;

        if(clusters.Children.Count > m)
        {
          int removeCount = clusters.Children.Count - m;

          clusters.Children.RemoveRange(0, removeCount);
          legends.Children.RemoveRange(0, removeCount);
        }
      }

      Vector offset = new Vector(cfg.u.ClusterWidth, 0);

      if(clusters.Children.Count > 0)
      {
        if(Obsolete)
        {
          cCluster.Redraw();
          cLegend.Redraw();
        }

        for(int i = 0; i < clusters.Children.Count; i++)
        {
          ((Cluster)clusters.Children[i]).Offset -= offset;
          ((Legend)legends.Children[i]).Offset -= offset;
        }
      }

      int positionIndex = displayClusters - 1;
      offset.X *= positionIndex;

      cCluster = new Cluster(vmgr, dateTime);
      cCluster.Offset = offset;
      clusters.Children.Add(cCluster);

      cLegend = new Legend(vmgr, cCluster);
      cLegend.Offset = offset;
      legends.Children.Add(cLegend);

      UpdateWidth();

      AnalyzeLastClusters();
    }

    // **********************************************************************

    void AnalyzeLastClusters()
    {
      int count = clusters.Children.Count;

      // Только что закрылся кластер, идущий ПЕРЕД только что созданным пустым.
      // Он лежит по индексу count - 2. Передаём его в детекторы распределения
      // объёма (absorption / climax / balance / V-reversal).
      //
      // Обёрнуто в try/catch: аналитика ни при каких обстоятельствах не должна
      // ронять основной конвейер отрисовки. Любая ошибка тут приведёт к потере
      // входного трейда в PutTrade и визуальным "разрывам" в кластерах.
      if(count >= 2 && cfg.u.SignalsEnabled && signalBus != null)
      {
        var closed = (Cluster)clusters.Children[count - 2];
        try
        {
          signalBus.OnClusterClosed(closed, cfg.u.PriceStep);
        }
        catch(Exception ex)
        {
          vmgr.MsgQueue.Enqueue(new Message("Аналитика сигналов отключена из-за ошибки: " + ex.Message));
          // Сбрасываем конвейер целиком, чтобы одна ошибка не повторялась каждый кластер.
          signalBus = null;
        }
      }

      if(count < 4)
        return;

      Cluster c1 = (Cluster)clusters.Children[count - 4];
      Cluster c2 = (Cluster)clusters.Children[count - 3];
      Cluster c3 = (Cluster)clusters.Children[count - 2];

      ClusterAnalyzer.Signal signal = ClusterAnalyzer.Analyze(c1, c2, c3);
      if(signal != ClusterAnalyzer.Signal.None)
      {
        vmgr.MsgQueue.Enqueue(new Message(ClusterAnalyzer.FormatMessage(signal, c3)));
        SoundAlert.PlayAbsorption();
      }

      ClusterAnalyzer.ClimaxSignal climax = ClusterAnalyzer.AnalyzeClimax(c1, c2, c3);
      if(climax != ClusterAnalyzer.ClimaxSignal.None)
      {
        vmgr.MsgQueue.Enqueue(new Message(ClusterAnalyzer.FormatClimaxMessage(climax, c1, c2, c3)));
        SoundAlert.PlayClimax();
      }

      ClusterAnalyzer.RejectionSignal rejection = ClusterAnalyzer.AnalyzeRejection(c1, c2, c3);
      if(rejection != ClusterAnalyzer.RejectionSignal.None)
      {
        vmgr.MsgQueue.Enqueue(new Message(ClusterAnalyzer.FormatRejectionMessage(rejection, c1, c2, c3)));
        SoundAlert.PlayRejection();
      }
    }

    // **********************************************************************

    void CreateCluster(int csize, ref Trade trade)
    {
      int space = cfg.u.ClusterSize - csize;

      if(space > 0)
      {
        int q = trade.Quantity;
        trade.Quantity = space;

        cCluster.Add(trade);
        Obsolete = true;

        trade.Quantity = q - space;
      }

      CreateCluster(trade.DateTime);
    }

    // **********************************************************************

    public void PutTrade(Trade trade, int count)
    {
      if(trade.DateTime < cCluster.DateTime)
      {
        // nClusters <= 0 означает "без ограничения" — создаём кластеры без лимита
        if(cfg.u.ClusterBase == ClusterBase.Time)
        {
          long csTicks = cfg.u.ClusterSize * TimeSpan.TicksPerSecond;
          CreateCluster(new DateTime(trade.DateTime.Ticks / csTicks * csTicks));
        }
        else
          CreateCluster(trade.DateTime);
      }

      switch(cfg.u.ClusterBase)
      {
        // ----------------------------------------------------------

        case ClusterBase.Time:
          long csTicks = cfg.u.ClusterSize * TimeSpan.TicksPerSecond;
          if(trade.DateTime.Ticks - cCluster.DateTime.Ticks >= csTicks)
            CreateCluster(new DateTime(trade.DateTime.Ticks / csTicks * csTicks));
          break;

        // ----------------------------------------------------------

        case ClusterBase.Volume:
          while(cCluster.Volume + trade.Quantity > cfg.u.ClusterSize)
            CreateCluster(cCluster.Volume, ref trade);
          break;

        // ----------------------------------------------------------

        case ClusterBase.Range:
          if(cCluster.MaxPrice - trade.IntPrice > cfg.u.ClusterSize
            || trade.IntPrice - cCluster.MinPrice > cfg.u.ClusterSize)
            CreateCluster(trade.DateTime);
          break;

        // ----------------------------------------------------------

        case ClusterBase.Ticks:
          if(cCluster.Ticks >= cfg.u.ClusterSize)
            CreateCluster(trade.DateTime);
          break;

        // ----------------------------------------------------------

        case ClusterBase.Delta:
          if(trade.Op == TradeOp.Sell)
            while(trade.Quantity - cCluster.Delta > cfg.u.ClusterSize)
              CreateCluster(-cCluster.Delta, ref trade);
          else
            while(cCluster.Delta + trade.Quantity > cfg.u.ClusterSize)
              CreateCluster(cCluster.Delta, ref trade);
          break;

        // ----------------------------------------------------------
      }

      cCluster.Add(trade);
      Obsolete = true;
    }

    // **********************************************************************

    public void Refresh()
    {
      Obsolete = false;

      cCluster.Redraw();
      cLegend.Redraw();
    }

    // **********************************************************************

    public void UpdateOffset() 
    { 
      clusters.Offset = new Vector(hScrollOffset, vmgr.BaseY);
      legends.Offset = new Vector(hScrollOffset, 0);
    }

    // **********************************************************************

    /// <summary>
    /// Горизонтальный скроллинг кластеров
    /// </summary>
    public void HorizontalScroll(double delta)
    {
      if(nClusters > 0 || clusters.Children.Count <= displayClusters)
        return; // Скроллинг только в режиме "без ограничения" и когда есть скрытые кластеры

      // Вычисляем максимальное смещение (сколько кластеров скрыто слева)
      int hiddenClusters = clusters.Children.Count - displayClusters;
      maxHScrollOffset = hiddenClusters * cfg.u.ClusterWidth;

      hScrollOffset += delta;

      // Ограничиваем скроллинг
      if(hScrollOffset > maxHScrollOffset)
        hScrollOffset = maxHScrollOffset;
      if(hScrollOffset < 0)
        hScrollOffset = 0;

      UpdateOffset();
    }

    /// <summary>
    /// Сбросить горизонтальный скроллинг к последним кластерам
    /// </summary>
    public void ResetHorizontalScroll()
    {
      hScrollOffset = 0;
      UpdateOffset();
    }

    /// <summary>
    /// Проверяет, доступен ли горизонтальный скроллинг
    /// </summary>
    public bool CanHorizontalScroll 
    { 
      get { return nClusters <= 0 && clusters.Children.Count > displayClusters; } 
    }

    // **********************************************************************

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
      // Shift + колесико = горизонтальный скроллинг
      if(Keyboard.Modifiers == ModifierKeys.Shift && CanHorizontalScroll)
      {
        HorizontalScroll(-Math.Sign(e.Delta) * cfg.u.ClusterWidth);
        e.Handled = true;
      }

      base.OnMouseWheel(e);
    }

    // **********************************************************************
  }
}
