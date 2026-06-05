// ==========================================================================
//  DeepSeekChatClient.cs — тонкий клиент OpenAI-compatible chat API.
// ==========================================================================
//  Назначение:
//    • Изолировать HTTP/JSON-бойлерплейт от ClusterLlmSession.
//    • Поддерживает DeepSeek (https://api.deepseek.com) и любой другой
//      совместимый endpoint (OpenAI, OpenRouter, локальный llama.cpp).
//    • Возвращает assistant text + usage (prompt/completion/total tokens)
//      для учёта стоимости сессии.
//
//  Не делает:
//    • Никакой очереди / rate limit / retries — это забота ClusterLlmSession.
//    • Никакого stream — нам нужен один полный ответ на бар.
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace QScalp.View.ClustersSpace.Llm
{
  sealed class DeepSeekChatClient : IDisposable
  {
    readonly HttpClient _http;
    readonly string _model;
    bool _disposed;

    /// <summary>
    /// Создаёт клиент. baseUrl должен быть без завершающего слеша,
    /// например "https://api.deepseek.com" — к нему будет дописан
    /// "/v1/chat/completions".
    /// </summary>
    public DeepSeekChatClient(string apiKey, string baseUrl, string model, TimeSpan timeout)
    {
      if(string.IsNullOrEmpty(apiKey))
        throw new ArgumentException("apiKey");

      _model = string.IsNullOrEmpty(model) ? "deepseek-chat" : model;

      var baseAddr = string.IsNullOrEmpty(baseUrl) ? "https://api.deepseek.com" : baseUrl.TrimEnd('/');

      _http = new HttpClient(new HttpClientHandler
      {
        // У DeepSeek-сервера TLS-сертификат валиден, но оставляем стандартное
        // поведение — не ослабляем безопасность ради отладки.
      })
      {
        BaseAddress = new Uri(baseAddr, UriKind.Absolute),
        Timeout = timeout
      };

      _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    // **********************************************************************

    public sealed class Message
    {
      [JsonProperty("role")]
      public string Role { get; set; }

      [JsonProperty("content")]
      public string Content { get; set; }
    }

    // **********************************************************************

    public sealed class ChatRequest
    {
      [JsonProperty("model")]
      public string Model { get; set; }

      [JsonProperty("messages")]
      public List<Message> Messages { get; set; }

      [JsonProperty("stream")]
      public bool Stream { get { return false; } }

      [JsonProperty("temperature")]
      public double Temperature { get; set; }

      // Поле нужно, чтобы Newtonsoft не выкидывал null-поля при сериализации.
      // Отключаем сериализацию stream — он не нужен в payload (по умолчанию
      // и так false на сервере). Пусть отправляется явно — это помогает при
      // отладке через прокси.
    }

    public sealed class ChatResponse
    {
      [JsonProperty("id")]
      public string Id { get; set; }

      [JsonProperty("model")]
      public string Model { get; set; }

      [JsonProperty("choices")]
      public List<Choice> Choices { get; set; }

      [JsonProperty("usage")]
      public Usage Usage { get; set; }
    }

    public sealed class Choice
    {
      [JsonProperty("index")]
      public int Index { get; set; }

      [JsonProperty("message")]
      public Message Message { get; set; }

      [JsonProperty("finish_reason")]
      public string FinishReason { get; set; }
    }

    public sealed class Usage
    {
      [JsonProperty("prompt_tokens")]
      public int PromptTokens { get; set; }

      [JsonProperty("completion_tokens")]
      public int CompletionTokens { get; set; }

      [JsonProperty("total_tokens")]
      public int TotalTokens { get; set; }
    }

    // **********************************************************************

    public sealed class ChatException : Exception
    {
      public int? StatusCode { get; private set; }

      public ChatException(string message, int? statusCode = null, Exception inner = null)
        : base(message, inner)
      {
        StatusCode = statusCode;
      }
    }

    // **********************************************************************

    /// <summary>
    /// Отправляет messages, возвращает assistant text и usage.
    /// Бросает ChatException при 4xx/5xx или проблемах с сетью.
    /// Не делает ретраев — ретраи / backoff делает ClusterLlmSession.
    /// </summary>
    public async Task<ChatResult> SendAsync(IEnumerable<Message> messages, double temperature,
      CancellationToken ct)
    {
      if(_disposed)
        throw new ObjectDisposedException("DeepSeekChatClient");

      var req = new ChatRequest
      {
        Model = _model,
        Messages = messages is List<Message> ? (List<Message>)messages : new List<Message>(messages),
        Temperature = temperature
      };

      var serializerSettings = new JsonSerializerSettings
      {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore
      };

      string bodyJson = JsonConvert.SerializeObject(req, serializerSettings);

      using(var content = new StringContent(bodyJson, Encoding.UTF8, "application/json"))
      using(var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions") { Content = content })
      using(var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false))
      {
        string responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if(!response.IsSuccessStatusCode)
        {
          // Тело ошибки часто содержит JSON с message — пробуем вытащить.
          string detail = TryExtractErrorMessage(responseText) ?? responseText;
          throw new ChatException(
            "HTTP " + (int)response.StatusCode + " " + response.StatusCode + ": " + detail,
            (int)response.StatusCode);
        }

        ChatResponse parsed;
        try
        {
          parsed = JsonConvert.DeserializeObject<ChatResponse>(responseText);
        }
        catch(Exception ex)
        {
          throw new ChatException("Cannot parse response JSON: " + ex.Message, null, ex);
        }

        if(parsed == null || parsed.Choices == null || parsed.Choices.Count == 0)
          throw new ChatException("Empty choices in response");

        var msg = parsed.Choices[0].Message;
        if(msg == null || string.IsNullOrEmpty(msg.Content))
          throw new ChatException("Empty assistant content");

        var usage = parsed.Usage ?? new Usage();

        return new ChatResult
        {
          AssistantText = msg.Content,
          Usage = usage
        };
      }
    }

    // **********************************************************************

    public sealed class ChatResult
    {
      public string AssistantText { get; set; }
      public Usage Usage { get; set; }
    }

    // **********************************************************************

    static string TryExtractErrorMessage(string body)
    {
      if(string.IsNullOrEmpty(body))
        return null;

      try
      {
        var err = JsonConvert.DeserializeObject<ServerError>(body);
        return err != null ? (err.Error != null ? err.Error.Message : null) : null;
      }
      catch
      {
        return null;
      }
    }

    sealed class ServerError
    {
      [JsonProperty("error")]
      public ServerErrorDetail Error { get; set; }
    }

    sealed class ServerErrorDetail
    {
      [JsonProperty("message")]
      public string Message { get; set; }

      [JsonProperty("type")]
      public string Type { get; set; }

      [JsonProperty("code")]
      public string Code { get; set; }
    }

    // **********************************************************************

    public void Dispose()
    {
      if(!_disposed)
      {
        _disposed = true;
        _http.Dispose();
      }
    }
  }
}
