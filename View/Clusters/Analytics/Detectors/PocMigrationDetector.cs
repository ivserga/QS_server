// ==========================================================================
//  PocMigrationDetector.cs — Миграция POC как индикатор продолжения тренда
// ==========================================================================
//
//  Что это такое:
//    POC (Point of Control) — цена с максимальным объёмом внутри кластера.
//    Если POC каждого нового бара сдвигается в одну и ту же сторону, это
//    означает, что «центр тяжести» торговли перемещается — тренд строит
//    новую область value area. Классический сетап trend-following.
//
//  Ситуация из датасета NVDA 20260422 14:05–14:10:
//    14:05 POC=20157 → 14:06 POC=20192 → 14:07 POC=20220 ...
//    (совпало с ростом объёмов и положительным skew).
//
//  Формальные признаки:
//    • В истории ≥ 3 последних закрытых кластера.
//    • Все 3 POC строго монотонны в одну сторону (up или down).
//    • Суммарный сдвиг POC от первого к третьему ≥ MinShiftTicks * priceStep.
//    • Сглаженный центр объёма (PocCenter = среднее PocPrice, CenterOfMass
//      и середины Top3) сместился в ту же сторону минимум на
//      MinShiftTicks * SmoothConfirmRatio тиков. Это защита от «дрожания»
//      POC между двумя соседними уровнями с почти равным объёмом, когда
//      дискретный POC скачет на 1 тик, а реальный центр тяжести бара стоит
//      на месте — обычная картина на ликвидных активах в боковике.
//    • Directional confirm score (PosPoc + ClosePos)/2 ≥ MinDirectionalConfirm.
//      Объединённый скор «где торговали» (POC) + «где остановились» (close)
//      внутри эмиттирующего бара. Отсекает классические ловушки на топе:
//      бар «зелёный», POCs дискретно мигрируют вверх, но и POC, и close
//      сидят в нижней половине — на самом деле раздача / разворот.
//    • Текущий бар не «схлопнулся»: Volume ≥ VolumeMinRatio * avg по окну.
//    • Направление подтверждено close: close(t0) и close(t2) по ту же сторону.
//    • Не выдаёт повторный сигнал той же направленности подряд.
//
//  Сила:
//    •  50% — относительный сдвиг POC (tick-ы сдвига / MinShiftTicks, cap=1).
//    •  30% — согласованность close (прошли ли они тоже в нужную сторону).
//    •  20% — объём выше среднего.
//
// ==========================================================================

using System;
using System.Globalization;

namespace QScalp.View.ClustersSpace.Analytics.Detectors
{
  // ==========================================================================

  sealed class PocMigrationDetector : ISignalDetector
  {
    // **********************************************************************

    public string Name { get { return "PocMigration"; } }
    public bool Enabled { get; set; }

    // --- параметры --------------------------------------------------------

    /// <summary>Минимальный суммарный сдвиг POC от бара t-2 к t в тиках (priceStep).</summary>
    public int MinShiftTicks       { get; set; }
    /// <summary>Минимальное отношение Volume текущего бара к среднему по окну.</summary>
    public double VolumeMinRatio   { get; set; }
    /// <summary>Длина окна для среднего объёма.</summary>
    public int AverageWindow       { get; set; }
    /// <summary>Сколько баров подряд должен длиться кулдаун после сигнала в ту же сторону.</summary>
    public int CooldownBars        { get; set; }
    /// <summary>
    /// Какую часть от MinShiftTicks должен составлять сдвиг сглаженного центра
    /// объёма (PocCenter) для подтверждения миграции. Защита от «дрожания» POC.
    /// 0 = выключить подтверждение (старое поведение). 0.5 = реальный центр
    /// должен сдвинуться минимум на половину MinShiftTicks.
    /// </summary>
    public double SmoothConfirmRatio { get; set; }

    /// <summary>
    /// Объединённый скор подтверждения направления на эмиттирующем баре,
    /// усредняющий две метрики:
    ///   • PosPoc           — позиция POC внутри бара (где реально торговали)
    ///   • ClosePosInBar    — позиция close внутри бара (где остановились)
    /// Для up-миграции скор = (PosPoc + ClosePos) / 2 должен быть
    /// ≥ MinDirectionalConfirm; для down — (1−PosPoc + 1−ClosePos) / 2.
    ///
    /// Объединённый скор лучше отдельных хард-фильтров: одна слабая метрика
    /// (например, низкий ClosePos на отскок-баре в downtrend'е) не отбрасывает
    /// валидный сигнал, если вторая метрика подтверждает направление.
    ///
    /// Защищает от ловушек:
    ///   • «Надгробие»: бар «зелёный», POC внизу, close в середине →
    ///     PosPoc=0.29, ClosePos=0.48, score=0.385 — UP блокируется.
    ///   • Разворот: POC высоко (был тест верха), но close у low →
    ///     PosPoc=0.73, ClosePos=0.18, score=0.455 — UP блокируется.
    ///   • Отскок-разворот в нисходящем тренде (валидный для DOWN):
    ///     PosPoc=0.73, ClosePos=0.29 → DOWN-score = 0.49 — пропускает.
    ///
    /// 0 = выключить. Стандартное значение 0.45.
    /// </summary>
    public double MinDirectionalConfirm { get; set; }

    /// <summary>
    /// Допустимое отклонение close от POC «в неправильную сторону», в тиках.
    /// Для up-миграции: блокировать, если close < POC − tolerance.
    /// Для down-миграции: блокировать, если close > POC + tolerance.
    /// Малые отклонения (1–8 тиков) — нормальные хвосты бара (POC сидит
    /// у low/high, close чуть в стороне). Большие (≥ ~39 тиков) — failed
    /// breakout above/below value, сигнал ложный.
    /// 0 или отрицательное — выключить проверку. Стандартное 10.
    /// </summary>
    public int CloseRelativePocTolTicks { get; set; }

    // --- состояние --------------------------------------------------------

    DateTime lastEmittedAt = DateTime.MinValue;
    SignalDirection lastDirection = SignalDirection.None;
    int barsSinceLastEmit = int.MaxValue;

    // **********************************************************************

    public PocMigrationDetector()
    {
      Enabled = true;
      MinShiftTicks       = 4;
      VolumeMinRatio      = 0.8;
      AverageWindow       = 5;
      CooldownBars        = 2;
      SmoothConfirmRatio       = 0.5;
      MinDirectionalConfirm    = 0.45;
      CloseRelativePocTolTicks = 10;
    }

    // **********************************************************************

    public Signal Evaluate(ClusterHistory history)
    {
      if(barsSinceLastEmit != int.MaxValue)
        barsSinceLastEmit++;

      var s2 = history.Last(0); // новейший
      var s1 = history.Last(1);
      var s0 = history.Last(2); // самый старый из тройки

      if(s2 == null || s1 == null || s0 == null)
        return null;

      if(s2.Volume == 0 || s2.PriceStep <= 0)
        return null;

      int step = s2.PriceStep;

      int shiftTicks21 = (s2.PocPrice - s1.PocPrice) / step;
      int shiftTicks10 = (s1.PocPrice - s0.PocPrice) / step;
      int shiftTicks20 = (s2.PocPrice - s0.PocPrice) / step;

      bool up   = shiftTicks21 > 0 && shiftTicks10 > 0 && shiftTicks20 >= MinShiftTicks;
      bool down = shiftTicks21 < 0 && shiftTicks10 < 0 && -shiftTicks20 >= MinShiftTicks;

      if(!up && !down)
        return null;

      // Подтверждение через сглаженный центр объёма: чистый PocPrice может
      // прыгать на 1 тик, если два соседних уровня имеют почти равный объём.
      // PocCenter = (PocPrice + CenterOfMass + midpoint(Top3)) / 3 — гладкий.
      double smoothShift = WindowMath.PocCenter(s2) - WindowMath.PocCenter(s0);
      double smoothShiftTicks = smoothShift / step;
      double smoothMin = MinShiftTicks * SmoothConfirmRatio;
      if(SmoothConfirmRatio > 0)
      {
        if(up   && smoothShiftTicks <  smoothMin) return null;
        if(down && smoothShiftTicks > -smoothMin) return null;
      }

      // Объединённый скор подтверждения направления на эмиттирующем баре.
      // Усредняет PosPoc (где торговали) и ClosePos (где остановились).
      // Хард-фильтры по одной из метрик режут валидные кейсы (например,
      // отскок-разворот: POC высоко, close у low — это норма для DOWN
      // сигнала). Объединённый скор отсеивает только истинные ловушки,
      // где обе метрики противоречат направлению.
      if(MinDirectionalConfirm > 0 && s2.MaxPrice > s2.MinPrice)
      {
        double closePosInBar =
          (double)(s2.ClosePrice - s2.MinPrice) /
          (s2.MaxPrice - s2.MinPrice);

        double upScore   = (s2.PosPoc + closePosInBar) * 0.5;
        double downScore = ((1.0 - s2.PosPoc) + (1.0 - closePosInBar)) * 0.5;

        if(up   && upScore   < MinDirectionalConfirm) return null;
        if(down && downScore < MinDirectionalConfirm) return null;
      }

      // Дополнительная проверка: close не должен сильно «улететь» в
      // противоположную направлению сторону от POC эмиттирующего бара.
      // Допуск в тиках обязательно нужен — POC часто сидит ровно на high
      // или low бара, и close естественно оказывается на 1-8 тиков с другой
      // стороны (это норма). Большие гэпы (≈ 30+) — failed breakout above/
      // below value, сигнал ложный.
      if(CloseRelativePocTolTicks > 0)
      {
        int gap = s2.ClosePrice - s2.PocPrice;
        if(up   && gap < -CloseRelativePocTolTicks) return null;
        if(down && gap >  CloseRelativePocTolTicks) return null;
      }

      // Объём не должен быть совсем затухшим (иначе это истощение, а не тренд).
      double avgVol = history.AverageVolumeBefore(AverageWindow);
      if(avgVol > 0 && s2.Volume < avgVol * VolumeMinRatio)
        return null;

      // Гистерезис: тот же бар / тот же направленный сигнал подряд.
      SignalDirection dir = up ? SignalDirection.Up : SignalDirection.Down;

      if(s2.Source.DateTime == lastEmittedAt)
        return null;

      if(dir == lastDirection && barsSinceLastEmit < CooldownBars)
        return null;

      lastEmittedAt = s2.Source.DateTime;
      lastDirection = dir;
      barsSinceLastEmit = 0;

      // Сила сигнала.
      double shiftScore = Math.Min(1.0,
        (double)Math.Abs(shiftTicks20) / Math.Max(1, 2 * MinShiftTicks));

      int closeShift20 = s2.ClosePrice - s0.ClosePrice;
      bool closeAgrees = up ? closeShift20 > 0 : closeShift20 < 0;
      double closeScore = closeAgrees ? 1.0 : 0.2;

      double volScore = avgVol > 0
        ? Math.Min(1.0, Math.Max(0.0, (s2.Volume / avgVol - VolumeMinRatio) / Math.Max(1e-6, 1.0 - VolumeMinRatio)))
        : 0.3;

      double strength = 0.5 * shiftScore + 0.3 * closeScore + 0.2 * volScore;

      return new Signal
      {
        Time = s2.Source.DateTime,
        Kind = up ? SignalKind.PocMigrationUp : SignalKind.PocMigrationDown,
        Direction = dir,
        Price = s2.PocPrice,
        Strength = strength,
        Message = FormatMessage(s0, s1, s2, up, shiftTicks20),
        Details = FormatDetails(s0, s1, s2, up, shiftTicks20, avgVol)
      };
    }

    // **********************************************************************

    static string FormatMessage(ClusterStats s0, ClusterStats s1, ClusterStats s2,
                                bool up, int shiftTicks20)
    {
      string arrow = up ? "вверх" : "вниз";

      return string.Format(CultureInfo.InvariantCulture,
        "Миграция POC {0}: {1} → {2} → {3} (сдвиг {4} тиков), тренд продолжается",
        arrow,
        s0.PocPrice, s1.PocPrice, s2.PocPrice,
        Math.Abs(shiftTicks20));
    }

    static string FormatDetails(ClusterStats s0, ClusterStats s1, ClusterStats s2,
                                bool up, int shiftTicks20, double avgVol)
    {
      double smoothShift = WindowMath.PocCenter(s2) - WindowMath.PocCenter(s0);
      double smoothTicks = s2.PriceStep > 0 ? smoothShift / s2.PriceStep : 0;

      return string.Format(CultureInfo.InvariantCulture,
        "dir={0} pocs={1}->{2}->{3} shiftTicks={4} smoothShiftTicks={5:F2} close={6}->{7}->{8} vol={9} avgVol={10:F0}",
        up ? "up" : "down",
        s0.PocPrice, s1.PocPrice, s2.PocPrice,
        shiftTicks20,
        smoothTicks,
        s0.ClosePrice, s1.ClosePrice, s2.ClosePrice,
        s2.Volume, avgVol);
    }

    // **********************************************************************
  }

  // ==========================================================================
}
