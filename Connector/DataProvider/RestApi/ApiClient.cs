// ==========================================================================
//    ApiClient.cs - HTTP-клиент для REST API
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace QScalp.Connector.RestApi
{
    class ApiClient : IDisposable
    {
        // **********************************************************************

        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public string BaseUrl => _baseUrl;
        public Action<string> OnLog;

        // **********************************************************************

        public ApiClient(string baseUrl, string apiKey)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey;
            _http = new HttpClient();
            
            if (!string.IsNullOrEmpty(apiKey))
                _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            
            _http.Timeout = TimeSpan.FromMinutes(5);
        }

        // **********************************************************************

        public async Task<QuotesResponse> GetQuotesAsync(
            string ticker, 
            string timestampGte = null, 
            int limit = 1000)
        {
            var url = BuildUrl($"/v3/quotes/{ticker}", timestampGte, limit);
            return await GetAsync<QuotesResponse>(url);
        }

        // **********************************************************************

        public async Task<TradesResponse> GetTradesAsync(
            string ticker, 
            string timestampGte = null, 
            int limit = 1000)
        {
            var url = BuildUrl($"/v3/trades/{ticker}", timestampGte, limit);
            return await GetAsync<TradesResponse>(url);
        }

        /// <summary> Загружает все страницы quotes по next_url. Для исторического режима (полный день). </summary>
        public async Task<QuoteResult[]> FetchAllQuotesAsync(string ticker, string timestampParam, int limit = 5000)
        {
            var url = BuildUrl($"/v3/quotes/{ticker}", timestampParam, limit);
            var list = new List<QuoteResult>();
            int page = 1;
            OnLog?.Invoke($"[API] Quotes page {page} request: {url}");
            QuotesResponse r = await GetAsync<QuotesResponse>(url);
            if (r?.Results != null) list.AddRange(r.Results);
            OnLog?.Invoke($"[API] Quotes page {page}: {r?.Results?.Length ?? 0} items, status={r?.Status}, total: {list.Count}");
            while (!string.IsNullOrEmpty(r?.NextUrl))
            {
                page++;
                OnLog?.Invoke($"[API] Quotes page {page} request...");
                r = await GetByUrlAsync<QuotesResponse>(r.NextUrl);
                if (r?.Results != null) list.AddRange(r.Results);
                OnLog?.Invoke($"[API] Quotes page {page}: {r?.Results?.Length ?? 0} items, total: {list.Count}");
            }
            OnLog?.Invoke($"[API] Quotes fetch complete: {list.Count} total, {page} pages");
            return list.ToArray();
        }

        /// <summary> Загружает все страницы trades по next_url. Для исторического режима (полный день). </summary>
        public async Task<TradeResult[]> FetchAllTradesAsync(string ticker, string timestampParam, int limit = 5000)
        {
            var url = BuildUrl($"/v3/trades/{ticker}", timestampParam, limit);
            var list = new List<TradeResult>();
            int page = 1;
            OnLog?.Invoke($"[API] Trades page {page} request: {url}");
            TradesResponse r = await GetAsync<TradesResponse>(url);
            if (r?.Results != null) list.AddRange(r.Results);
            OnLog?.Invoke($"[API] Trades page {page}: {r?.Results?.Length ?? 0} items, status={r?.Status}, total: {list.Count}");
            while (!string.IsNullOrEmpty(r?.NextUrl))
            {
                page++;
                OnLog?.Invoke($"[API] Trades page {page} request...");
                r = await GetByUrlAsync<TradesResponse>(r.NextUrl);
                if (r?.Results != null) list.AddRange(r.Results);
                OnLog?.Invoke($"[API] Trades page {page}: {r?.Results?.Length ?? 0} items, total: {list.Count}");
            }
            OnLog?.Invoke($"[API] Trades fetch complete: {list.Count} total, {page} pages");
            return list.ToArray();
        }

        // **********************************************************************

        private async Task<T> GetAsync<T>(string url)
        {
            return await GetWithRetryAsync<T>(url);
        }

        /// <summary> Запрос по абсолютному URL (для next_url пагинации). </summary>
        internal async Task<T> GetByUrlAsync<T>(string absoluteUrl)
        {
            if (string.IsNullOrEmpty(absoluteUrl)) return default(T);
            
            // Добавляем apiKey если его нет в URL
            if (!string.IsNullOrEmpty(_apiKey) && !absoluteUrl.Contains("apiKey="))
            {
                absoluteUrl += (absoluteUrl.Contains("?") ? "&" : "?") + $"apiKey={_apiKey}";
            }
            
            return await GetWithRetryAsync<T>(absoluteUrl);
        }

        // **********************************************************************

        /// <summary> HTTP GET с retry логикой </summary>
        private async Task<T> GetWithRetryAsync<T>(string url, int maxRetries = 3)
        {
            Exception lastEx = null;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    OnLog?.Invoke($"[API] GET (attempt {attempt}) {url}");
                    var response = await _http.GetAsync(url);
                    OnLog?.Invoke($"[API] Response: {(int)response.StatusCode} {response.ReasonPhrase}");
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    OnLog?.Invoke($"[API] Body: {json.Length} chars");
                    return JsonConvert.DeserializeObject<T>(json);
                }
                catch (TaskCanceledException ex)
                {
                    lastEx = ex;
                    OnLog?.Invoke($"[API] Request timeout (attempt {attempt}/{maxRetries}): {url}");
                    if (attempt < maxRetries)
                        await Task.Delay(1000 * attempt);
                }
                catch (HttpRequestException ex)
                {
                    lastEx = ex;
                    OnLog?.Invoke($"[API] HTTP error (attempt {attempt}/{maxRetries}): {ex.Message}");
                    if (attempt < maxRetries)
                        await Task.Delay(1000 * attempt);
                }
            }
            
            throw lastEx;
        }

        // **********************************************************************

        private string BuildUrl(string endpoint, string timestampParam, int limit)
        {
            var sb = new StringBuilder(_baseUrl);
            sb.Append(endpoint);
            sb.Append($"?limit={limit}");
            
            if (!string.IsNullOrEmpty(_apiKey))
                sb.Append($"&apiKey={_apiKey}");
            
            if (!string.IsNullOrEmpty(timestampParam))
            {
                // Дата (YYYY-MM-DD) использует параметр "timestamp"
                // Nanosecond timestamp использует "timestamp.gte"
                bool isDate = timestampParam.Length == 10 && timestampParam[4] == '-';
                string paramName = isDate ? "timestamp" : "timestamp.gte";
                sb.Append($"&{paramName}={timestampParam}");
            }
            
            return sb.ToString();
        }

        // **********************************************************************

        public void Dispose()
        {
            _http?.Dispose();
        }

        // **********************************************************************
    }
}
