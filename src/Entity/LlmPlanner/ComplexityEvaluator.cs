using System.Collections.Generic;
using System.Linq;

namespace GameLoop
{
    /// <summary>
    /// Evaluates the current context for an NPC and selects the appropriate
    /// PlanType + urgency level. Currently driven by trigger signal tags;
    /// long-term goal count adds future extensibility.
    /// </summary>
    public class ComplexityEvaluator
    {
        public PlanContext Evaluate(GameEntity entity, TriggerResult triggerResult, TickContext tickCtx)
        {
            var goals = entity.Get<GoalComponent>();
            var attrs = entity.Get<Attributes>();

            var longTerm = goals?.GetActiveGoals()
                .Where(g => g.Type == GoalType.LongTerm).ToList() ?? new List<Goal>();
            var shortTerm = goals?.GetActiveGoals()
                .Where(g => g.Type == GoalType.ShortTerm).ToList() ?? new List<Goal>();

            // Determine trigger signal from the first LlmOption's tags
            var signal = "time";
            if (triggerResult.LlmOptions.Count > 0)
            {
                var tags = triggerResult.LlmOptions[0].Tags;
                if (tags != null)
                {
                    if (tags.Contains("combat")) signal = "combat";
                    else if (tags.Contains("danger")) signal = "danger";
                    else if (tags.Contains("social")) signal = "social";
                }
            }

            // Select plan type based on signal
            PlanType planType = signal switch
            {
                "combat" => PlanType.Combat,
                "danger" => PlanType.Emergency,
                "social" => PlanType.Social,
                _ => PlanType.Daily
            };

            // Urgency: combat > danger > social > time
            int urgency = signal switch
            {
                "combat" => 10,
                "danger" => 8,
                "social" => 5,
                _ => 1
            };

            return new PlanContext
            {
                Type = planType,
                Urgency = urgency,
                LongTermGoals = longTerm,
                ShortTermGoals = shortTerm,
                TriggerContext = triggerResult.LlmOptions.Count > 0
                    ? triggerResult.LlmOptions[0].Description : "",
                TriggerSignal = signal,
                Tick = tickCtx.Tick,
                Hour = tickCtx.Hour,
                Day = tickCtx.Day,
                Season = tickCtx.Season,
                TimeBlock = tickCtx.TimeBlock
            };
        }
    }
}
