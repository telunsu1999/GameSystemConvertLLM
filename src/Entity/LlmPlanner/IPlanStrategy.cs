using System.Collections.Generic;

namespace GameLoop
{
    public interface IPlanStrategy
    {
        PlanType Type { get; }

        /// <summary>Build the system prompt (identity, role, current situation).</summary>
        string BuildSystemPrompt(PlanContext ctx, GameEntity entity);

        /// <summary>Build the user prompt (state, goals, trigger context).</summary>
        string BuildUserPrompt(PlanContext ctx, GameEntity entity, CollectResult collected);

        /// <summary>Build function-calling tools from available actions.</summary>
        List<object> BuildTools(List<ActionSchema> allActions, PlanContext ctx);

        /// <summary>Max output tokens for LLM call.</summary>
        int MaxTokens { get; }

        /// <summary>Safety net: minimum ticks between two LLM calls of this type.</summary>
        int CooldownTicks { get; }

        /// <summary>Max actions exposed to LLM (0 = all).</summary>
        int MaxActions { get; }

        /// <summary>Fallback strategy on failure (null = rule fallback).</summary>
        PlanType? FallbackType { get; }

        /// <summary>LLM generation parameters for this strategy (temperature, top_p, etc.).</summary>
        LlmGenConfig GenConfig { get; }
    }
}
