// ==========================================================================
//  LogEntryVM.cs — Одна строка в журнале тикера.
// ==========================================================================
//  Хранит исходный текст и флаги уровня (ERR / WARN), чтобы XAML мог
//  раскрашивать элементы списка триггерами без конвертеров.
// ==========================================================================

namespace QScalp.Client.ViewModels
{
    public sealed class LogEntryVM
    {
        public string Text { get; }
        public bool IsError { get; }
        public bool IsWarning { get; }

        public LogEntryVM(string text)
        {
            Text = text ?? string.Empty;
            // Префиксы ставит TickerVM (см. AppendLog) — здесь чисто
            // распознаём их и поднимаем флаги.
            if (Text.IndexOf("ERR:", System.StringComparison.Ordinal) >= 0)
                IsError = true;
            else if (Text.IndexOf("WARN:", System.StringComparison.Ordinal) >= 0)
                IsWarning = true;
        }

        public override string ToString() => Text;
    }
}
