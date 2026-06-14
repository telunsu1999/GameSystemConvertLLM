using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameLoop
{
    /// <summary>
    /// Daily routine planning. Standard prompt with full state + goals + all actions.
    /// Used for scheduled time-block planning (morning/noon/evening).
    /// </summary>
    public class DailyPlan : IPlanStrategy
    {
        public PlanType Type => PlanType.Daily;
        public int MaxTokens => 2048;
        public int CooldownTicks => 24;  // 4 game hours
        public int MaxActions => 0; // all
        public PlanType? FallbackType => null; // no downgrade; goes to rule fallback
        public LlmGenConfig GenConfig => LlmGenConfig.NonThinking;

        public string BuildSystemPrompt(PlanContext ctx, GameEntity entity)
        {
            var personality = entity.Str("personality");
            return $"你是{entity.Str("name")}（{personality}）。" +
                "你的任务是为今天规划4-5个行动。\n\n" +
                "重要规则：\n" +
                "1. 如果需要去别的地方做某事（如去商店找人、去酒馆喝酒），必须先调用goto移动到该地点\n" +
                "  示例：想和老雷交谈 → 先 goto(商店) 再 talk(老雷)\n" +
                "  示例：想去喝酒 → 先 goto(酒馆) 再 drink\n" +
                "2. 每天4个时段：早晨(H6-12)、下午(H12-18)、傍晚(H18-24)、深夜(H24-6)\n" +
                "3. 每个时段调用1-2个函数（如果要移动就先goto再做事）\n" +
                "4. 直接输出函数调用，禁止输出任何分析、解释、<think>标签\n" +
                "5. 严格按照 <function=函数名>参数</function> 格式";
        }

        public string BuildUserPrompt(PlanContext ctx, GameEntity entity, CollectResult collected)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"时间: {ctx.Season} D{ctx.Day} {ctx.TimeBlock} H{ctx.Hour}");
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

            sb.AppendLine("请规划今日每个时段的行动（直接输出函数调用）：");
            sb.AppendLine("早晨(H6-12)");
            sb.AppendLine("下午(H12-18)");
            sb.AppendLine("傍晚(H18-24)");
            sb.AppendLine("深夜(H24-6)");
            sb.AppendLine();
            sb.AppendLine("主要地点：铁匠铺、酒馆、商店、城门、集市、神殿");
            sb.AppendLine("注意：需要去特定地点才能做的事，必须先调用goto(地点)再做事。不要输出任何解释文字。");
            return sb.ToString();
        }

        public virtual List<object> BuildTools(List<ActionSchema> allActions, PlanContext ctx)
        {
            var actions = MaxActions > 0 ? allActions.Take(MaxActions).ToList() : allActions;
            return PlanToolBuilder.Build(actions);
        }
    }
}
