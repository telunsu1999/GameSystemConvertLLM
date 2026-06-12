using System.Threading.Tasks;

namespace GameLoop
{
    public interface ILLMClient
    {
        Task<string> SendAsync(string prompt, int? maxTokens = null, double? temperature = null);
        Task<bool> HealthCheckAsync();
    }
}
