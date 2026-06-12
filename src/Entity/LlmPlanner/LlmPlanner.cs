using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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
        private readonly Action<LlmSession> _onSession;
        private int _lastLlmHour = -1;
        private bool _busy;

        public LlmPlanner(LLMClient client, string rootDir, Action<LlmSession> onSession = null)
        {
            _client = client;
            _rootDir = rootDir;
            _onSession = onSession;
        }

        public void OnTick(TickContext ctx)
        {
            if (!ctx.LlmEnabled || _busy) return;
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
            var capturedTick = ctx.Tick;
            var capturedHour = ctx.Hour;
            var capturedSeason = ctx.Season;
            var capturedDay = ctx.Day;
            var capturedBlock = ctx.TimeBlock;

            // Fire-and-forget: set thinking, call LLM async, clear thinking, schedule plan
            _busy = true;
            attrs.Set("llm_thinking", 1, "number", new[] { "meta" });
            Logger.Info("LLM", $"Plan started: {entity.Id}", new { context, tick = capturedTick });

            _ = RunPlanAsync(entity, attrs, resolver, sched, ctx.Events as EventSystem,
                context, capturedTick, capturedHour, capturedSeason, capturedDay, capturedBlock);
        }

        private async Task RunPlanAsync(GameEntity entity, Attributes attrs, ActionResolver resolver,
            Scheduler sched, EventSystem events, string context,
            int tick, int hour, string season, int day, string block)
        {
            try
            {
                // Collect semantic data
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
                sb.AppendLine($"Current: {season} D{day} {block} H{hour}");
                sb.AppendLine();
                foreach (var t in collected.SemanticTexts) sb.AppendLine($"- {t}");
                sb.AppendLine();

                // Inject goal context
                var goals = entity.Get<GoalComponent>();
                if (goals != null)
                {
                    var goalCtx = goals.GetGoalContext();
                    if (!string.IsNullOrEmpty(goalCtx))
                        sb.AppendLine(goalCtx);
                }

                sb.AppendLine($"It is {context}, make a plan aligned with your goals for the next few hours.");
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

                // Call LLM asynchronously
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var response = await _client.SendAsync(prompt, maxTokens: 1024, enableThinking: true);
                sw.Stop();
                Logger.Info("LLM", $"Plan response: {entity.Id}",
                    new { response = response.Trim(), latencyMs = sw.ElapsedMilliseconds });

                // Parse JSON plan
                var clean = System.Text.RegularExpressions.Regex.Replace(response, "<think>.*?</think>", "",
                    System.Text.RegularExpressions.RegexOptions.Singleline).Trim();
                var jsonMatch = System.Text.RegularExpressions.Regex.Match(clean, @"\{[\s\S]*""plan""[\s\S]*\}");
                if (!jsonMatch.Success) { Logger.Warn("LLM", $"Plan parse failed: {entity.Id}"); return; }

                var plan = JsonSerializer.Deserialize<LlmPlan>(jsonMatch.Value,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (plan?.Plan == null || plan.Plan.Count == 0) return;

                for (int i = 0; i < plan.Plan.Count; i++)
                {
                    var step = plan.Plan[i];
                    var atTick = tick + (i + 1) * 6;
                    sched.Add(step.Action, new[] { "llm_plan" },
                        step.Params ?? new Dictionary<string, object>(), atTick: atTick);
                }
                Logger.Info("LLM", $"Plan scheduled: {entity.Id}",
                    new { steps = plan.Plan.Select((s, i) => $"T{tick + (i + 1) * 6}:{s.Action}").ToList() });

                _onSession?.Invoke(new LlmSession
                {
                    NpcId = entity.Id,
                    Tick = tick,
                    Prompt = prompt,
                    Response = response.Trim(),
                    Latency = (int)sw.ElapsedMilliseconds,
                    Steps = plan.Plan.Select((s, i) => new LlmPlanResult
                        { Tick = tick + (i + 1) * 6, Action = s.Action }).ToList()
                });
            }
            catch (Exception ex)
            {
                Logger.Warn("LLM", $"Plan failed: {entity.Id}", new { error = ex.Message });
                _onSession?.Invoke(new LlmSession
                {
                    NpcId = entity.Id, Tick = tick, Prompt = "",
                    Response = ex.Message, Error = true
                });
            }
            finally
            {
                attrs.Set("llm_thinking", 0, "number", new[] { "meta" });
                _busy = false;
            }
        }
    }

    class LlmPlan { public List<LlmPlanStep> Plan { get; set; } = new List<LlmPlanStep>(); }
    class LlmPlanStep { public string Action { get; set; } = ""; public Dictionary<string, object> Params { get; set; } }

    public class LlmSession
    {
        public string NpcId { get; set; }
        public int Tick { get; set; }
        public string Prompt { get; set; }
        public string Response { get; set; }
        public int Latency { get; set; }
        public bool Error { get; set; }
        public List<LlmPlanResult> Steps { get; set; } = new List<LlmPlanResult>();
    }

    public class LlmPlanResult
    {
        public int Tick { get; set; }
        public string Action { get; set; }
    }
}
