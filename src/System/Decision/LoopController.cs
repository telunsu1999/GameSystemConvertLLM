using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GameLoop
{
    public class LoopController
    {
        private readonly WorldClock _clock;
        private readonly DecisionManager _decisionManager;

        public WorldClock Clock => _clock;
        public DecisionManager Decisions => _decisionManager;

        public LoopController(WorldClock clock, DecisionManager decisionManager)
        {
            _clock = clock;
            _decisionManager = decisionManager;
        }

        public async Task<List<TickResult>> TickAsync(int count,
            Dictionary<string, (Attributes attrs, Scheduler scheduler, TriggerSystem triggers)> npcs,
            Dictionary<string, (Scheduler scheduler, TriggerSystem triggers)> zones = null,
            string collectConfig = null)
        {
            var results = new List<TickResult>();

            for (int t = 0; t < count; t++)
            {
                var oldSnap = _clock.Snapshot();
                _clock.Advance(1);
                var snap = _clock.Snapshot();

                var result = new TickResult { Tick = snap.Tick };

                // Process all NPCs
                foreach (var (npcId, (attrs, sched, triggers)) in npcs)
                {
                    // 1. Scheduler due actions
                    var due = sched.Check(snap);
                    if (due.Count > 0)
                    {
                        var records = _decisionManager.ProcessSchedule(due, attrs, sched, snap);
                        result.Records.AddRange(records);
                    }

                    // 2. TriggerSystem evaluation (on hour change or time block change)
                    if (snap.Hour != oldSnap.Hour || snap.TimeBlock != oldSnap.TimeBlock)
                    {
                        var triggerResult = triggers.Evaluate("time", "hour_changed", attrs);
                        if (triggerResult.DirectActions.Count > 0 || triggerResult.LlmOptions.Count > 0)
                        {
                            var records = await _decisionManager.ProcessAsync(
                                npcId, triggerResult, attrs, sched, snap,
                                collectConfig, "ignore");
                            result.Records.AddRange(records);
                        }
                    }
                }

                // Process zones (spawns etc.)
                if (zones != null)
                {
                    foreach (var (zoneId, (sched, triggers)) in zones)
                    {
                        var due = sched.Check(snap);
                        if (due.Count > 0)
                        {
                            var records = _decisionManager.ProcessSchedule(due, null, sched, snap);
                            result.Records.AddRange(records);
                        }
                    }
                }

                results.Add(result);
            }

            return results;
        }

        public void NotifyAttributeChange(string npcId, string attrName,
            Attributes attrs, Scheduler scheduler, TriggerSystem triggers,
            TickSnapshot snap, string collectConfig = null)
        {
            var triggerResult = triggers.Evaluate("attr", attrName, attrs);
            if (triggerResult.DirectActions.Count > 0 || triggerResult.LlmOptions.Count > 0)
            {
                _ = _decisionManager.ProcessAsync(npcId, triggerResult, attrs, scheduler, snap,
                    collectConfig, "ignore");
            }
        }
    }

    public class TickResult
    {
        public int Tick;
        public List<DecisionRecord> Records = new List<DecisionRecord>();
    }
}
