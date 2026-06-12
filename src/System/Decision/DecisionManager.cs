using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GameLoop
{
    public class DecisionManager
    {
        private readonly IDataCollector _collector;
        private readonly IPromptTemplate _prompt;
        private readonly ILLMClient _llm;
        private readonly IActionResolver _resolver;
        private readonly List<DecisionRecord> _history = new List<DecisionRecord>();
        private readonly int _maxRetries;
        private readonly int _retryDelayMs;

        public DecisionManager(IDataCollector collector,
                               IPromptTemplate prompt,
                               ILLMClient llm,
                               IActionResolver resolver,
                               int maxRetries = 2,
                               int retryDelayMs = 500)
        {
            _collector = collector;
            _prompt = prompt;
            _llm = llm;
            _resolver = resolver;
            _maxRetries = maxRetries;
            _retryDelayMs = retryDelayMs;
        }

        public async Task<List<DecisionRecord>> ProcessAsync(
            string npcId, TriggerResult triggerResult, Attributes attrs,
            EventSystem events, Scheduler scheduler, TickSnapshot snap,
            string collectConfig = null, string fallbackActionType = null)
        {
            var records = new List<DecisionRecord>();

            foreach (var rule in triggerResult.DirectActions)
                ExecuteDirect(rule, attrs, events, scheduler, snap, records);

            if (triggerResult.LlmOptions.Count > 0)
            {
                var record = await ProcessLlmOptionsAsync(npcId, triggerResult.LlmOptions,
                    attrs, events, scheduler, snap, collectConfig, fallbackActionType);
                if (record != null) records.Add(record);
            }

            _history.AddRange(records);
            return records;
        }

        public List<DecisionRecord> ProcessSchedule(List<ScheduleItem> dueItems,
            Attributes attrs, EventSystem events, Scheduler scheduler, TickSnapshot snap)
        {
            var records = new List<DecisionRecord>();
            foreach (var item in dueItems)
                ExecuteDirect(item, attrs, events, scheduler, snap, records);
            _history.AddRange(records);
            return records;
        }

        private void ExecuteDirect(TriggerRule rule, Attributes attrs,
            EventSystem events, Scheduler scheduler, TickSnapshot snap, List<DecisionRecord> records)
        {
            _resolver.Execute(new ActionItem { ActionType = rule.ActionType,
                Params = rule.Params ?? new Dictionary<string, object>() }, attrs, events, scheduler, snap);
            records.Add(new DecisionRecord { Source = "trigger:direct", ActionType = rule.ActionType, Method = "direct", Tick = snap.Tick });
        }

        private void ExecuteDirect(ScheduleItem item, Attributes attrs,
            EventSystem events, Scheduler scheduler, TickSnapshot snap, List<DecisionRecord> records)
        {
            _resolver.Execute(new ActionItem { ActionType = item.ActionType,
                Params = item.Params ?? new Dictionary<string, object>() }, attrs, events, scheduler, snap);
            records.Add(new DecisionRecord { Source = "scheduler", ActionType = item.ActionType, Method = "direct", Tick = snap.Tick });
        }

        private async Task<DecisionRecord> ProcessLlmOptionsAsync(string npcId, List<TriggerRule> options,
            Attributes attrs, EventSystem events, Scheduler scheduler,
            TickSnapshot snap, string collectConfig, string fallbackActionType)
        {
            var collected = _collector.Collect(npcId, collectConfig ?? "default");
            if (collected == null) return RunFallback(fallbackActionType, attrs, events, scheduler, snap);

            var optionTexts = new List<string>();
            for (int i = 0; i < options.Count; i++)
                optionTexts.Add($"{i + 1}. {options[i].Description}");

            var prompt = _prompt.Build(attrs, collected, "玩家来到了你身边", optionTexts);

            string response = null;
            for (int attempt = 0; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    response = await _llm.SendAsync(prompt, maxTokens: 8);
                    break;
                }
                catch
                {
                    if (attempt == _maxRetries)
                        return RunFallback(fallbackActionType, attrs, events, scheduler, snap);
                    await Task.Delay(_retryDelayMs * (attempt + 1));
                }
            }

            if (response == null || !TryParseChoice(response, options.Count, out int idx))
                return RunFallback(fallbackActionType, attrs, events, scheduler, snap);

            var chosen = options[idx];
            _resolver.Execute(new ActionItem { ActionType = chosen.ActionType,
                Params = chosen.Params ?? new Dictionary<string, object>() }, attrs, events, scheduler, snap);

            return new DecisionRecord { Source = "trigger:llm", ActionType = chosen.ActionType, Method = "llm", LlmResponse = response, Tick = snap.Tick };
        }

        private DecisionRecord RunFallback(string fallbackActionType, Attributes attrs,
            EventSystem events, Scheduler scheduler, TickSnapshot snap)
        {
            if (string.IsNullOrEmpty(fallbackActionType)) return null;
            try { _resolver.Execute(new ActionItem { ActionType = fallbackActionType,
                Params = new Dictionary<string, object>() }, attrs, events, scheduler, snap); } catch { }
            return new DecisionRecord { Source = "fallback", ActionType = fallbackActionType, Method = "fallback", Tick = snap.Tick };
        }

        private bool TryParseChoice(string response, int count, out int index)
        {
            index = 0; response = response.Trim();
            if (int.TryParse(response, out int n) && n >= 1 && n <= count) { index = n - 1; return true; }
            foreach (char c in response)
                if (char.IsDigit(c)) { int d = c - '0'; if (d >= 1 && d <= count) { index = d - 1; return true; } }
            return false;
        }

        public List<DecisionRecord> GetHistory(int lastN = 20)
            => _history.Skip(Math.Max(0, _history.Count - lastN)).ToList();
    }

    public class DecisionRecord
    {
        public string Source;
        public string ActionType;
        public string Method;
        public string LlmResponse;
        public int Tick;
    }
}
