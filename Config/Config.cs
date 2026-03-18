// =====================================================================
//    Config.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =====================================================================

using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;

namespace QScalp
{
  static class cfg
  {
    // **********************************************************************
    // *                        Constants & Properties                      *
    // **********************************************************************

    public const string ProgName = "QScalp";
    public static readonly string FullProgName;

    // **********************************************************************

    public const int QuikTryConnectInterval = 1000;

    public const string StockTopicName = "stock";
    public const string TradesTopicName = "trades";

    // **********************************************************************

    public static readonly TimeSpan RefreshInterval = new TimeSpan(0, 0, 0, 0, 15);
    public static readonly TimeSpan SbUpdateInterval = new TimeSpan(0, 0, 0, 0, 250);

    // **********************************************************************

    public static StatSettings s { get; private set; }
    public static UserSettings35 u { get; private set; }

    // **********************************************************************

    public static string MainFormTitle { get; private set; }

    public static Typeface BaseFont { get; private set; }
    public static Typeface BoldFont { get; private set; }

    public static CultureInfo BaseCulture { get; private set; }
    public static NumberFormatInfo PriceFormat { get; private set; }

    public static double QuoteHeight { get; private set; }
    public static double TextTopOffset { get; private set; }
    public static double TextMinWidth { get; private set; }

    // **********************************************************************

    public const Key FKeySaveConf = Key.F2;
    public const Key FKeyLoadConf = Key.F3;
    public const Key FKeyCfgOrExit = Key.F4;
    public const Key FKeyTradeLog = Key.F5;
    public const Key FKeyDropPos = Key.F7;
    public const Key FKeyClearGuide = Key.F8;
    public const Key FKeyClearLevels = Key.F9;
    public const Key FKeyShowMenu = Key.F10;

    // **********************************************************************

    public const string UserCfgFileExt = "cfg";
    public const string TradeLogFileExt = "csv";
    public const string FlushScArg = "-FlushStaticConfig";

    // **********************************************************************

    public static readonly string ExecFile;
    public static readonly string UserCfgFile;
    public static readonly string StatCfgFile;
    public static readonly string LogFile;
    public static readonly string WsDebugLogFile;
    public static readonly string SecFile;
    public static readonly string TradeLogFile;


    // **********************************************************************
    // *                             Constructor                            *
    // **********************************************************************

    static cfg()
    {
      // ------------------------------------------------------------

      Version ver = Assembly.GetExecutingAssembly().GetName().Version;
      FullProgName = ProgName + " " + ver.Major.ToString() + "." + ver.Minor.ToString();

      // ------------------------------------------------------------

      ExecFile = Assembly.GetExecutingAssembly().Location;
      string fs = ExecFile.Remove(ExecFile.LastIndexOf('.') + 1);

      UserCfgFile = fs + UserCfgFileExt;
      StatCfgFile = fs + "sc";
      LogFile = fs + "Log.csv";

      fs = fs.Remove(fs.LastIndexOf('\\') + 1);
      WsDebugLogFile = fs + "ws_debug.log";
      SecFile = fs + "seclist.csv";
      TradeLogFile = fs + "trades." + TradeLogFileExt;

      // ------------------------------------------------------------

      BaseCulture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
      BaseCulture.NumberFormat.NumberDecimalDigits = 0;

      PriceFormat = (NumberFormatInfo)NumberFormatInfo.CurrentInfo.Clone();

      // ------------------------------------------------------------
    }


    // **********************************************************************
    // *                          Properties reinit                         *
    // **********************************************************************

    public static void Reinit()
    {
      MainFormTitle = u.SecCode.Length > 0 ? u.SecCode + " - " + cfg.FullProgName : cfg.FullProgName;

      PriceFormat.NumberDecimalDigits = (int)Math.Log10(u.PriceRatio);

      BaseFont = new Typeface(
        new FontFamily(u.FontFamily),
        FontStyles.Normal,
        FontWeights.Medium,
        FontStretches.Normal);

      BoldFont = new Typeface(
        BaseFont.FontFamily,
        FontStyles.Normal,
        FontWeights.Bold,
        FontStretches.Normal);

      FormattedText ft = new FormattedText(
        "8", BaseCulture, FlowDirection.LeftToRight, BaseFont, u.FontSize, s.QuoteTextBrush);

      QuoteHeight = Math.Ceiling(ft.Extent + s.TextVMargin * 2);
      TextTopOffset = ft.Baseline - ft.Extent / 2;
      TextMinWidth = ft.MinWidth * 1.4;
    }


    // **********************************************************************
    // *                         Static config methods                      *
    // **********************************************************************

    public static void FlushStaticConfig()
    {
      try
      {
        using(Stream fs = new FileStream(StatCfgFile, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
          XmlSerializer xs = new XmlSerializer(typeof(StatSettings));
          xs.Serialize(fs, new StatSettings());
        }

        MessageBox.Show("Файл статической конфигурации создан:\n" + StatCfgFile,
          cfg.ProgName, MessageBoxButton.OK, MessageBoxImage.Asterisk);
      }
      catch(Exception e)
      {
        MessageBox.Show("Ошибка сохранения файла \'" + StatCfgFile + "\':\n"
          + e.Message, cfg.ProgName, MessageBoxButton.OK, MessageBoxImage.Exclamation);
      }
    }

    // **********************************************************************

    public static void LoadStaticConfig()
    {
      if(File.Exists(StatCfgFile))
        try
        {
          using(Stream fs = File.OpenRead(StatCfgFile))
          {
            XmlSerializer xs = new XmlSerializer(typeof(StatSettings));
            s = (StatSettings)xs.Deserialize(fs);
          }
        }
        catch(Exception e)
        {
          MessageBox.Show("Ошибка загрузки файла \'" + StatCfgFile + "\':\n"
            + e.Message + "\n\nУдалите его или создайте вновь.",
            cfg.ProgName, MessageBoxButton.OK, MessageBoxImage.Exclamation);

          s = new StatSettings();
        }
      else
        s = new StatSettings();
    }


    // **********************************************************************
    // *                        User config methods                         *
    // **********************************************************************

    public static void SaveUserConfig(string fn)
    {
      try
      {
        using(Stream fs = new FileStream(fn, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
          XmlSerializer xs = new XmlSerializer(typeof(UserSettings35));
          xs.Serialize(fs, u);
        }
      }
      catch(Exception e)
      {
        MessageBox.Show("Ошибка сохранения конфигурационного файла:\n" + e.Message,
          cfg.ProgName, MessageBoxButton.OK, MessageBoxImage.Exclamation);
      }
    }

    // **********************************************************************

    public static void LoadUserConfig(string fn)
    {
      try
      {
        using(Stream fs = File.OpenRead(fn))
        {
          XmlSerializer xs = new XmlSerializer(typeof(UserSettings35));
          u = (UserSettings35)xs.Deserialize(fs);
        }

        Reinit();
      }
      catch(Exception e)
      {
        if(!(u == null && e is FileNotFoundException))
          MessageBox.Show("Ошибка загрузки конфигурационного файла:\n"
            + e.Message + "\nИспользованы исходные настройки.",
            cfg.ProgName, MessageBoxButton.OK, MessageBoxImage.Exclamation);

        if(u == null)
        {
          u = new UserSettings35();
          Reinit();
        }
      }
    }

    // **********************************************************************
  }
}
