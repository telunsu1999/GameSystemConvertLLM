using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;

namespace GameLoop
{
    public class LLMClient : ILLMClient, System.IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _modelName;
        private readonly int _defaultMaxTokens;
        private readonly double _defaultTemperature;

        // JSON serializer with camelCase naming (OpenAI API convention)
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        public LLMClient(string baseUrl = "http://localhost:8000",
                         string modelName = "Qwen/Qwen3.5-0.8B",
                         int defaultMaxTokens = 64,
                         double defaultTemperature = 0.7)
        {
            _http = new HttpClient();
            _baseUrl = baseUrl.TrimEnd('/');
            _modelName = modelName;
            _defaultMaxTokens = defaultMaxTokens;
            _defaultTemperature = defaultTemperature;
        }

        /// <summary>
        /// Create an LLMClient from the shared configs/server.json.
        /// Connects directly to vLLM (not the proxy) by default.
        /// </summary>
        public static LLMClient FromConfig(string repoRoot,
                                           int defaultMaxTokens = 64,
                                           double defaultTemperature = 0.7,
                                           bool useProxy = false)
        {
            var config = ServerConfig.Load(repoRoot);
            var baseUrl = useProxy ? config.ProxyBaseUrl : config.VllmBaseUrl;
            return new LLMClient(baseUrl, config.model_name, defaultMaxTokens, defaultTemperature);
        }

        public async Task<string> SendAsync(string prompt, int? maxTokens = null, double? temperature = null, bool enableThinking = false)
        {
            var request = new ChatCompletionRequest
            {
                model = _modelName,
                messages = new List<Message>
                {
                    new Message { role = "user", content = prompt }
                },
                max_tokens = maxTokens ?? _defaultMaxTokens,
                temperature = temperature ?? _defaultTemperature,
                top_p = 0.9,
                enable_thinking = enableThinking
            };

            var json = JsonConvert.SerializeObject(request, JsonSettings);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync($"{_baseUrl}/v1/chat/completions", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ChatCompletionResponse>(responseJson, JsonSettings);

            return result.choices[0].message.content.Trim();
        }

        /// <summary>
        /// Structured call with separate system/user messages and optional tools.
        /// </summary>
        public async Task<string> SendWithToolsAsync(
            string systemPrompt,
            string userPrompt,
            List<object> tools = null,
            int? maxTokens = null,
            double? temperature = null,
            bool enableThinking = false)
        {
            var messages = new List<Message>();
            if (!string.IsNullOrEmpty(systemPrompt))
                messages.Add(new Message { role = "system", content = systemPrompt });
            messages.Add(new Message { role = "user", content = userPrompt });

            var request = new ChatCompletionRequest
            {
                model = _modelName,
                messages = messages,
                max_tokens = maxTokens ?? _defaultMaxTokens,
                temperature = temperature ?? _defaultTemperature,
                top_p = 1.0,
                enable_thinking = enableThinking,
                tools = tools,
                tool_choice = tools != null && tools.Count > 0 ? "auto" : null,
            };

            var json = JsonConvert.SerializeObject(request, JsonSettings);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync($"{_baseUrl}/v1/chat/completions", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ChatCompletionResponse>(responseJson, JsonSettings);

            return result.choices[0].message.content.Trim();
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

        // --- OpenAI-compatible request/response models ---

        private class ChatCompletionRequest
        {
            public string model { get; set; }
            public List<Message> messages { get; set; }
            public int max_tokens { get; set; }
            public double temperature { get; set; }
            public double top_p { get; set; }
            public bool enable_thinking { get; set; }
            public List<object> tools { get; set; }
            public string tool_choice { get; set; }
        }

        private class Message
        {
            public string role { get; set; }
            public string content { get; set; }
        }

        private class ChatCompletionResponse
        {
            public List<Choice> choices { get; set; }
        }

        private class Choice
        {
            public Message message { get; set; }
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
