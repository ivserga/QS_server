using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace QScalp.Server.Connector.RestApi
{
    class ApiClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public string BaseUrl => _baseUrl;
        public Action<string> OnLog;

        // ********************************************************************

        public ApiClient(string baseUrl, string apiKey)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey;
            _http = new HttpClient();

            if (!string.IsNullOrEmpty(apiKey))
                _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            _http.Timeout = TimeSpan.FromMinutes(5);
        }

        // ********************************************************************

        public async Task<QuoteResult[]> FetchAllQuotesAsync(string ticker, string timestampParam, int limit = 5000)
        {
            var url = BuildUrl($"/v3/quotes/{ticker}", timestampParam, limit);
            var list = new List<QuoteResult>();
            int page = 1;
            OnLog?.Invoke($"Quotes page {page}: {url}");
            var r = await GetWithRetryAsync<QuotesResponse>(url);
            if (r?.Results != null) list.AddRange(r.Results);
            while (!string.IsNullOrEmpty(r?.NextUrl))
            {
                page++;
                r = await GetByUrlAsync<QuotesResponse>(r.NextUrl);
                if (r?.Results != null) list.AddRange(r.Results);
            }
            OnLog?.Invoke($"Quotes: {list.Count} total, {page} pages");
            return list.ToArray();
        }

        // ********************************************************************

        public async Task<TradeResult[]> FetchAllTradesAsync(string ticker, string timestampParam, int limit = 5000)
        {
            var url = BuildUrl($"/v3/trades/{ticker}", timestampParam, limit);
            var list = new List<TradeResult>();
            int page = 1;
            OnLog?.Invoke($"Trades page {page}: {url}");
            var r = await GetWithRetryAsync<TradesResponse>(url);
            if (r?.Results != null) list.AddRange(r.Results);
            while (!string.IsNullOrEmpty(r?.NextUrl))
            {
                page++;
                r = await GetByUrlAsync<TradesResponse>(r.NextUrl);
                if (r?.Results != null) list.AddRange(r.Results);
            }
            OnLog?.Invoke($"Trades: {list.Count} total, {page} pages");
            return list.ToArray();
        }

        // ********************************************************************

        private async Task<T> GetByUrlAsync<T>(string absoluteUrl)
        {
            if (string.IsNullOrEmpty(absoluteUrl)) return default(T);
            if (!string.IsNullOrEmpty(_apiKey) && !absoluteUrl.Contains("apiKey="))
                absoluteUrl += (absoluteUrl.Contains("?") ? "&" : "?") + $"apiKey={_apiKey}";
            return await GetWithRetryAsync<T>(absoluteUrl);
        }

        // ********************************************************************

        private async Task<T> GetWithRetryAsync<T>(string url, int maxRetries = 3)
        {
            Exception lastEx = null;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await _http.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<T>(json);
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    OnLog?.Invoke($"HTTP error (attempt {attempt}/{maxRetries}): {ex.Message}");
                    if (attempt < maxRetries)
                        await Task.Delay(1000 * attempt);
                }
            }
            throw lastEx;
        }

        // ********************************************************************

        private string BuildUrl(string endpoint, string timestampParam, int limit)
        {
            var sb = new StringBuilder(_baseUrl);
            sb.Append(endpoint);
            sb.Append($"?limit={limit}");
            if (!string.IsNullOrEmpty(_apiKey))
                sb.Append($"&apiKey={_apiKey}");
            if (!string.IsNullOrEmpty(timestampParam))
            {
                bool isDate = timestampParam.Length == 10 && timestampParam[4] == '-';
                string paramName = isDate ? "timestamp" : "timestamp.gte";
                sb.Append($"&{paramName}={timestampParam}");
            }
            return sb.ToString();
        }

        // ********************************************************************

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
