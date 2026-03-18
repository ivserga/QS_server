// =========================================================================
//    CfgChecker.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =========================================================================

namespace QScalp
{
  partial class MainWindow
  {
    // **********************************************************************
    // *                    Проверка изменения настроек                     *
    // **********************************************************************

    void CheckConfigChanges(UserSettings35 old)
    {
      this.Topmost = cfg.u.WindowTopmost;

      menuEmulation.IsChecked = cfg.u.TermEmulation;

      if(cfg.u.WorkSize != old.WorkSize)
        UpdateWorkSize(0);

      // ------------------------------------------------------------

      if(cfg.u.SecCode != old.SecCode
        || cfg.u.ClassCode != old.ClassCode
        || cfg.u.PriceRatio != old.PriceRatio
        || cfg.u.PriceStep != old.PriceStep)
      {
        this.Title = cfg.MainFormTitle;

        dp.Disconnect();
        tmgr.Disconnect();

        tmgr.DropState();
        sv.ClearOrders();

        dp.Connect();
        tmgr.Connect();

        sv.RebuildStock();
        sv.RebuildClusters();
        sv.RebuildInfo();
        sv.RebuildGraph();
        sv.RebuildAux();
      }
      else
      {
        // ------------------------------------------------

        if(cfg.u.DdeServerName != old.DdeServerName
          || cfg.u.ApiBaseUrl != old.ApiBaseUrl
          || cfg.u.WsBaseUrl != old.WsBaseUrl
          || cfg.u.ApiKey != old.ApiKey)
        {
          dp.Disconnect();
          dp.Connect();
        }

        // При изменении даты данных или режима исторических данных - очищаем и перезагружаем
        if(cfg.u.ApiDataDate != old.ApiDataDate
          || cfg.u.SkipHistoricalData != old.SkipHistoricalData)
        {
          dp.Disconnect();
          sv.ClearAllData();
          dp.Connect();
        }

        if(cfg.u.TermEmulation != old.TermEmulation)
        {
          tmgr.Disconnect();
          tmgr.DropState();
          tmgr.Connect();
        }
        else if(cfg.u.QuikFolder != old.QuikFolder
          || cfg.u.EnableQuikLog != old.EnableQuikLog)
        {
          tmgr.Disconnect();
          tmgr.Connect();
        }

        // ------------------------------------------------

        if(cfg.u.Grid1Step != old.Grid1Step
          || cfg.u.Grid2Step != old.Grid2Step
          || cfg.u.FontFamily != old.FontFamily
          || cfg.u.FontSize != old.FontSize)
        {
          sv.RebuildStock();
          sv.RebuildClusters();
          sv.RebuildInfo();
          sv.RebuildGraph();
          sv.RebuildAux();
        }
        else
        {
          if(cfg.u.FullVolume != old.FullVolume
            || cfg.u.VQuoteVolumeWidth != old.VQuoteVolumeWidth
            || cfg.u.VQuotePriceWidth != old.VQuotePriceWidth)
            sv.RebuildStock();

          if(cfg.u.Clusters != old.Clusters
            || cfg.u.ClusterLegend != old.ClusterLegend
            || cfg.u.ClusterValueFilter != old.ClusterValueFilter
            || cfg.u.ClusterView != old.ClusterView
            || cfg.u.ClusterFill != old.ClusterFill
            || cfg.u.ClusterOpacityDelta != old.ClusterOpacityDelta
            || cfg.u.ClusterFillVolume1 != old.ClusterFillVolume1
            || cfg.u.ClusterFillVolume2 != old.ClusterFillVolume2
            || cfg.u.ClusterWidth != old.ClusterWidth)
            sv.RebuildClusters();

          bool guideChanged = cfg.u.GuideSources.Length != old.GuideSources.Length;
          for(int i = 0; !guideChanged && i < cfg.u.GuideSources.Length; i++)
            if(!cfg.u.GuideSources[i].Equals(old.GuideSources[i]))
              guideChanged = true;

          bool toneChanged = cfg.u.ToneSources.Length != old.ToneSources.Length;
          for(int i = 0; !toneChanged && i < cfg.u.ToneSources.Length; i++)
            if(!cfg.u.ToneSources[i].Equals(old.ToneSources[i]))
              toneChanged = true;

          if(cfg.u.SpreadTickWidth != old.SpreadTickWidth
            || cfg.u.SpreadsTickInterval != old.SpreadsTickInterval
            || cfg.u.TradesTickInterval != old.TradesTickInterval
            || guideChanged
            || cfg.u.GuideTickWidth != old.GuideTickWidth
            || cfg.u.GuideTickHeight != old.GuideTickHeight
            || cfg.u.GuideTickInterval != old.GuideTickInterval
            || toneChanged
            || cfg.u.ToneMeterHeight != old.ToneMeterHeight)
            sv.RebuildGraph();
        }

        // ------------------------------------------------
      }

      // ------------------------------------------------------------
    }

    // **********************************************************************
  }
}
