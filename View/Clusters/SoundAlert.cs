// ======================================================================
//  SoundAlert.cs — Звуковые оповещения по сигналам кластерного анализа
// ======================================================================

using System;
using System.Media;
using System.Threading;

namespace QScalp.View.ClustersSpace
{
  static class SoundAlert
  {
    // **********************************************************************

    static DateTime lastSoundTime;
    static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(500);

    // **********************************************************************

    public static void PlayAbsorption()
    {
      if(!cfg.u.ClusterSoundAlerts || !cfg.u.ClusterAlertAbsorption)
        return;
      PlayThrottled(SystemSounds.Asterisk);
    }

    // **********************************************************************

    public static void PlayClimax()
    {
      if(!cfg.u.ClusterSoundAlerts || !cfg.u.ClusterAlertClimax)
        return;
      PlayThrottled(SystemSounds.Exclamation);
    }

    // **********************************************************************

    public static void PlayRejection()
    {
      if(!cfg.u.ClusterSoundAlerts || !cfg.u.ClusterAlertRejection)
        return;
      PlayThrottled(SystemSounds.Hand);
    }

    // **********************************************************************

    static void PlayThrottled(SystemSound sound)
    {
      DateTime now = DateTime.UtcNow;

      if(now - lastSoundTime < MinInterval)
        return;

      lastSoundTime = now;

      ThreadPool.QueueUserWorkItem(_ =>
      {
        try { sound.Play(); }
        catch { }
      });
    }

    // **********************************************************************
  }
}
