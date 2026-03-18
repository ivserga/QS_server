// =====================================================================
//    QScalp.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =====================================================================

using System;
using System.Threading;
using System.Windows;

namespace QScalp
{
  static class Program
  {
    // **********************************************************************
    // *                                Main()                              *
    // **********************************************************************

    [STAThread]
    static void Main(string[] args)
    {
      AppDomain.CurrentDomain.UnhandledException +=
        new UnhandledExceptionEventHandler(UnhandledException);

      if(args.Length > 0 && args[0] == cfg.FlushScArg)
        cfg.FlushStaticConfig();
      else
        try
        {
          bool first;

          using(Mutex mutex = new Mutex(true, cfg.ExecFile.Replace('\\', ':'), out first))
          {
            if(first)
            {
              // ------------------------------------------------------

              cfg.LoadStaticConfig();
              cfg.LoadUserConfig(cfg.UserCfgFile);

              Application app = new Application();
              app.ShutdownMode = ShutdownMode.OnMainWindowClose;
              app.Run(new MainWindow());

              cfg.SaveUserConfig(cfg.UserCfgFile);

              // ------------------------------------------------------
            }
            else
              MessageBox.Show("Приложение уже запущено:\n" + cfg.ExecFile,
                cfg.ProgName, MessageBoxButton.OK, MessageBoxImage.Hand);
          }
        }
        catch(Exception e)
        {
          UnhandledException(null, new UnhandledExceptionEventArgs(e, true));
        }
    }

    // **********************************************************************

    static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
      if(e.IsTerminating)
        AppDomain.CurrentDomain.UnhandledException -= UnhandledException;

      Exception ex = e.ExceptionObject as Exception;

      MessageBox.Show("В ходе выполнения программы произошла критическая ошибка:\n\n"
        + (ex == null ? e.ToString() : ex.ToString()), cfg.FullProgName,
        MessageBoxButton.OK, MessageBoxImage.Hand,
        MessageBoxResult.OK, MessageBoxOptions.ServiceNotification);
    }

    // **********************************************************************
    //                           Service functions                          *
    // **********************************************************************

    public static void FixWindowLocation(Window window)
    {
      if(window.Left + window.Width <= SystemParameters.VirtualScreenLeft)
        window.Left = SystemParameters.VirtualScreenLeft;

      if(window.Top + window.Height <= SystemParameters.VirtualScreenTop)
        window.Top = SystemParameters.VirtualScreenTop;

      double vsRight = SystemParameters.VirtualScreenLeft
        + SystemParameters.VirtualScreenWidth;

      if(window.Left >= vsRight)
        window.Left = vsRight - window.Width;

      double vsBottom = SystemParameters.VirtualScreenTop
        + SystemParameters.VirtualScreenHeight;

      if(window.Top >= vsBottom)
        window.Top = vsBottom - window.Height;
    }

    // **********************************************************************
  }
}
