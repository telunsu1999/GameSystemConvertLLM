using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLoop
{
    public enum GoalType { LongTerm, ShortTerm }

    public class Milestone
    {
        public string Id { get; set; } = "";
        public string Desc { get; set; } = "";
        // Signal mode: matches _signal events
        public string Source { get; set; } = "";
        public int Count { get; set; } = 1;
        public int Current { get; set; }
        // Legacy mode: watches a specific attribute
        public string ConditionAttr { get; set; } = "";
        public string ConditionOp { get; set; } = "gte";
        public long ConditionValue { get; set; }
        public bool Done { get; set; }
    }

    public class Goal
    {
        public string Id { get; set; } = "";
        public GoalType Type { get; set; } = GoalType.ShortTerm;
        public string Desc { get; set; } = "";
        public int Priority { get; set; } = 1;
        public string TargetEntity { get; set; } = "";
        public bool Completed { get; set; }
        public List<Milestone> Milestones { get; set; } = new List<Milestone>();

        /// <summary>First incomplete milestone, or null if all done.</summary>
        public Milestone CurrentMilestone => Milestones.FirstOrDefault(m => !m.Done);

        public float Progress => Milestones.Count == 0 ? 1
            : (float)Milestones.Count(m => m.Done) / Milestones.Count;
    }

    /// <summary>
    /// Manages NPC goals (long-term and short-term).
    /// Tracks milestone progress via Attributes.OnChanged events.
    /// Provides goal context for LlmPlanner prompt generation.
    /// </summary>
    public class GoalComponent : IEntityComponent
    {
        public List<Goal> Goals { get; } = new List<Goal>();

        /// <summary>Fires when a milestone completes.</summary>
        public event Action<Goal, Milestone> OnMilestoneCompleted;

        /// <summary>Fires when an entire goal completes.</summary>
        public event Action<Goal> OnGoalCompleted;

        private Attributes _attrs;

        /// <summary>
        /// Wire to the entity's Attributes. Must be called after both are added to entity.
        /// </summary>
        public void Wire(Attributes attrs)
        {
            _attrs = attrs;
            attrs.OnChanged += CheckMilestones;
        }

        public void OnTick(TickContext ctx)
        {
            // Progress is event-driven, nothing to poll
        }

        // ===== Public API =====

        public void AddGoal(Goal goal)
        {
            Goals.Add(goal);
            Logger.Debug("Goal", $"Added: {goal.Id}", new { type = goal.Type, desc = goal.Desc });
        }

        public bool RemoveGoal(string id)
        {
            var g = Goals.FirstOrDefault(g => g.Id == id);
            if (g == null) return false;
            Goals.Remove(g);
            Logger.Debug("Goal", $"Removed: {id}");
            return true;
        }

        public void CompleteGoal(string id)
        {
            var g = Goals.FirstOrDefault(g => g.Id == id);
            if (g == null) return;
            g.Completed = true;
            OnGoalCompleted?.Invoke(g);
            Logger.Debug("Goal", $"Completed: {id}", new { desc = g.Desc });
        }

        /// <summary>Get all active (incomplete) goals, sorted by priority desc.</summary>
        public List<Goal> GetActiveGoals()
        {
            return Goals.Where(g => !g.Completed).OrderByDescending(g => g.Priority).ToList();
        }

        /// <summary>
        /// Generate context text for LlmPlanner prompt.
        /// </summary>
        public string GetGoalContext()
        {
            var active = GetActiveGoals();
            if (active.Count == 0) return "";

            var longTerm = active.Where(g => g.Type == GoalType.LongTerm).ToList();
            var shortTerm = active.Where(g => g.Type == GoalType.ShortTerm).ToList();

            var lines = new List<string>();
            lines.Add("【长期追求】");

            foreach (var g in longTerm)
            {
                var pct = (int)(g.Progress * 100);
                var m = g.CurrentMilestone;
                var hint = m != null ? $" → 下一步: {m.Desc}" : "";
                lines.Add($"  ★ {g.Desc} ({pct}%){hint}");
            }

            if (shortTerm.Count > 0)
            {
                lines.Add("【今日待办】");
                foreach (var g in shortTerm)
                {
                    lines.Add($"  · {g.Desc}");
                }
            }

            lines.Add("");
            return string.Join("\n", lines);
        }

        // ===== Private =====

        private void CheckMilestones(string key, object value)
        {
            // Signal mode: key == "_signal"
            if (key == "_signal")
            {
                var signal = value?.ToString() ?? "";
                var parts = signal.Split(':');
                if (parts.Length < 2) return;
                var source = parts[0];
                var target = parts[1];

                foreach (var goal in Goals.Where(g => !g.Completed))
                {
                    if (!string.IsNullOrEmpty(goal.TargetEntity) && goal.TargetEntity != target)
                        continue;
                    var m = goal.CurrentMilestone;
                    if (m == null || m.Done || m.Source != source) continue;
                    m.Current++;
                    var attrKey = $"goal_{goal.Id}_{m.Id}";
                    _attrs.Set(attrKey, m.Current, "number", new[] { "goal_track" });
                    if (m.Current >= m.Count) AdvanceMilestone(goal, m);
                }
                return;
            }

            // Legacy mode: key matches milestone's ConditionAttr
            long numVal = 0;
            bool isNum = value is long l ? (numVal = l) != 0 || true
                       : value is int i ? (numVal = i) != 0 || true
                       : false;

            foreach (var goal in Goals.Where(g => !g.Completed))
            {
                var m = goal.CurrentMilestone;
                if (m == null || m.Done || m.ConditionAttr != key) continue;

                bool met = isNum && m.ConditionOp switch
                {
                    "gte" => numVal >= m.ConditionValue,
                    "lte" => numVal <= m.ConditionValue,
                    "eq" => numVal == m.ConditionValue,
                    _ => false
                };
                if (met) AdvanceMilestone(goal, m);
            }
        }

        private void AdvanceMilestone(Goal goal, Milestone m)
        {
            m.Done = true;
            OnMilestoneCompleted?.Invoke(goal, m);
            Logger.Debug("Goal", $"Milestone done: {goal.Id}.{m.Id}", new { desc = m.Desc });

            if (goal.CurrentMilestone == null)
            {
                goal.Completed = true;
                OnGoalCompleted?.Invoke(goal);
                Logger.Debug("Goal", $"Goal completed: {goal.Id}", new { desc = goal.Desc });
            }
        }
    }
}
