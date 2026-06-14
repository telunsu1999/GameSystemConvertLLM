using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameLoop
{
    /// <summary>
    /// Social interaction planning. Focuses on social actions, includes
    /// relationship context. Falls back to DailyPlan on failure.
    /// </summary>
    public class SocialPlan : IPlanStrategy
    {
        public PlanType Type => PlanType.Social;
        public int MaxTokens => 512;
        public int CooldownTicks => 3;
        public int MaxActions => 6;
        public PlanType? FallbackType => PlanType.Daily;
        public LlmGenConfig GenConfig => LlmGenConfig.Creative;

        public string BuildSystemPrompt(PlanContext ctx, GameEntity entity)
        {
            return $"你是{entity.Str("name")}，一个{entity.Str("personality")}。" +
                   "你正在与他人互动。请根据你们的关系和当前目标，选择合适的社交行为。" +
                   "你必须通过调用函数来执行社交动作。";
        }

        public string BuildUserPrompt(PlanContext ctx, GameEntity entity, CollectResult collected)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"【社交】{ctx.TriggerContext}");
            sb.AppendLine();

            foreach (var t in collected.SemanticTexts)
                sb.AppendLine($"- {t}");
            sb.AppendLine();

            var goals = entity.Get<GoalComponent>();
            if (goals != null)
            {
                var goalCtx = goals.GetGoalContext();
                if (!string.IsNullOrEmpty(goalCtx))
                    sb.AppendLine(goalCtx);
            }

            return sb.ToString();
        }

        public List<object> BuildTools(List<ActionSchema> allActions, PlanContext ctx)
        {
            var socialActions = allActions
                .Where(a => IsSocialAction(a.ActionType))
                .Take(MaxActions)
                .ToList();
            if (socialActions.Count == 0)
                socialActions = allActions.Take(MaxActions).ToList();
            return PlanToolBuilder.Build(socialActions);
        }

        private static bool IsSocialAction(string name)
        {
            return name switch
            {
                "talk" or "gift" or "trade" or "propose" or "greet" or "insult"
                    or "chat" or "flirt" or "bargain" or "comfort" => true,
                _ => name.StartsWith("social_") || name.Contains("talk"),
            };
        }
    }
}
