namespace GameLoop
{
    /// <summary>
    /// Per-strategy LLM generation parameters.
    /// Each plan type can specify its own optimal sampling configuration.
    /// </summary>
    public class LlmGenConfig
    {
        /// <summary>Sampling temperature (0.0-2.0). Lower = more deterministic.</summary>
        public double Temperature { get; set; } = 0.7;

        /// <summary>Nucleus sampling threshold (0.0-1.0).</summary>
        public double TopP { get; set; } = 0.8;

        /// <summary>Top-K sampling. 0 = disabled.</summary>
        public int TopK { get; set; } = 20;

        /// <summary>Presence penalty (-2.0 to 2.0). Positive values discourage repetition.</summary>
        public double PresencePenalty { get; set; } = 1.5;

        /// <summary>Repetition penalty. >1.0 discourages token reuse.</summary>
        public double RepetitionPenalty { get; set; } = 1.05;

        /// <summary>Max output tokens.</summary>
        public int MaxTokens { get; set; } = 2048;

        /// <summary>Whether to enable LLM thinking mode.</summary>
        public bool EnableThinking { get; set; } = false;

        // --- Convenience presets ---

        /// <summary>Official Qwen3.5 non-thinking for general tasks.</summary>
        public static LlmGenConfig NonThinking => new LlmGenConfig
        {
            Temperature = 0.7, TopP = 0.8, TopK = 20,
            PresencePenalty = 1.5, RepetitionPenalty = 1.05, EnableThinking = false
        };

        /// <summary>Fast-response combat/emergency — low temp, few tokens.</summary>
        public static LlmGenConfig FastResponse => new LlmGenConfig
        {
            Temperature = 0.3, TopP = 0.9, TopK = 10,
            PresencePenalty = 0, RepetitionPenalty = 1.0, EnableThinking = false
        };

        /// <summary>Social/creative — higher temp for variety.</summary>
        public static LlmGenConfig Creative => new LlmGenConfig
        {
            Temperature = 0.9, TopP = 0.95, TopK = 30,
            PresencePenalty = 0.5, RepetitionPenalty = 1.0, EnableThinking = true
        };
    }
}
