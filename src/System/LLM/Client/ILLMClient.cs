using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameLoop
{
    public interface ILLMClient
    {
        /// <summary>Simple text prompt (legacy).</summary>
        Task<string> SendAsync(string prompt, int? maxTokens = null, double? temperature = null, bool enableThinking = false);

        /// <summary>Structured call with system message and tools.</summary>
        Task<string> SendWithToolsAsync(
            string systemPrompt,
            string userPrompt,
            List<object> tools = null,
            int? maxTokens = null,
            double? temperature = null,
            bool enableThinking = false);

        Task<bool> HealthCheckAsync();
    }
}
