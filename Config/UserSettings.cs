// =========================================================================
//   UserSettings.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =========================================================================

using System.Windows;
using System.Windows.Input;

using QScalp.XMedia;

namespace QScalp
{
  public class UserSettings35
  {
    // **********************************************************************
    // *                             REST API                               *
    // **********************************************************************

    public string ApiBaseUrl = "https://api.massive.com";
    public string ApiKey = "";
    public string ApiDataDate = "";  // Дата загрузки данных (YYYY-MM-DD), пусто = сегодня

    // **********************************************************************
    // *                          QScalp Server                             *
    // **********************************************************************

    public string ServerUrl = "";  // URL сервера QScalp (ws://host:port/)
    public bool UseServer = false; // Подключаться через QScalp.Server вместо напрямую

    // **********************************************************************
    // *                            WebSocket                               *
    // **********************************************************************

    public string WsBaseUrl = "wss://socket.massive.com/stocks";  // WebSocket URL
    public bool UseWebSocket = true;  // Использовать WebSocket для real-time (иначе polling)
    public bool WsDebugMode = false;  // Режим отладки WebSocket (вывод всех сообщений)
    public bool SkipHistoricalData = false;  // Не загружать исторические данные за текущий день
    public int TradeFilterTicks = 0;  // Расширение фильтра сделок за пределы спреда (пунктов ниже bid / выше ask), 0 = строго спред

    // **********************************************************************
    // *                              QUIK & DDE                            *
    // **********************************************************************

    public string QuikFolder = @"C:\Program Files\QUIK";
    public bool EnableQuikLog = false;
    public bool AcceptAllTrades = false;

    public string DdeServerName = cfg.ProgName;

    // **********************************************************************
    // *                                 Счет                               *
    // **********************************************************************

    public string QuikAccount = "SPBFUTxxxxx";
    public string QuikClientCode = "";

    // **********************************************************************
    // *                              Инструмент                            *
    // **********************************************************************

    public string SecCode = "RIM2";
    public string ClassCode = "SPBFUT";

    public int PriceRatio = 1;
    public int PriceStep = 5;

    public int Grid1Step = 100;
    public int Grid2Step = 500;

    public int FullVolume = 300;

    // **********************************************************************
    // *                                Стакан                              *
    // **********************************************************************

    public double VQuoteVolumeWidth = 70;
    public double VQuotePriceWidth = 46;

    // **********************************************************************
    // *                     График спреда и лента сделок                   *
    // **********************************************************************

    public double SpreadTickWidth = 5;
    public int SpreadsTickInterval = 500;

    public int TradesTickInterval = 750;

    public int TradeVolume1 = 1;
    public int TradeVolume2 = 10;
    public int TradeVolume3 = 20;
    public double TradeVolume3Div = 1;

    // **********************************************************************
    // *                               Поводырь                             *
    // **********************************************************************

    public GuideSource[] GuideSources = new GuideSource[] {
      new GuideSource("MICEXINDEXCF", "INDX", 0.01, 1, 1) };
    //new GuideSource("SBER", "EQBR", 0.01, 0.2, 1),
    //new GuideSource("GAZP", "EQNE", 0.01, 0.2, 1),
    //new GuideSource("GMKN", "EQBS", 1, 0.2, 1),
    //new GuideSource("LKOH", "EQBR", 0.1, 0.2, 1),
    //new GuideSource("ROSN", "EQNL", 0.01, 0.2, 0.5) };

    public double GuideTickWidth = 4;
    public double GuideTickHeight = 3;
    public int GuideTickInterval = 0;

    // **********************************************************************
    // *                              Настроение                            *
    // **********************************************************************

    public ToneSource[] ToneSources = new ToneSource[] {
      new ToneSource("SBER", "EQBR", 2000, 10000),
      new ToneSource("GAZP", "EQNE", 2000, 2000),
      new ToneSource("GMKN", "EQBS", 2000, 800),
      new ToneSource("LKOH", "EQBR", 2000, 1200),
      new ToneSource("ROSN", "EQNL", 2000, 1500) };

    public double ToneMeterHeight = 100;

    // **********************************************************************
    // *                               Кластеры                             *
    // **********************************************************************

    public int Clusters = 3;

    public ClusterBase ClusterBase = ClusterBase.Time;
    public int ClusterSize = 600;

    public ClusterBase ClusterLegend = ClusterBase.Volume | ClusterBase.Time;

    public ClusterView ClusterView = ClusterView.Summary;
    public int ClusterValueFilter = 0;

    public ClusterFill ClusterFill = ClusterFill.Double;
    public int ClusterOpacityDelta = 500;
    public int ClusterFillVolume1 = 1000;
    public int ClusterFillVolume2 = 1500;

    public double ClusterWidth = 51;

    public bool ClusterSoundAlerts = true;
    public bool ClusterAlertAbsorption = true;
    public bool ClusterAlertClimax = true;
    public bool ClusterAlertRejection = true;

    // **********************************************************************
    // *                           Сигналы / Detectors                      *
    // **********************************************************************

    // Глобальный включатель всего аналитического конвейера.
    public bool SignalsEnabled = true;

    // Глубина истории кластеров, по которой работают детекторы.
    public int SignalsHistoryCapacity = 128;

    // Писать CSV-лог сигналов (signals.csv рядом с trades.csv).
    public bool SignalsLogToCsv = true;

    // ---- AbsorptionDetector (абсорбция на границе бара) ----
    public bool   AbsorptionEnabled          = true;
    public double AbsorptionMinPocShare      = 0.30;
    public double AbsorptionEdgeThreshold    = 0.85;
    public double AbsorptionTailMaxShare     = 0.05;
    public double AbsorptionVolumeMultiplier = 1.4;
    public int    AbsorptionAverageWindow    = 5;

    // ---- ClimaxDetector (концентрация капитуляции) ----
    public bool   ClimaxEnabled               = true;
    public double ClimaxMinTop3Share          = 0.40;
    public double ClimaxVolumeMultiplier      = 0.8;
    public double ClimaxMinDensityMultiplier  = 1.3;
    public double ClimaxEdgePosPocTop         = 0.75;
    public double ClimaxEdgePosPocBottom      = 0.25;
    public int    ClimaxAverageWindow         = 5;
    public int    ClimaxCooldownBars          = 3;

    // ---- BalanceAfterClimaxDetector (подтверждение завершения импульса) ----
    public bool   BalanceEnabled            = true;
    public double BalanceVolumeShare        = 0.65;
    public double BalanceRangeShare         = 0.65;
    public double BalanceMaxDeltaShare      = 0.15;
    public double BalanceClimaxMinTop3Share = 0.35;
    public double BalanceClimaxDensityMult  = 1.3;
    public int    BalanceAverageWindow      = 5;

    // ---- VReversalDetector (V-образный разворот после абсорбции) ----
    public bool   VReversalEnabled                = true;
    public int    VReversalMinRun                 = 2;
    public double VReversalAbsorptionEdge         = 0.35;
    public double VReversalAbsorptionPocShare     = 0.05;
    public double VReversalMinBodyRatio           = 0.35;
    public double VReversalMinCenterOfMassShift   = 0.15;

    // ---- PocMigrationDetector (миграция POC = продолжение тренда) ----
    public bool   PocMigrationEnabled             = true;
    public int    PocMigrationMinShiftTicks       = 4;
    public double PocMigrationVolumeMinRatio      = 0.8;
    public int    PocMigrationAverageWindow       = 5;
    public int    PocMigrationCooldownBars        = 2;
    /// <summary>
    /// Подтверждение через сглаженный центр (PocPrice + CenterOfMass +
    /// midpoint(Top3))/3. Сдвиг центра должен быть ≥ MinShiftTicks * Ratio,
    /// иначе сигнал отбраковывается как «дрожание POC». 0 = выключить.
    /// </summary>
    public double PocMigrationSmoothConfirmRatio  = 0.5;
    /// <summary>
    /// Объединённый скор подтверждения направления на эмиттирующем баре:
    /// (PosPoc + ClosePosInBar) / 2 для up, симметрично для down. Отсекает
    /// ловушки, где POCs формально мигрируют, но сам бар их не подтверждает
    /// (POC и close оба в неправильной половине бара). 0 = выключить.
    /// </summary>
    public double PocMigrationMinDirectionalConfirm = 0.45;
    /// <summary>
    /// Допуск отклонения close от POC «в неправильную сторону», в тиках.
    /// Up блокируется, если close < POC − tolerance; down — если close >
    /// POC + tolerance. Малые отклонения (≤8 тиков) — норма (POC сидит на
    /// границе бара). Большие (≈30+) — failed breakout above/below value.
    /// 0 = выключить.
    /// </summary>
    public int    PocMigrationCloseRelativePocTolTicks = 10;

    // ---- HvnRejectDetector (отскок от High Volume Node) ----
    public bool   HvnRejectEnabled                = true;
    public int    HvnRejectLookbackBars           = 20;
    public double HvnRejectMinHvnShare            = 0.12;
    public int    HvnRejectMinReclaimTicks        = 2;
    public double HvnRejectMaxTailShare           = 0.15;
    public int    HvnRejectCooldownBars           = 3;

    // ---- DistributionDetector (раздача у вершины: lower_highs + угасание объёма) ----
    public bool   DistributionEnabled             = true;
    public int    DistributionLookback            = 12;
    public int    DistributionMinSwingDiffTicks   = 2;
    public double DistributionMaxVolumeSlope      = -0.03;
    public int    DistributionMinHighAgeBars      = 2;
    public bool   DistributionUseClusterUplift    = true;
    public double DistributionPosPocLowThreshold  = 0.40;
    public double DistributionMinShareLowPosPoc   = 0.45;
    public double DistributionMinStrength         = 0.50;
    public int    DistributionCooldownBars        = 5;

    // ---- AccumulationDetector (накопление у дна: higher_lows + поддержание объёма) ----
    public bool   AccumulationEnabled             = true;
    public int    AccumulationLookback            = 12;
    public int    AccumulationMinSwingDiffTicks   = 2;
    public double AccumulationMinVolumeSlope      = 0.00;
    public int    AccumulationMinLowAgeBars       = 2;
    public bool   AccumulationUseClusterUplift    = true;
    public double AccumulationPosPocHighThreshold = 0.60;
    public double AccumulationMinShareHighPosPoc  = 0.45;
    public double AccumulationMinStrength         = 0.50;
    public int    AccumulationCooldownBars        = 5;

    // ---- BreakoutDetector (пробой диапазона вверх/вниз) ----
    public bool   BreakoutEnabled              = true;
    public int    BreakoutLookbackBars         = 10;
    public int    BreakoutMinBreakoutTicks     = 2;
    public double BreakoutMinBodyRatio         = 0.45;
    public double BreakoutMinVolumeRatio       = 1.00;
    public bool   BreakoutUseClusterUplift     = true;
    public double BreakoutPosPocFavorable      = 0.60;
    public double BreakoutMaxTop3Share         = 0.55;
    public double BreakoutMinStrength          = 0.50;
    public int    BreakoutCooldownBars         = 3;

    // ---- DoubleTopDetector (двойная вершина с затуханием объёма на втором пике) ----
    public bool   DoubleTopEnabled                  = true;
    public int    DoubleTopLookback                 = 25;
    public int    DoubleTopPeakLeftRight            = 2;
    public int    DoubleTopMaxPeakDiffTicks         = 5;
    public int    DoubleTopMinBarsBetweenPeaks      = 3;
    public int    DoubleTopMinCorrectionTicks       = 8;
    public int    DoubleTopMinSecondPeakAgeBars     = 1;
    public int    DoubleTopMaxSecondPeakAgeBars     = 5;
    public int    DoubleTopMinPostPeakDropTicks     = 3;
    public bool   DoubleTopUseClusterUplift         = true;
    public double DoubleTopMaxVolumeRatio           = 1.10;
    /// <summary>Допуск (в priceStep) для сравнения PocCenter двух пиков. 0.5 = полтика.</summary>
    public double DoubleTopPocCenterTolTicks        = 0.5;
    public double DoubleTopMinStrength              = 0.50;
    public int    DoubleTopCooldownBars             = 8;

    // ---- DoubleBottomDetector (двойное дно с истощением продаж на втором минимуме) ----
    public bool   DoubleBottomEnabled                  = true;
    public int    DoubleBottomLookback                 = 25;
    public int    DoubleBottomTroughLeftRight          = 2;
    public int    DoubleBottomMaxTroughDiffTicks       = 5;
    public int    DoubleBottomMinBarsBetweenTroughs    = 3;
    public int    DoubleBottomMinReboundTicks          = 8;
    public int    DoubleBottomMinSecondTroughAgeBars   = 1;
    public int    DoubleBottomMaxSecondTroughAgeBars   = 5;
    public int    DoubleBottomMinPostTroughRiseTicks   = 3;
    public bool   DoubleBottomUseClusterUplift         = true;
    public double DoubleBottomMaxVolumeRatio           = 1.10;
    /// <summary>Допуск (в priceStep) для сравнения PocCenter двух дон. 0.5 = полтика.</summary>
    public double DoubleBottomPocCenterTolTicks        = 0.5;
    public double DoubleBottomMinStrength              = 0.50;
    public int    DoubleBottomCooldownBars             = 8;

    // ---- OrphanCloseDetector (close улетел далеко от POC = «сиротское» закрытие) ----
    // Отключён по умолчанию: на практике даёт слишком много срабатываний.
    // Класс детектора и параметры оставлены — можно включить вручную.
    public bool   OrphanCloseEnabled         = false;
    /// <summary>Минимальный range последнего бара (в priceStep). Отсев мелких баров.</summary>
    public int    OrphanCloseMinRangeTicks   = 10;
    /// <summary>Минимальная доля |close-POC|/range. 0.45 = close в дальней четверти от POC.</summary>
    public double OrphanCloseMinGapShare     = 0.45;
    /// <summary>Окно для среднего объёма (для проверки ликвидности бара).</summary>
    public int    OrphanCloseAverageWindow   = 10;
    /// <summary>Минимальное отношение Volume/avg для жирного бара (отсев тонких).</summary>
    public double OrphanCloseMinVolumeRatio  = 0.80;
    public double OrphanCloseMinStrength     = 0.50;
    public int    OrphanCloseCooldownBars    = 3;

    // **********************************************************************
    // *                              Управление                            *
    // **********************************************************************

    public KeyBindings KeyDownBindings = new KeyBindings(
      new Key[] { Key.Up, Key.Down, Key.Right, Key.Left,
        Key.Delete, Key.Tab, Key.Escape, Key.B, Key.S },
      new OwnAction[][] {
        new OwnAction[] { new OwnAction(TradeOp.Buy, BaseQuote.Counter, 15, -100) },
        new OwnAction[] { new OwnAction(TradeOp.Sell, BaseQuote.Counter, 15, -100) },
        new OwnAction[] { new OwnAction(TradeOp.Upsize, BaseQuote.Similar, 5, -100) },
        new OwnAction[] { new OwnAction(TradeOp.Downsize, BaseQuote.Similar, 5, -100) },
        new OwnAction[] {
          new OwnAction(TradeOp.Cancel),
          new OwnAction(TradeOp.Wait),
          new OwnAction(TradeOp.Close, BaseQuote.Counter, 500, 0) },
        new OwnAction[] {
          new OwnAction(TradeOp.Cancel),
          new OwnAction(TradeOp.Reverse, BaseQuote.Counter, 500, 0) },
        new OwnAction[] { new OwnAction(TradeOp.Cancel) },
        new OwnAction[] { new OwnAction(TradeOp.Buy, BaseQuote.Absolute, 0, -100) },
        new OwnAction[] { new OwnAction(TradeOp.Sell, BaseQuote.Absolute, 0, -100) } });

    public KeyBindings KeyUpBindings = new KeyBindings(
      new Key[] { Key.Up, Key.Down },
      new OwnAction[][] {
        new OwnAction[] {
          new OwnAction(TradeOp.Cancel),
          new OwnAction(TradeOp.Wait),
          new OwnAction(TradeOp.Close, BaseQuote.Counter, 500, 0) },
        new OwnAction[] {
          new OwnAction(TradeOp.Cancel),
          new OwnAction(TradeOp.Wait),
          new OwnAction(TradeOp.Close, BaseQuote.Counter, 500, 0) } });

    // **********************************************************************

    public int WorkSize = 1;
    public int WorkSizeDelta = 1;

    public bool MouseEnabled = false;
    public int MouseSlippage = 100;

    // **********************************************************************

    public Key KeyBlockKey = Key.RightShift;

    public Key KeyCenterSpread = Key.Enter;
    public Key KeyPageUp = Key.PageUp;
    public Key KeyPageDown = Key.PageDown;

    public Key KeyWorkSizeInc = Key.Add;
    public Key KeyWorkSizeDec = Key.Subtract;

    // **********************************************************************
    // *                        Автозакрытие позиции                        *
    // **********************************************************************

    public int AutoStopOffset = 0;
    public int AutoStopSlippage = 100;
    public bool AutoStopTrail = false;
    public int AutoTakeOffset = 0;

    // **********************************************************************
    // *                          Торговый журнал                           *
    // **********************************************************************

    public int SingleTradeTimeout = 4;
    public bool TradeLogFlush = false;

    public bool ShowTradeLog = false;
    public double TlwLeft = 100;
    public double TlwTop = 100;
    public double TlwHeight = 400;

    // **********************************************************************
    // *                         Эмулятор терминала                         *
    // **********************************************************************

    public bool TermEmulation = false;
    public int EmulatorDelayMin = 300;
    public int EmulatorDelayMax = 1000;
    public int EmulatorLimit = 10000;

    // **********************************************************************
    // *                                 Прочее                             *
    // **********************************************************************

    public string FontFamily = "Tahoma, Segoe UI, Microsoft Sans Serif";
    public double FontSize = 10.5;

    public bool WindowTopmost = false;
    public bool ConfirmExit = false;

    public WindowState WindowState = WindowState.Normal;
    public double WindowLeft = 50;
    public double WindowTop = 50;
    public double WindowWidth = 650;
    public double WindowHeight = 670;


    // **********************************************************************
    // *                               Clone()                              *
    // **********************************************************************

    public UserSettings35 Clone()
    {
      UserSettings35 u = (UserSettings35)MemberwiseClone();

      u.GuideSources = (GuideSource[])GuideSources.Clone();
      u.ToneSources = (ToneSource[])ToneSources.Clone();

      return u;
    }

    // **********************************************************************
  }
}
