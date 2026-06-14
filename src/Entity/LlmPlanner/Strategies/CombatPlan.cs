using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameLoop
{
    /// <summary>
    /// Combat planning. Fast-response (256 tokens), combat actions only,
    /// per-tick replan allowed. Falls back to EmergencyPlan on failure.
    /// </summary>
    public class CombatPlan : IPlanStrategy
    {
        public PlanType Type => PlanType.Combat;
        public int MaxTokens => 256;
        public int CooldownTicks => 1;
        public int MaxActions => 5;
        public PlanType? FallbackType => PlanType.Emergency;
        public LlmGenConfig GenConfig => LlmGenConfig.FastResponse;

        public string BuildSystemPrompt(PlanContext ctx, GameEntity entity)
        {
            return $"你是{entity.Str("name")}，一个{entity.Str("personality")}。你正处于战斗中！" +
                   "你的首要任务是评估威胁、保护自己和同伴、击退敌人。" +
                   "你必须通过调用函数来执行战斗动作。";
        }

        public string BuildUserPrompt(PlanContext ctx, GameEntity entity, CollectResult collected)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"【战斗】{ctx.TriggerContext}");
            sb.AppendLine();
            foreach (var t in collected.SemanticTexts)
                sb.AppendLine($"- {t}");
            return sb.ToString();
        }

        public List<object> BuildTools(List<ActionSchema> allActions, PlanContext ctx)
        {
            var combatActions = allActions
                .Where(a => IsCombatAction(a.ActionType))
                .Take(MaxActions)
                .ToList();
            // Fallback: if no combat-tagged actions, expose all (capped)
            if (combatActions.Count == 0)
                combatActions = allActions.Take(MaxActions).ToList();
            return PlanToolBuilder.Build(combatActions);
        }

        private static bool IsCombatAction(string name)
        {
            return name switch
            {
                "attack" or "defend" or "flee" or "use_skill" or "guard"
                    or "charge" or "shoot" or "counter" or "taunt" => true,
                _ => name.StartsWith("combat_") || name.Contains("attack") || name.Contains("defend"),
            };
        }
    }
}
