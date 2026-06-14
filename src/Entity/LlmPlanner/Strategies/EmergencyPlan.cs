using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameLoop
{
    /// <summary>
    /// Emergency/crisis planning. Minimal prompt (128 tokens), survival
    /// actions only, per-tick replan. No further fallback (rule default).
    /// </summary>
    public class EmergencyPlan : IPlanStrategy
    {
        public PlanType Type => PlanType.Emergency;
        public int MaxTokens => 128;
        public int CooldownTicks => 1;
        public int MaxActions => 4;
        public PlanType? FallbackType => null; // last resort → rule fallback
        public LlmGenConfig GenConfig => LlmGenConfig.FastResponse;

        public string BuildSystemPrompt(PlanContext ctx, GameEntity entity)
        {
            return $"你是{entity.Str("name")}。你正处于危机之中！" +
                   "你的生命或安全受到严重威胁，必须立即行动保命。" +
                   "你必须通过调用函数来执行紧急动作。";
        }

        public string BuildUserPrompt(PlanContext ctx, GameEntity entity, CollectResult collected)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"【紧急】{ctx.TriggerContext}");
            sb.AppendLine();
            foreach (var t in collected.SemanticTexts)
                sb.AppendLine($"- {t}");
            return sb.ToString();
        }

        public List<object> BuildTools(List<ActionSchema> allActions, PlanContext ctx)
        {
            var survivalActions = allActions
                .Where(a => IsSurvivalAction(a.ActionType))
                .Take(MaxActions)
                .ToList();
            if (survivalActions.Count == 0)
                survivalActions = allActions.Take(MaxActions).ToList();
            return PlanToolBuilder.Build(survivalActions);
        }

        private static bool IsSurvivalAction(string name)
        {
            return name switch
            {
                "flee" or "heal" or "hide" or "call_help" or "rest"
                    or "retreat" or "use_potion" or "surrender" => true,
                _ => name.Contains("heal") || name.Contains("flee") || name.Contains("hide"),
            };
        }
    }
}
