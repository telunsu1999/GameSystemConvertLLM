using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GameLoop
{
    public class LLMClient : ILLMClient, System.IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly int _defaultMaxTokens;
        private readonly double _defaultTemperature;

        public LLMClient(string baseUrl = "http://localhost:8000",
                         int defaultMaxTokens = 64,
                         double defaultTemperature = 0.7)
        {
            _http = new HttpClient();
            _baseUrl = baseUrl.TrimEnd('/');
            _defaultMaxTokens = defaultMaxTokens;
            _defaultTemperature = defaultTemperature;
        }

        public async Task<string> SendAsync(string prompt, int? maxTokens = null, double? temperature = null, bool enableThinking = false)
        {
            var request = new GenerateRequest
            {
                prompt = prompt,
                max_tokens = maxTokens ?? _defaultMaxTokens,
                temperature = temperature ?? _defaultTemperature,
                top_p = 0.9,
                enable_thinking = enableThinking
            };

            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync($"{_baseUrl}/api/v1/generate", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<GenerateResponse>(responseJson);

            return result.text.Trim();
        }

        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                var cts = new System.Threading.CancellationTokenSource(2000);
                var response = await _http.GetAsync($"{_baseUrl}/health", cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private class GenerateRequest
        {
            public string prompt { get; set; }
            public int max_tokens { get; set; }
            public double temperature { get; set; }
            public double top_p { get; set; }
            public bool enable_thinking { get; set; }
        }

        private class GenerateResponse
        {
            public string text { get; set; }
            public int tokens_used { get; set; }
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
