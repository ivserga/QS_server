// ==========================================================================
//  Signal.cs — Базовая модель торгового сигнала, выдаваемого детекторами
// ==========================================================================

using System;

namespace QScalp.View.ClustersSpace.Analytics
{
  // ==========================================================================

  enum SignalKind
  {
    /// <summary>Пусто.</summary>
    None,
    /// <summary>Абсорбция продавцом (шип объёма на верхней границе, цена не пробила).</summary>
    AbsorptionSell,
    /// <summary>Абсорбция покупателем (шип объёма на нижней границе, цена не пробила).</summary>
    AbsorptionBuy,
    /// <summary>Продающий климакс (лавинный объём в узкой зоне у низа).</summary>
    SellingClimax,
    /// <summary>Покупающий климакс (лавинный объём в узкой зоне у верха).</summary>
    BuyingClimax,
    /// <summary>Баланс после климакса (объём и range резко схлопнулись).</summary>
    BalanceAfterClimax,
    /// <summary>V-разворот: absorption у низа + бычий разворотный кластер.</summary>
    VReversalUp,
    /// <summary>^-разворот: absorption у верха + медвежий разворотный кластер.</summary>
    VReversalDown,
    /// <summary>Миграция POC вверх: 3 последних POC идут по возрастанию — продолжение тренда вверх.</summary>
    PocMigrationUp,
    /// <summary>Миграция POC вниз: 3 последних POC идут по убыванию — продолжение тренда вниз.</summary>
    PocMigrationDown,
    /// <summary>HVN сыграл поддержкой (бар пришёл сверху, коснулся HVN, закрылся выше) — ожидание отскока вверх.</summary>
    HvnRejectUp,
    /// <summary>HVN сыграл сопротивлением (бар пришёл снизу, коснулся HVN, закрылся ниже) — ожидание отскока вниз.</summary>
    HvnRejectDown,
    /// <summary>Распределение у вершины: lower_highs, угасание объёма, POC съезжает вниз.</summary>
    Distribution,
    /// <summary>Накопление у дна: higher_lows, объём набирается на покупках, POC ползёт вверх.</summary>
    Accumulation,
    /// <summary>Пробой вверх: close выше максимума N предыдущих баров на объёме, POC у верха бара.</summary>
    BreakoutUp,
    /// <summary>Пробой вниз: close ниже минимума N предыдущих баров на объёме, POC у низа бара.</summary>
    BreakoutDown,
    /// <summary>Двойная вершина: два пика на близких ценах с коррекцией между ними и затуханием на втором.</summary>
    DoubleTop,
    /// <summary>Двойное дно: два минимума на близких ценах с восстановлением между ними и истощением продаж на втором.</summary>
    DoubleBottom,
    /// <summary>«Сиротское» закрытие у низа: close улетел вниз от области реальной торговли (POC/Top3) — ожидание отскока к POC.</summary>
    OrphanCloseUp,
    /// <summary>«Сиротское» закрытие у верха: close улетел вверх от области реальной торговли — ожидание возврата к POC.</summary>
    OrphanCloseDown
  }

  // ==========================================================================

  enum SignalDirection { None, Up, Down }

  // ==========================================================================

  /// <summary>
  /// Описание сработавшего сигнала: от какого детектора, когда, по какой цене,
  /// с какой уверенностью и с текстом для пользователя.
  /// </summary>
  sealed class Signal
  {
    public DateTime Time;
    public SignalKind Kind;
    public SignalDirection Direction;

    /// <summary>Ключевая цена сигнала (уровень absorption / POC климакса и т. п.).</summary>
    public int Price;

    /// <summary>Оценка уверенности сигнала в диапазоне [0..1].</summary>
    public double Strength;

    /// <summary>Короткое сообщение для UI (Messenger).</summary>
    public string Message;

    /// <summary>Расширенное описание для CSV-лога (без переносов).</summary>
    public string Details;

    /// <summary>Имя детектора-источника (для диагностики).</summary>
    public string Source;
  }

  // ==========================================================================
}
