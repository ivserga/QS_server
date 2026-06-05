// ==========================================================================
//  ClusterLlmSession.cs — потоковый анализ закрытых кластеров через LLM.
// ==========================================================================
//  Обязанности:
//    • Хранит локальный диалог messages[] (system + session-user + пары
//      user/assistant по каждому отправленному бару).
//    • Локальный List<ClusterSnapshot> для построения summary без повторных
//      вызовов API при pruning (без тяжёлого price_levels[]).
//    • Coalesce: если кластеры закрываются быстрее, чем отвечает API,
//      отправляется только последний ожидающий Cluster (с его price_levels).
//    • Rate limit (MinIntervalSeconds) и экспоненциальный backoff при
//      подряд идущих ошибках.
//    • Учёт токенов за сессию (для Messenger-метрик).
//    • Сброс при смене инструмента / по запросу пользователя.
//
//  Не делает:
//    • Не маршаллит в UI-поток сам — обратный вызов onMessage выполняется
//      в потоке sender-а; вызывающий (ClustersElement) должен использовать
//      Dispatcher.BeginInvoke для MsgQueue.Enqueue.
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QScalp.View.ClustersSpace.Llm
{
  sealed class ClusterLlmSession : IDisposable
  {
    // **********************************************************************
    //  Внешние зависимости и настройки (снимок при конструировании).
    // **********************************************************************

    readonly DeepSeekChatClient _client;
    readonly Action<string, bool> _onMessage;   // (text, isError)
    readonly Func<bool> _shouldResetForInstrument; // проверка смены тикера

    readonly string _ticker;
    readonly string _systemPrompt;
    readonly double _temperature;
    readonly int _minIntervalMs;
    readonly int _keepRecentBars;
    readonly int _maxContextTokens;
    readonly bool _logToFile;
    readonly string _logPath;

    // **********************************************************************
    //  Состояние диалога. Все доступы — под _messagesLock, чтобы можно было
    //  безопасно делать Reset() во время летящего запроса.
    // **********************************************************************

    readonly object _messagesLock = new object();
    readonly List<DeepSeekChatClient.Message> _messages = new List<DeepSeekChatClient.Message>();
    readonly List<ClusterSnapshot> _snapshots = new List<ClusterSnapshot>();

    int _nextIndex;

    // **********************************************************************
    //  Очередь / rate limit / backoff. Все — под _queueLock.
    //  _pending — последний не отправленный Cluster (полный объект,
    //  нужен для сериализации с price_levels[]).
    // **********************************************************************

    readonly object _queueLock = new object();
    readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);

    Cluster _pendingCluster;
    int _pendingIndex;
    bool _hasPending;
    DateTime _lastSendUtc = DateTime.MinValue;

    int _consecutiveErrors;
    DateTime _backoffUntilUtc = DateTime.MinValue;
    bool _authReported;   // чтобы не спамить про 401

    // **********************************************************************
    //  Метрики сессии.
    // **********************************************************************

    int _requestCount;
    long _promptTokensTotal;
    long _completionTokensTotal;
    int _lastReportAtRequestCount;

    // **********************************************************************
    //  CancellationToken для Dispose.
    // **********************************************************************

    readonly CancellationTokenSource _disposeCts = new CancellationTokenSource();
    bool _disposed;

    // **********************************************************************
    //  Константы backoff-а.
    // **********************************************************************

    const int MaxConsecutiveErrors = 3;
    static readonly TimeSpan BackoffPause = TimeSpan.FromMinutes(5);
    const int MetricsEveryNRequests = 10;

    // **********************************************************************

    public ClusterLlmSession(
      DeepSeekChatClient client,
      string ticker,
      string classCode,
      string systemPromptTemplate,
      Action<string, bool> onMessage,
      Func<bool> shouldResetForInstrument = null,
      double temperature = 0.3,
      int minIntervalSeconds = 30,
      int keepRecentBars = 15,
      int maxContextTokens = 12000,
      bool logToFile = false,
      string logPath = null)
    {
      if(client == null) throw new ArgumentNullException("client");
      if(onMessage == null) throw new ArgumentNullException("onMessage");

      _client = client;
      _onMessage = onMessage;
      _shouldResetForInstrument = shouldResetForInstrument;

      _ticker = ticker ?? "";
      _temperature = temperature;
      _minIntervalMs = Math.Max(0, minIntervalSeconds) * 1000;
      _keepRecentBars = Math.Max(2, keepRecentBars);
      _maxContextTokens = Math.Max(2000, maxContextTokens);
      _logToFile = logToFile;
      _logPath = logPath;

      _systemPrompt = BuildSystemPrompt(systemPromptTemplate, ticker);

      InitSession();
    }

    // **********************************************************************

    void InitSession()
    {
      lock(_messagesLock)
      {
        _messages.Clear();
        _snapshots.Clear();
        _nextIndex = 0;

        _messages.Add(new DeepSeekChatClient.Message
        {
          Role = "system",
          Content = _systemPrompt
        });

        _messages.Add(new DeepSeekChatClient.Message
        {
          Role = "user",
          Content = BuildSessionHeader()
        });
      }
    }

    static string BuildSystemPrompt(string template, string ticker)
    {
      if(string.IsNullOrWhiteSpace(template))
        template = DefaultSystemPromptTemplate;

      return template.Replace("{ticker}", ticker ?? "")
                     .Replace("{Ticker}", ticker ?? "");
    }

    public const string DefaultSystemPromptTemplate =
      "Ты — опытный трейдер с 10-летним опытом торговли акциями NYSE/Nasdaq " +
      "(включая {ticker}). Твоя задача — анализировать потоковые кластерные " +
      "(footprint) бары и находить действия крупных игроков: поглощения " +
      "(absorption), кульминации (climax), раздачу (distribution), накопление " +
      "(accumulation), ложные пробои, V-развороты.\n\n" +
      "Правила ответа:\n" +
      "• Отвечай кратко, по делу, на русском.\n" +
      "• Не повторяй вводные данные бара обратно — ссылайся на его индекс.\n" +
      "• Если по новому бару нет значимых изменений по сравнению с предыдущим " +
      "контекстом — скажи это одной фразой.\n" +
      "• Фокус на рассуждениях «кто сейчас активнее, покупатели или " +
      "продавцы», и какие ценовые уровни сработали как сопротивление/поддержка.\n" +
      "• Не давай торговых рекомендаций — только наблюдения.\n\n" +
      "Семантика объёмов: bid — продажи (инициированы продавцами на биде), " +
      "ask — покупки (на оффере), inside_spread — нейтральный объём внутри " +
      "спреда, delta = ask - bid.";

    string BuildSessionHeader()
    {
      var sb = new StringBuilder(256);
      sb.Append("Сессия анализа кластеров.\n");
      sb.Append("instrument.sec_code = ").Append(cfg.u.SecCode).Append('\n');
      sb.Append("instrument.class_code = ").Append(cfg.u.ClassCode).Append('\n');
      sb.Append("settings.cluster_base = ").Append(cfg.u.ClusterBase).Append('\n');
      sb.Append("settings.cluster_size = ").Append(cfg.u.ClusterSize).Append('\n');
      sb.Append("settings.price_step = ").Append(cfg.u.PriceStep).Append('\n');
      sb.Append("settings.price_ratio = ").Append(cfg.u.PriceRatio).Append('\n');
      sb.Append("session_date = ").Append(DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('\n');
      sb.Append("\nЖди отдельных сообщений вида «Закрыт кластер #N. ... Данные: {json}».");
      return sb.ToString();
    }

    // **********************************************************************
    //  Точка входа — вызывается из ClustersElement при закрытии кластера.
    //  Никогда не блокирует вызывающий поток дольше нескольких мслоков.
    // **********************************************************************

    public void OnClusterClosed(Cluster cluster)
    {
      if(_disposed || cluster == null || cluster.Volume == 0)
        return;

      // Авто-Reset при смене инструмента.
      if(_shouldResetForInstrument != null && _shouldResetForInstrument())
      {
        Reset();
      }

      int index;

      lock(_messagesLock)
      {
        index = ++_nextIndex;
        // Snapshot хранится всегда — даже если бар уйдёт в coalesce.
        // Это гарантирует, что после pruning summary остаётся осмысленной.
        _snapshots.Add(new ClusterSnapshot(index, cluster));
      }

      // Coalesce: сохраняем только последний ожидающий бар (Cluster целиком).
      lock(_queueLock)
      {
        _pendingCluster = cluster;
        _pendingIndex = index;
        _hasPending = true;
      }

      // Запускаем обработчик. Task.Run — чтобы не блокировать UI-поток.
      // Не awaiting — фоновая работа.
      var ignored = Task.Run(PumpAsync);
    }

    // **********************************************************************

    public void Reset()
    {
      lock(_messagesLock)
        InitSession();

      lock(_queueLock)
      {
        _hasPending = false;
        _pendingCluster = null;
        _consecutiveErrors = 0;
        _backoffUntilUtc = DateTime.MinValue;
        _authReported = false;
      }

      try { _onMessage("[AI] " + (_ticker.Length > 0 ? _ticker + ": " : "") + "диалог сброшен.", false); }
      catch { }
    }

    // **********************************************************************

    public void ResetMetrics()
    {
      _requestCount = 0;
      _promptTokensTotal = 0;
      _completionTokensTotal = 0;
      _lastReportAtRequestCount = 0;
    }

    // **********************************************************************
    //  Главный цикл обработчика. Должен корректно обрабатывать coalesce:
    //  возможна ситуация, когда несколько тасок запустились одновременно,
    //  но _sendGate гарантирует, что реально работает только одна.
    // **********************************************************************

    async Task PumpAsync()
    {
      if(_disposed)
        return;

      try
      {
        await _sendGate.WaitAsync(_disposeCts.Token).ConfigureAwait(false);
      }
      catch(OperationCanceledException)
      {
        return;
      }

      try
      {
        // Coalesce: берём самый свежий pending. Если уже пусто — выходим.
        Cluster cluster;
        int index;

        lock(_queueLock)
        {
          if(!_hasPending)
            return;

          cluster = _pendingCluster;
          index = _pendingIndex;
          _hasPending = false;
          _pendingCluster = null;
        }

        if(cluster == null)
          return;

        // Backoff: если мы в паузе — откладываем бар обратно и уходим.
        if(DateTime.UtcNow < _backoffUntilUtc)
        {
          lock(_queueLock)
          {
            _pendingCluster = cluster;
            _pendingIndex = index;
            _hasPending = true;
          }
          return;
        }

        // Rate limit: не чаще MinIntervalSeconds.
        if(_minIntervalMs > 0)
        {
          var sinceLast = DateTime.UtcNow - _lastSendUtc;
          int wait = _minIntervalMs - (int)sinceLast.TotalMilliseconds;
          if(wait > 0)
          {
            try
            {
              await Task.Delay(wait, _disposeCts.Token).ConfigureAwait(false);
            }
            catch(OperationCanceledException)
            {
              return;
            }
          }
        }

        if(_disposed)
          return;

        await SendOneAsync(cluster, index).ConfigureAwait(false);
        _lastSendUtc = DateTime.UtcNow;
      }
      finally
      {
        try { _sendGate.Release(); }
        catch(ObjectDisposedException) { }
        catch(SemaphoreFullException) { }
      }
    }

    // **********************************************************************

    async Task SendOneAsync(Cluster cluster, int index)
    {
      // Сериализуем бар один раз (с full price_levels[]). Делаем это под
      // messagesLock, чтобы текст был консистентен с тем, что уйдёт в историю.
      string clusterJson;
      string userText;

      lock(_messagesLock)
      {
        clusterJson = ClusterExporter.SerializeClusterLine(index, cluster);
      }

      userText = "Закрыт кластер #" + index.ToString(CultureInfo.InvariantCulture)
        + ". Обнови оценку манипуляций крупных игроков. Данные: " + clusterJson;

      // Подготовка messages для отправки: snapshot + pruning.
      List<DeepSeekChatClient.Message> toSend;

      lock(_messagesLock)
      {
        // Добавляем user-а с новым баром, pruning смотрит на размер.
        _messages.Add(new DeepSeekChatClient.Message { Role = "user", Content = userText });
        PruneIfNeeded();
        toSend = new List<DeepSeekChatClient.Message>(_messages);
      }

      DeepSeekChatClient.ChatResult result;
      try
      {
        result = await _client.SendAsync(toSend, _temperature, _disposeCts.Token).ConfigureAwait(false);
      }
      catch(DeepSeekChatClient.ChatException ex)
      {
        HandleSendError(ex);
        // Откатываем user-сообщение, чтобы при следующей попытке оно не
        // дублировалось (assistant-а всё равно нет).
        lock(_messagesLock)
        {
          // Удаляем только если в хвосте действительно наш user.
          if(_messages.Count > 0
            && _messages[_messages.Count - 1].Role == "user"
            && _messages[_messages.Count - 1].Content == userText)
          {
            _messages.RemoveAt(_messages.Count - 1);
          }
        }
        return;
      }
      catch(OperationCanceledException)
      {
        return;
      }
      catch(Exception ex)
      {
        HandleSendError(new DeepSeekChatClient.ChatException(ex.Message, null, ex));
        lock(_messagesLock)
        {
          if(_messages.Count > 0
            && _messages[_messages.Count - 1].Role == "user"
            && _messages[_messages.Count - 1].Content == userText)
          {
            _messages.RemoveAt(_messages.Count - 1);
          }
        }
        return;
      }

      // Успех: добавляем assistant в историю.
      lock(_messagesLock)
      {
        _messages.Add(new DeepSeekChatClient.Message
        {
          Role = "assistant",
          Content = result.AssistantText
        });
      }

      // Метрики.
      Interlocked.Increment(ref _requestCount);
      if(result.Usage != null)
      {
        Interlocked.Add(ref _promptTokensTotal, result.Usage.PromptTokens);
        Interlocked.Add(ref _completionTokensTotal, result.Usage.CompletionTokens);
      }

      // Сброс счётчика ошибок.
      lock(_queueLock)
      {
        _consecutiveErrors = 0;
        _backoffUntilUtc = DateTime.MinValue;
        _authReported = false;
      }

      // Уведомление.
      string prefix = "[AI] " + (_ticker.Length > 0 ? _ticker + " " : "")
        + cluster.DateTime.ToString("HH:mm", CultureInfo.InvariantCulture) + ": ";

      try { _onMessage(prefix + result.AssistantText, false); } catch { }

      if(_logToFile)
        LogToFile("USR " + userText + "\nAI  " + result.AssistantText);

      // Периодические метрики.
      ReportMetricsIfDue();
    }

    // **********************************************************************

    void HandleSendError(DeepSeekChatClient.ChatException ex)
    {
      bool isAuth = ex.StatusCode == 401 || ex.StatusCode == 403;
      bool isServer = ex.StatusCode.HasValue && ex.StatusCode.Value >= 500;

      lock(_queueLock)
      {
        _consecutiveErrors++;

        if(isAuth)
        {
          if(!_authReported)
          {
            _authReported = true;
            try { _onMessage("[AI] Ошибка авторизации (HTTP " + ex.StatusCode + "). Проверьте LlmApiKey в настройках.", true); } catch { }
          }
          return;
        }

        if(_consecutiveErrors >= MaxConsecutiveErrors || isServer)
        {
          _backoffUntilUtc = DateTime.UtcNow + BackoffPause;
          try
          {
            _onMessage("[AI] " + (_ticker.Length > 0 ? _ticker + ": " : "")
              + "пауза " + (int)BackoffPause.TotalMinutes + " мин после "
              + _consecutiveErrors + " ошибок: " + ex.Message,
              true);
          }
          catch { }
        }
        else
        {
          try
          {
            _onMessage("[AI] " + (_ticker.Length > 0 ? _ticker + ": " : "")
              + "ошибка запроса (" + _consecutiveErrors + "/" + MaxConsecutiveErrors + "): "
              + ex.Message,
              true);
          }
          catch { }
        }
      }
    }

    // **********************************************************************

    void PruneIfNeeded()
    {
      // Должно вызываться под _messagesLock.
      if(_messages.Count <= 4)
        return;

      int estimatedTokens = EstimateTokensUnsafe();
      if(estimatedTokens <= _maxContextTokens)
        return;

      // _messages[0] = system, [1] = session-user — оставляем всегда.
      // Хотим сохранить последние _keepRecentBars пар user/assistant.
      int totalPairs = (_messages.Count - 2) / 2;
      int keepPairs = Math.Min(_keepRecentBars, totalPairs);
      if(keepPairs <= 0)
        return;

      int keepMsgs = keepPairs * 2;
      int firstPairIdx = 2;
      int dropUntilIdx = _messages.Count - keepMsgs;

      if(dropUntilIdx <= firstPairIdx)
        return;

      // Снимаем snapshot-ы, попадающие в выбрасываемый диапазон пар.
      // Каждая пара user/assistant в _messages соответствует одному snapshot-у.
      // Snapshot index в списке — это номер пары по порядку, начиная с 0.
      var sb = new StringBuilder();
      sb.Append("Краткая сводка пропущенных баров (без price_levels):\n");

      int pairCount = (dropUntilIdx - firstPairIdx) / 2;
      for(int i = 0; i < pairCount; i++)
      {
        int snapIdx = i;
        if(snapIdx >= 0 && snapIdx < _snapshots.Count)
          sb.Append(_snapshots[snapIdx].FormatSummary()).Append('\n');
      }

      var summaryMessage = new DeepSeekChatClient.Message
      {
        Role = "user",
        Content = sb.ToString()
      };

      int removeCount = dropUntilIdx - firstPairIdx;
      _messages.RemoveRange(firstPairIdx, removeCount);
      _messages.Insert(firstPairIdx, summaryMessage);

      // Удаляем snapshot-ы, которые «свёрнуты» в summary, чтобы при
      // последующих pruning-ах не дублировать их.
      if(pairCount > 0 && pairCount <= _snapshots.Count)
        _snapshots.RemoveRange(0, pairCount);
    }

    // **********************************************************************

    int EstimateTokens()
    {
      lock(_messagesLock)
        return EstimateTokensUnsafe();
    }

    int EstimateTokensUnsafe()
    {
      long chars = 0;
      for(int i = 0; i < _messages.Count; i++)
      {
        var m = _messages[i];
        if(m != null && m.Content != null)
          chars += m.Content.Length;
      }
      return (int)(chars / 3.5);
    }

    // **********************************************************************

    void ReportMetricsIfDue()
    {
      int n = _requestCount;
      if((n - _lastReportAtRequestCount) < MetricsEveryNRequests)
        return;

      _lastReportAtRequestCount = n;

      var sb = new StringBuilder(128);
      sb.Append("[AI] Метрики сессии ");
      if(_ticker.Length > 0)
        sb.Append(_ticker).Append(' ');
      sb.Append("за ").Append(n.ToString(CultureInfo.InvariantCulture)).Append(" запросов: ");
      sb.Append("prompt=").Append(_promptTokensTotal.ToString(CultureInfo.InvariantCulture));
      sb.Append(", completion=").Append(_completionTokensTotal.ToString(CultureInfo.InvariantCulture));
      sb.Append(", total=").Append((_promptTokensTotal + _completionTokensTotal).ToString(CultureInfo.InvariantCulture));

      try { _onMessage(sb.ToString(), false); } catch { }
    }

    // **********************************************************************

    void LogToFile(string text)
    {
      if(!_logToFile || string.IsNullOrEmpty(_logPath))
        return;

      try
      {
        File.AppendAllText(_logPath,
          DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
          + " | " + text + Environment.NewLine);
      }
      catch
      {
        // логи не должны валить основную логику
      }
    }

    // **********************************************************************

    public void Dispose()
    {
      if(_disposed)
        return;

      _disposed = true;
      try { _disposeCts.Cancel(); } catch { }
      try { _sendGate.Dispose(); } catch { }
      try { _disposeCts.Dispose(); } catch { }
    }
  }
}
