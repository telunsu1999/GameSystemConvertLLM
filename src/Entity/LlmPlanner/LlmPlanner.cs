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
    /// OnTick: evaluates triggers → ComplexityEvaluator selects PlanType →
    /// corresponding IPlanStrategy builds prompt/tools → LLM → parse → schedule.
    /// </summary>
    public class LlmPlanner : IEntityComponent
    {
        private readonly LLMClient _client;
        private readonly string _rootDir;
        private readonly Action<LlmSession> _onSession;
        private readonly ComplexityEvaluator _evaluator = new();
        private readonly Dictionary<PlanType, IPlanStrategy> _strategies;
        private int _lastPlanTick = -1;
        private bool _busy;

        public LlmPlanner(LLMClient client, string rootDir, Action<LlmSession> onSession = null)
        {
            _client = client;
            _rootDir = rootDir;
            _onSession = onSession;
            _strategies = new Dictionary<PlanType, IPlanStrategy>
            {
                [PlanType.Daily]     = new DailyPlan(),
                [PlanType.Combat]    = new CombatPlan(),
                [PlanType.Social]    = new SocialPlan(),
                [PlanType.Emergency] = new EmergencyPlan(),
            };
        }

        public void OnTick(TickContext ctx)
        {
            if (!ctx.LlmEnabled || _busy) return;

            var entity = ctx.Entity as GameEntity;
            if (entity == null) return;
            var triggers = entity.Get<TriggerSystem>();
            var attrs = entity.Get<Attributes>();
            var resolver = entity.Get<ActionResolver>();
            var sched = entity.Get<Scheduler>();
            if (triggers == null || attrs == null || resolver == null || sched == null) return;

            var result = triggers.Evaluate("time", "hour", attrs);
            if (result.LlmOptions.Count == 0) return;

            // --- Complexity evaluation → select strategy ---
            var planCtx = _evaluator.Evaluate(entity, result, ctx);
            if (!_strategies.TryGetValue(planCtx.Type, out var strategy)) return;

            // --- Safety cooldown ---
            if (ctx.Tick - _lastPlanTick < strategy.CooldownTicks) return;

            _lastPlanTick = ctx.Tick;
            var capturedTick = ctx.Tick;

            // Fire-and-forget
            _busy = true;
            attrs.Set("llm_thinking", 1, "number", new[] { "meta" });
            Logger.Info("LLM", $"Plan started: {entity.Id}",
                new { type = planCtx.Type.ToString(), urgency = planCtx.Urgency, tick = capturedTick });

            _ = RunPlanAsync(entity, strategy, planCtx, attrs, resolver, sched, capturedTick);
        }

        private async Task RunPlanAsync(
            GameEntity entity, IPlanStrategy strategy, PlanContext planCtx,
            Attributes attrs, ActionResolver resolver, Scheduler sched, int tick)
        {
            var sysPrompt = "";
            var userPrompt = "";
            try
            {
                // --- Collect data ---
                var records = entity.Get<RecordModule>();
                var semantic = new SemanticEngine();
                var collector = new DataCollector(attrs, records, semantic,
                    System.IO.Path.Combine(_rootDir, "configs", "collect"),
                    System.IO.Path.Combine(_rootDir, "configs", "semantic"));
                var collected = collector.Collect(entity.Id, new CollectConfig
                {
                    AttrTags = new[] { "identity", "vital", "world", "currency", "trade" },
                    EventTags = new[] { "social", "combat", "travel" },
                    RuleFiles = new[] { "npc_state", "npc_danger", "social", "adventure_memory" },
                    EventDays = 7, EventLimit = 5
                });

                // --- Build prompt via strategy ---
                var actions = resolver.GetAvailableActions();
                sysPrompt  = strategy.BuildSystemPrompt(planCtx, entity);
                userPrompt = strategy.BuildUserPrompt(planCtx, entity, collected);
                var tools  = strategy.BuildTools(actions, planCtx);

                // --- Call LLM ---
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var genCfg = strategy.GenConfig;
                genCfg.MaxTokens = strategy.MaxTokens;
                var rawResponse = await _client.SendWithToolsAsync(
                    sysPrompt, userPrompt, tools, genCfg);
                sw.Stop();

                // --- Parse response ---
                var response = rawResponse.Trim();
                Logger.Info("LLM", $"Plan response: {entity.Id}",
                    new { type = planCtx.Type.ToString(), response, latencyMs = sw.ElapsedMilliseconds });

                var planSteps = ParseResponse(response);
                if (planSteps.Count == 0)
                {
                    Logger.Warn("LLM", $"Plan parse failed: {entity.Id}", new { response });
                    await TryFallback(entity, strategy, planCtx, attrs, resolver, sched, tick);
                    return;
                }

                // --- Schedule steps ---
                for (int i = 0; i < planSteps.Count; i++)
                {
                    var (action, pars) = planSteps[i];
                    var atTick = tick + (i + 1) * 6;
                    sched.Add(action, new[] { "llm_plan" }, pars, atTick: atTick);
                }
                Logger.Info("LLM", $"Plan scheduled: {entity.Id}",
                    new { type = planCtx.Type.ToString(),
                        steps = planSteps.Select((s, i) => $"T{tick + (i + 1) * 6}:{s.action}").ToList() });

                _onSession?.Invoke(new LlmSession
                {
                    NpcId = entity.Id, Tick = tick,
                    Prompt = $"[system] {sysPrompt}\n[user] {userPrompt}",
                    Response = rawResponse.Trim(),
                    Latency = (int)sw.ElapsedMilliseconds,
                    ToolsJson = JsonSerializer.Serialize(tools),
                    Steps = planSteps.Select((s, i) => new LlmPlanResult
                        { Tick = tick + (i + 1) * 6, Action = s.action }).ToList()
                });
            }
            catch (Exception ex)
            {
                Logger.Warn("LLM", $"Plan failed: {entity.Id}", new { error = ex.Message });
                await TryFallback(entity, strategy, planCtx, attrs, resolver, sched, tick);
            }
            finally
            {
                attrs.Set("llm_thinking", 0, "number", new[] { "meta" });
                _busy = false;
            }
        }

        /// <summary>
        /// Fallback chain: try the strategy's FallbackType, then rule default.
        /// </summary>
        private async Task TryFallback(
            GameEntity entity, IPlanStrategy failedStrategy, PlanContext planCtx,
            Attributes attrs, ActionResolver resolver, Scheduler sched, int tick)
        {
            if (failedStrategy.FallbackType != null &&
                _strategies.TryGetValue(failedStrategy.FallbackType.Value, out var fallback))
            {
                Logger.Info("LLM", $"Plan fallback: {entity.Id} {failedStrategy.Type}→{fallback.Type}");
                planCtx.Type = fallback.Type;
                await RunPlanAsync(entity, fallback, planCtx, attrs, resolver, sched, tick);
                return;
            }

            // Rule fallback: idle
            Logger.Warn("LLM", $"Plan rule fallback: {entity.Id} → idle");
            _onSession?.Invoke(new LlmSession
            {
                NpcId = entity.Id, Tick = tick, Prompt = "",
                Response = $"fallback from {failedStrategy.Type}", Error = true
            });
        }

        // ================================================================
        // Response parsing (shared)
        // ================================================================

        private static List<(string action, Dictionary<string, object> pars)> ParseResponse(string response)
        {
            var steps = new List<(string action, Dictionary<string, object> pars)>();

            // Parse <function=NAME>...</function> from tool_call output
            var toolCalls = System.Text.RegularExpressions.Regex.Matches(
                response, @"<function=([^>]+)>(.*?)</function>",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            if (toolCalls.Count > 0)
            {
                foreach (System.Text.RegularExpressions.Match tc in toolCalls)
                {
                    var actionName = tc.Groups[1].Value.Trim();
                    var inner = tc.Groups[2].Value;
                    var pars = new Dictionary<string, object>();
                    var paramMatches = System.Text.RegularExpressions.Regex.Matches(
                        inner, @"<parameter=([^>]+)>\s*(.*?)\s*</parameter>",
                        System.Text.RegularExpressions.RegexOptions.Singleline);
                    foreach (System.Text.RegularExpressions.Match pm in paramMatches)
                    {
                        var pname = pm.Groups[1].Value.Trim();
                        var pval = pm.Groups[2].Value.Trim();
                        pars[pname] = int.TryParse(pval, out var iv) ? (object)iv : pval;
                    }
                    steps.Add((actionName, pars));
                }
                return steps;
            }

            // Fallback: parse JSON {"plan":[...]} format
            var clean = System.Text.RegularExpressions.Regex.Replace(
                response, "<think>.*?</think>", "",
                System.Text.RegularExpressions.RegexOptions.Singleline).Trim();
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(
                clean, @"\{[\s\S]*""plan""[\s\S]*\}");
            if (jsonMatch.Success)
            {
                var plan = JsonSerializer.Deserialize<LlmPlan>(jsonMatch.Value,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (plan?.Plan != null)
                {
                    foreach (var s in plan.Plan)
                        steps.Add((s.Action, s.Params ?? new Dictionary<string, object>()));
                }
            }

            return steps;
        }
    }

    // ================================================================
    // Data types
    // ================================================================

    class LlmPlan { public List<LlmPlanStep> Plan { get; set; } = new List<LlmPlanStep>(); }
    class LlmPlanStep { public string Action { get; set; } = ""; public Dictionary<string, object> Params { get; set; } }

    public class LlmSession
    {
        public string NpcId { get; set; }
        public int Tick { get; set; }
        public string Prompt { get; set; }
        public string Response { get; set; }
        public string ToolsJson { get; set; }
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
