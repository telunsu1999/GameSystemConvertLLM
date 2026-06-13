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

            _ = RunPlanAsync(entity, attrs, resolver, sched,
                context, capturedTick, capturedHour, capturedSeason, capturedDay, capturedBlock);
        }

        private async Task RunPlanAsync(GameEntity entity, Attributes attrs, ActionResolver resolver,
            Scheduler sched, string context,
            int tick, int hour, string season, int day, string block)
        {
            try
            {
                // Collect semantic data from entity's own RecordModule
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

                // Build system prompt (identity only) and user prompt (state+goals)
                var actions = resolver.GetAvailableActions();

                // --- System: short identity + output instruction ---
                var sysPrompt = $"你是{entity.Str("name")}，一个{entity.Str("personality")}。你必须通过调用函数来制定计划。";

                // --- User: dynamic state + goals ---
                var sb = new StringBuilder();
                sb.AppendLine($"当前时间: {season} D{day} {block} H{hour}");
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

                sb.AppendLine($"当前是{context}，请制定接下来几小时的日程计划。");
                var userPrompt = sb.ToString();

                // --- Tools: actions as function definitions ---
                var tools = new List<object>();
                for (int i = 0; i < actions.Count; i++)
                {
                    var a = actions[i];
                    var func = new Dictionary<string, object>
                    {
                        ["name"] = a.ActionType,
                        ["description"] = $"{a.Description}。效果: {a.Effect}",
                    };
                    if (a.Params.Count > 0)
                    {
                        var props = new Dictionary<string, object>();
                        var required = new List<string>();
                        foreach (var p in a.Params)
                        {
                            props[p.Name] = new Dictionary<string, object>
                            {
                                ["type"] = p.Type switch { "number" => "integer", _ => "string" },
                                ["description"] = p.Name,
                            };
                            required.Add(p.Name);
                        }
                        func["parameters"] = new Dictionary<string, object>
                        {
                            ["type"] = "object",
                            ["properties"] = props,
                            ["required"] = required,
                        };
                    }
                    tools.Add(new Dictionary<string, object> { ["type"] = "function", ["function"] = func });
                }

                // Call LLM with tools (no thinking needed for 4B)
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var rawResponse = await _client.SendWithToolsAsync(
                    sysPrompt, userPrompt, tools,
                    maxTokens: 1024, enableThinking: false);
                sw.Stop();

                // --- Parse response: try tool_call XML first, then JSON plan ---
                var response = rawResponse.Trim();
                Logger.Info("LLM", $"Plan response: {entity.Id}",
                    new { response = response, latencyMs = sw.ElapsedMilliseconds });

                var planSteps = new List<(string action, Dictionary<string, object> pars)>();

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
                        planSteps.Add((actionName, pars));
                    }
                }

                // Fallback: parse JSON {"plan":[...]} format
                if (planSteps.Count == 0)
                {
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
                                planSteps.Add((s.Action, s.Params ?? new Dictionary<string, object>()));
                        }
                    }
                }

                if (planSteps.Count == 0)
                {
                    Logger.Warn("LLM", $"Plan parse failed: {entity.Id}", new { response });
                    return;
                }

                for (int i = 0; i < planSteps.Count; i++)
                {
                    var (action, pars) = planSteps[i];
                    var atTick = tick + (i + 1) * 6;
                    sched.Add(action, new[] { "llm_plan" }, pars, atTick: atTick);
                }
                Logger.Info("LLM", $"Plan scheduled: {entity.Id}",
                    new { steps = planSteps.Select((s, i) => $"T{tick + (i + 1) * 6}:{s.action}").ToList() });

                _onSession?.Invoke(new LlmSession
                {
                    NpcId = entity.Id,
                    Tick = tick,
                    Prompt = $"[system] {sysPrompt}\n[user] {userPrompt}",
                    Response = rawResponse.Trim(),
                    Latency = (int)sw.ElapsedMilliseconds,
                    Steps = planSteps.Select((s, i) => new LlmPlanResult
                        { Tick = tick + (i + 1) * 6, Action = s.action }).ToList()
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
