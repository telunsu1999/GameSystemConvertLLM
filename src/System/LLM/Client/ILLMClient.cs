using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GameLoop
{
    public interface ILLMClient
    {
        Task<string> SendAsync(string prompt, int? maxTokens = null, double? temperature = null, bool enableThinking = false);
        Task<string> SendWithToolsAsync(string systemPrompt, string userPrompt, List<object> tools = null, LlmGenConfig genCfg = null);
        Task<bool> HealthCheckAsync();
        Task<bool> HealthCheckAsync(CancellationToken cancellationToken);
    }
}
