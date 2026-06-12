using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace GameLoop
{
    /// <summary>
    /// Entity component that generates multi-step plans via LLM.
    /// OnTick: evaluates triggers, calls LLM if needed, parses JSON plan,
    /// schedules steps into the entity's Scheduler.
    /// </summary>
    public class LlmPlanner : IEntityComponent
    {
        private readonly LLMClient _client;
        private readonly string _rootDir;
        private int _lastLlmHour = -1;

        public LlmPlanner(LLMClient client, string rootDir)
        {
            _client = client;
            _rootDir = rootDir;
        }

        public void OnTick(TickContext ctx)
        {
            if (!ctx.LlmEnabled) return;
            if (ctx.Hour == _lastLlmHour) return;
            _lastLlmHour = ctx.Hour;

            var entity = ctx.Entity as GameEntity;
            if (entity == null) return;
            var triggers = entity.Get<TriggerSystem>();
            var attrs = entity.Get<Attributes>();
            var resolver = entity.Get<ActionResolver>();
            var sched = entity.Get<Scheduler>();
            if (triggers == null || attrs == null || resolver == null || sched == null) return;

            var result = triggers.Evaluate("time", "hour", attrs);
            if (result.LlmOptions.Count == 0) return;

            var context = result.LlmOptions[0].Description;
            Logger.Info("LLM", $"Plan needed: {entity.Id}", new { context, tick = ctx.Tick });

            // Collect semantic data
            var events = ctx.Events as EventSystem;
            var semantic = new SemanticEngine();
            var collector = new DataCollector(attrs, events, semantic,
                System.IO.Path.Combine(_rootDir, "configs", "collect"),
                System.IO.Path.Combine(_rootDir, "configs", "semantic"));
            var collected = collector.Collect(entity.Id, new CollectConfig
            {
                AttrTags = new[] { "identity", "vital", "world", "currency", "trade" },
                EventTags = new[] { "social", "combat", "travel" },
                RuleFiles = new[] { "npc_state", "npc_danger", "social", "adventure_memory" },
                EventDays = 7, EventLimit = 5
            });

            // Build prompt
            var actions = resolver.GetAvailableActions();
            var sb = new StringBuilder();
            sb.AppendLine($"You are {entity.Str("name")}, personality: {entity.Str("personality")}");
            sb.AppendLine($"Current: {ctx.Season} D{ctx.Day} {ctx.TimeBlock} H{ctx.Hour}");
            sb.AppendLine();
            foreach (var t in collected.SemanticTexts) sb.AppendLine($"- {t}");
            sb.AppendLine();
            sb.AppendLine($"It is {context}, make a plan for the next few hours.");
            sb.AppendLine();
            sb.AppendLine("Available actions:");
            for (int i = 0; i < actions.Count; i++)
            {
                var a = actions[i];
                sb.Append($"  {a.ActionType}");
                if (a.Params.Count > 0)
                    sb.Append($"({string.Join(",", a.Params.Select(p => $"{p.Name}:{p.Type}"))})");
                sb.AppendLine($" - {a.Description} -> {a.Effect}");
            }
            sb.AppendLine();
            sb.AppendLine("Output a JSON plan (each step 1 hour apart):");
            sb.AppendLine("{\"plan\":[{\"action\":\"eat\"},{\"action\":\"work\",\"params\":{\"income\":10}}]}");
            sb.AppendLine("Only output JSON.");
            var prompt = sb.ToString();

            // Call LLM
            string response;
            try
            {
                response = _client.SendAsync(prompt, maxTokens: 512).Result;
                Logger.Info("LLM", $"Plan response: {entity.Id}", new { response = response.Trim() });
            }
            catch
            {
                Logger.Warn("LLM", $"Plan failed: {entity.Id}");
                return;
            }

            // Parse JSON plan
            var clean = System.Text.RegularExpressions.Regex.Replace(response, "<think>.*?</think>", "",
                System.Text.RegularExpressions.RegexOptions.Singleline).Trim();
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(clean, @"\{[\s\S]*""plan""[\s\S]*\}");
            if (!jsonMatch.Success) { Logger.Warn("LLM", $"Plan parse failed: {entity.Id}"); return; }

            try
            {
                var plan = JsonSerializer.Deserialize<LlmPlan>(jsonMatch.Value,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (plan?.Plan == null || plan.Plan.Count == 0) return;

                for (int i = 0; i < plan.Plan.Count; i++)
                {
                    var step = plan.Plan[i];
                    var atTick = ctx.Tick + (i + 1) * 6;
                    sched.Add(step.Action, new[] { "llm_plan" },
                        step.Params ?? new Dictionary<string, object>(), atTick: atTick);
                }
                Logger.Info("LLM", $"Plan scheduled: {entity.Id}",
                    new { steps = plan.Plan.Select((s, i) => $"T{ctx.Tick + (i + 1) * 6}:{s.Action}").ToList() });
            }
            catch (Exception ex)
            {
                Logger.Warn("LLM", $"Plan JSON error: {entity.Id}", new { error = ex.Message });
            }
        }
    }

    class LlmPlan { public List<LlmPlanStep> Plan { get; set; } = new List<LlmPlanStep>(); }
    class LlmPlanStep { public string Action { get; set; } = ""; public Dictionary<string, object> Params { get; set; } }
}
