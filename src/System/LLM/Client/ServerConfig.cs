using System.IO;
using Newtonsoft.Json;

namespace GameLoop
{
    /// <summary>
    /// Shared server configuration, read from configs/server.json.
    /// Used by both LLMClient (C#) and llm_server/main.py (Python).
    /// </summary>
    public class ServerConfig
    {
        public string host { get; set; } = "127.0.0.1";
        public int vllm_port { get; set; } = 8000;
        public int proxy_port { get; set; } = 8080;
        public string model_name { get; set; } = "Qwen/Qwen3.5-0.8B";
        public string model_cache_dir { get; set; } = ".hf_cache";
        public int max_model_len { get; set; } = 262144;
        public int tensor_parallel_size { get; set; } = 1;
        public bool language_model_only { get; set; } = true;
        public int health_check_timeout_ms { get; set; } = 2000;

        // --- Computed ---
        public string VllmBaseUrl => $"http://{host}:{vllm_port}";
        public string ProxyBaseUrl => $"http://{host}:{proxy_port}";

        /// <summary>
        /// Load config from configs/server.json relative to repo root.
        /// </summary>
        public static ServerConfig Load(string repoRoot)
        {
            var configPath = Path.Combine(repoRoot, "configs", "server.json");
            if (!File.Exists(configPath))
            {
                System.Console.WriteLine($"[WARN] ServerConfig not found: {configPath}, using defaults");
                return new ServerConfig();
            }
            var json = File.ReadAllText(configPath);
            return JsonConvert.DeserializeObject<ServerConfig>(json) ?? new ServerConfig();
        }
    }
}
