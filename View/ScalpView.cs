// ========================================================================
//    ScalpView.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// ========================================================================

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QScalp.View
{
  sealed class ScalpView : Grid, IDataReceiver
  {
    // **********************************************************************

    StockElement eStock;
    ClustersElement eClusters;
    InfoElement eInfo;
    GraphElement eGraph;

    Highlighter highlighter;
    Messenger messenger;

    double mouseY;

    ViewManager vmgr;

    // Флаг для пересборки стакана после очистки данных
    bool _needsStockRebuild;

    // **********************************************************************

    public delegate void OnQuoteClickDelegate(MouseButtonEventArgs e, int price);
    public OnQuoteClickDelegate OnQuoteClick { get; set; }

    public int SelectedPrice { get { return eStock.SelectedPrice; } }
    public bool AutoCentering { get { return vmgr.AutoCentering; } }

    // **********************************************************************

    public ScalpView()
    {
      vmgr = new ViewManager(this);

      ClipToBounds = true;

      // ------------------------------------------------------------

      eStock = new StockElement(vmgr);
      eClusters = new ClustersElement(vmgr);
      eInfo = new InfoElement(vmgr);
      eGraph = new GraphElement(vmgr);

      highlighter = new Highlighter(vmgr, this);
      messenger = new Messenger(vmgr);

      VSplitter splClusters = new VSplitter(cfg.s.VSplitter1Pen, ResizeClusters);
      VSplitter splVolume = new VSplitter(cfg.s.VSplitter1Pen, ResizeVolume);
      VSplitter splPrice = new VSplitter(cfg.s.VSplitter2Pen, ResizePrice);

      // ------------------------------------------------------------

      int cn = 0;
      int gc;

      Grid.SetColumn(eClusters, cn++);
      Grid.SetColumn(splClusters, cn++);
      Grid.SetColumn(eGraph, gc = cn++);
      Grid.SetColumn(splVolume, cn++);
      Grid.SetColumn(eStock, cn++);
      Grid.SetColumn(splPrice, cn++);
      Grid.SetColumn(eInfo, cn++);

      for(int i = 0; i < cn; i++)
      {
        ColumnDefinitions.Add(new ColumnDefinition());
        if(i != gc)
          ColumnDefinitions[i].Width = new GridLength(1, GridUnitType.Auto);
      }

      Children.Add(highlighter);

      Children.Add(eClusters);
      Children.Add(eGraph);
      Children.Add(eStock);
      Children.Add(eInfo);

      Children.Add(splClusters);
      Children.Add(splPrice);
      Children.Add(splVolume);

      Children.Add(messenger);

      // ------------------------------------------------------------

      Background = cfg.s.BackBrush;
    }

    // **********************************************************************
    // *                    Управление видимой областью                     *
    // **********************************************************************

    public void CenterSpread()
    {
      vmgr.CenterSpread();
      vmgr.AutoCentering = true;
    }

    // **********************************************************************

    public void Page(int n)
    {
      if(vmgr.DataExist)
      {
        vmgr.AutoCentering = false;
        vmgr.Scroll(n * ActualHeight * cfg.s.ManualScrollSize);
      }
    }

    // **********************************************************************
    // *                    Изменение размеров элементов                    *
    // **********************************************************************

    void ResizeClusters(double delta)
    {
      // При режиме "без ограничения" (Clusters <= 0) используем фактическое количество кластеров
      int clusterCount = cfg.u.Clusters > 0 ? cfg.u.Clusters : eClusters.DisplayClusterCount;
      
      if(clusterCount > 0)
      {
        delta = Math.Round(delta / clusterCount);

        if(delta * clusterCount > eGraph.ActualWidth)
          delta = Math.Floor(eGraph.ActualWidth / clusterCount);

        cfg.u.ClusterWidth += delta;
        eClusters.UpdateWidth();
      }
    }

    // **********************************************************************

    void ResizePrice(double delta)
    {
      if(delta > eGraph.ActualWidth)
        delta = eGraph.ActualWidth;

      cfg.u.VQuotePriceWidth += delta;
      eStock.UpdateWidth();
    }

    // **********************************************************************

    void ResizeVolume(double delta)
    {
      if(-delta > eGraph.ActualWidth)
        delta = -eGraph.ActualWidth;

      cfg.u.VQuoteVolumeWidth -= delta;
      eStock.UpdateWidth();
    }

    // **********************************************************************

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
      if(sizeInfo.WidthChanged)
      {
        highlighter.SetWidth(ActualWidth);
        messenger.SetWidth(ActualWidth);
        
        // При режиме "без ограничения" (Clusters <= 0) пересчитываем ширину области кластеров
        if(cfg.u.Clusters <= 0)
          eClusters.UpdateWidth();
      }

      base.OnRenderSizeChanged(sizeInfo);
    }

    // **********************************************************************
    // *                                 Мышь                               *
    // **********************************************************************

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
      if(vmgr.DataExist)
      {
        if(eStock.SelectedPrice != 0)
        {
          if(OnQuoteClick != null)
            OnQuoteClick(e, eStock.SelectedPrice);

          e.Handled = true;
        }
        else
          switch(e.ChangedButton)
          {
            case MouseButton.Left:
              mouseY = e.GetPosition(this).Y;

              Mouse.Capture(this);
              Cursor = Cursors.Hand;

              if(e.ClickCount > 1)
                CenterSpread();

              e.Handled = true;
              break;

            case MouseButton.Right:
              if(e.ClickCount > 1)
                highlighter.LockLevel();
              else
              {
                highlighter.ShowMouse();
                Mouse.Capture(this);
              }

              e.Handled = true;
              break;
          }
      }

      base.OnMouseDown(e);
    }

    // **********************************************************************

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
      if(Mouse.Captured == this)
      {
        switch(e.ChangedButton)
        {
          case MouseButton.Left:
            Cursor = Cursors.Arrow;
            break;

          case MouseButton.Right:
            highlighter.HideMouse();
            break;
        }

        if(e.LeftButton == MouseButtonState.Released
          && e.RightButton == MouseButtonState.Released)
          Mouse.Capture(null);

        e.Handled = true;
      }

      base.OnMouseUp(e);
    }

    // **********************************************************************

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
      Cursor = Cursors.Arrow;
      highlighter.HideMouse();

      base.OnLostMouseCapture(e);
    }

    // **********************************************************************

    protected override void OnMouseMove(MouseEventArgs e)
    {
      if(Mouse.Captured == this)
      {
        if(e.LeftButton == MouseButtonState.Pressed)
        {
          double y = e.GetPosition(this).Y;
          double delta = y - mouseY;

          if(Math.Abs(delta) > cfg.s.CenteringDisable)
            vmgr.AutoCentering = false;

          vmgr.Scroll(delta);
          mouseY = y;
        }
        else
          highlighter.UpdateMouse();

        e.Handled = true;
      }

      base.OnMouseMove(e);
    }

    // **********************************************************************

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
      // Shift + колесико = горизонтальный скроллинг кластеров
      if(Keyboard.Modifiers == ModifierKeys.Shift && eClusters.CanHorizontalScroll)
      {
        eClusters.HorizontalScroll(-Math.Sign(e.Delta) * cfg.u.ClusterWidth);
        e.Handled = true;
      }
      else
      {
        Page(Math.Sign(e.Delta));
        e.Handled = true;
      }
      
      base.OnMouseWheel(e);
    }

    // **********************************************************************
    // *               Реализация интерфейса приема данных                  *
    // **********************************************************************

    public void PutMessage(Message msg)
    {
      vmgr.MsgQueue.Enqueue(msg);
    }

    // **********************************************************************

    public void PutStock(Quote[] quotes, Spread spread)
    {
      eStock.PutQuotes(quotes);

      if(vmgr.Ask != spread.Ask || vmgr.Bid != spread.Bid)
      {
        vmgr.SetSpread(spread);
        vmgr.SpreadsQueue.Enqueue(spread);
        
        // После очистки данных нужно пересоздать ячейки стакана для новых цен
        if(_needsStockRebuild)
        {
          _needsStockRebuild = false;
          int newAsk = spread.Ask;
          int newBid = spread.Bid;
          
          // UI операции должны выполняться в UI потоке
          Dispatcher.BeginInvoke(new Action(() =>
          {
            // Обновляем спред на актуальные значения (могли измениться пока ждали Dispatcher)
            vmgr.SetSpread(new Spread(newAsk, newBid));
            vmgr.CenterSpread();
            vmgr.MsgQueue.Enqueue(new Message($"Stock rebuild: Ask={newAsk}, Bid={newBid}"));
            eStock.Rebuild();
            
            // Принудительно помечаем для обновления
            eStock.InvalidateVisual();
          }));
        }
      }
    }

    // **********************************************************************

    public void PutTrade(string skey, Trade trade)
    {
      vmgr.TradesQueue.Enqueue(skey, trade);
    }

    // **********************************************************************

    public void PutOwnOrder(OwnOrder order)
    {
      vmgr.OrdersList.Enqueue(order);
    }

    // **********************************************************************

    public void PutPosition(int quantity, int price)
    {
      eInfo.PutPosition(quantity, price);
    }

    // **********************************************************************
    // *                         Сервисные функции                          *
    // **********************************************************************

    public void RebuildStock() { eStock.Rebuild(); }
    public void RebuildClusters() { eClusters.Rebuild(); }
    public void RebuildInfo() { eInfo.Rebuild(); }
    public void RebuildGraph() { eGraph.Rebuild(); }
    public void RebuildAux() { highlighter.Rebuild(); }

    public void ClearOrders() { vmgr.OrdersList.Clear(); }
    public void ClearGuide() { eGraph.ClearGuide(); }
    public void ClearLevels() { highlighter.Clear(); }

    /// <summary>
    /// Полностью очищает кластеры (для перезагрузки данных за другую дату)
    /// </summary>
    public void ClearClusters() { eClusters.Clear(); }

    /// <summary>
    /// Очищает котировки стакана
    /// </summary>
    public void ClearStock() { eStock.ClearQuotes(); }

    /// <summary>
    /// Сбрасывает состояние данных для загрузки новых
    /// </summary>
    public void ResetDataState() { vmgr.ResetDataState(); }

    /// <summary>
    /// Полная очистка всех данных для перезагрузки за другую дату
    /// </summary>
    public void ClearAllData()
    {
      // Сначала очищаем очереди чтобы старые данные не обрабатывались
      vmgr.ClearQueues();
      
      ClearClusters();
      ClearStock();
      ClearGuide();
      ResetDataState();
      
      // Флаг для пересборки стакана при получении первых новых данных
      _needsStockRebuild = true;
    }

    // **********************************************************************
  }
}
