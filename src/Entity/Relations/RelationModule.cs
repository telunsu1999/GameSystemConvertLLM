using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLoop
{
    // ================================================================
    // Data Models
    // ================================================================

    /// <summary>Npc's subjective view of another NPC. One-way only.</summary>
    public class Relation
    {
        public string TargetId { get; set; } = "";
        public string Type { get; set; } = "";        // affection, trust, respect, fear, rivalry
        public float Value { get; set; }               // 0-100
        public List<string> Tags { get; set; } = new List<string>();
        public List<RelationEvent> History { get; set; } = new List<RelationEvent>();
    }

    public class RelationEvent
    {
        public int Tick { get; set; }
        public float Delta { get; set; }
        public string Reason { get; set; } = "";
    }

    /// <summary>What this NPC perceives about how another NPC feels toward them.</summary>
    public class Perceived
    {
        public string SourceId { get; set; } = "";
        public string Type { get; set; } = "";         // felt_warmth, sensed_hostility, heard_rumor
        public float Value { get; set; }                // 0-100
        public float Confidence { get; set; }           // 0-1, how sure this NPC is about the perception
        public string Source { get; set; } = "";        // direct_talk, gift_received, gossip, observation
        public int UpdatedAt { get; set; }
    }

    // ================================================================
    // Component
    // ================================================================

    /// <summary>
    /// Maintains an NPC's subjective, one-way relationships toward other NPCs,
    /// and what this NPC perceives about how others feel toward them.
    ///
    /// - outwardRelations: "I feel X toward Y"
    /// - perceivedRelations: "I think Y feels Z toward me" (indirect, has confidence)
    ///
    /// Both are private to this NPC; other NPCs cannot read them directly.
    /// Perceptions are updated when events occur (talk, gift, gossip...).
    /// </summary>
    public class RelationModule : IEntityComponent
    {
        // targetNpcId → Relation
        public Dictionary<string, Relation> OutwardRelations { get; } = new Dictionary<string, Relation>();
        // sourceNpcId → Perceived
        public Dictionary<string, Perceived> PerceivedRelations { get; } = new Dictionary<string, Perceived>();

        public void OnTick(TickContext ctx) { /* event-driven, no polling */ }

        // ================================================================
        // Outward CRUD
        // ================================================================

        /// <summary>Create or overwrite a relation toward another NPC.</summary>
        public Relation Set(string targetId, string type, float value, List<string> tags = null)
        {
            var rel = new Relation
            {
                TargetId = targetId,
                Type = type,
                Value = Math.Clamp(value, 0, 100),
                Tags = tags ?? new List<string>(),
            };
            OutwardRelations[targetId] = rel;
            return rel;
        }

        /// <summary>Get relation toward target, or null.</summary>
        public Relation Get(string targetId)
        {
            return OutwardRelations.TryGetValue(targetId, out var r) ? r : null;
        }

        /// <summary>Adjust relation value by delta, recording the reason in history.</summary>
        public Relation Adjust(string targetId, float delta, string reason, int tick = 0)
        {
            var rel = Get(targetId);
            if (rel == null)
            {
                rel = Set(targetId, "neutral", 50 + delta);
                rel.History.Add(new RelationEvent { Tick = tick, Delta = delta, Reason = reason });
                return rel;
            }

            rel.Value = Math.Clamp(rel.Value + delta, 0, 100);
            rel.History.Add(new RelationEvent { Tick = tick, Delta = delta, Reason = reason });
            return rel;
        }

        /// <summary>Remove relation toward target.</summary>
        public bool Remove(string targetId)
        {
            return OutwardRelations.Remove(targetId);
        }

        /// <summary>Get all relations of a given type.</summary>
        public List<Relation> GetAllOfType(string type)
        {
            return OutwardRelations.Values.Where(r => r.Type == type).ToList();
        }

        /// <summary>Get target IDs of relations that have the given tag.</summary>
        public List<string> GetTargetsWithTag(string tag)
        {
            return OutwardRelations.Values
                .Where(r => r.Tags.Contains(tag))
                .Select(r => r.TargetId)
                .ToList();
        }

        /// <summary>Check if this NPC has any relation toward target.</summary>
        public bool HasRelation(string targetId)
        {
            return OutwardRelations.ContainsKey(targetId);
        }

        // ================================================================
        // Perception CRUD
        // ================================================================

        /// <summary>Record a perception about how sourceId feels toward this NPC.</summary>
        public Perceived PerceiveFrom(string sourceId, string type, float value, float confidence, string source, int tick = 0)
        {
            var p = new Perceived
            {
                SourceId = sourceId,
                Type = type,
                Value = Math.Clamp(value, 0, 100),
                Confidence = Math.Clamp(confidence, 0, 1),
                Source = source,
                UpdatedAt = tick,
            };
            PerceivedRelations[sourceId] = p;
            return p;
        }

        /// <summary>Get perception about how sourceId feels, or null.</summary>
        public Perceived GetPerceived(string sourceId)
        {
            return PerceivedRelations.TryGetValue(sourceId, out var p) ? p : null;
        }

        /// <summary>Remove a perception.</summary>
        public bool ForgetPerceived(string sourceId)
        {
            return PerceivedRelations.Remove(sourceId);
        }

        /// <summary>Get all perceptions with confidence >= min.</summary>
        public List<Perceived> GetPerceivedByConfidence(float minConfidence)
        {
            return PerceivedRelations.Values
                .Where(p => p.Confidence >= minConfidence)
                .ToList();
        }

        // ================================================================
        // Prompt text (for LlmPlanner)
        // ================================================================

        /// <summary>
        /// Generate a compact text summary of this NPC's relationships,
        /// suitable for injecting into LLM prompts.
        /// </summary>
        public string ToPromptText()
        {
            var lines = new List<string>();

            if (OutwardRelations.Count > 0)
            {
                lines.Add("【人际关系】");
                foreach (var kv in OutwardRelations)
                {
                    var r = kv.Value;
                    var tagStr = r.Tags.Count > 0 ? $" [{string.Join(",", r.Tags)}]" : "";
                    lines.Add($"  对{r.TargetId}: {r.Type}={r.Value:F0}{tagStr}");
                }
            }

            if (PerceivedRelations.Count > 0)
            {
                lines.Add("【感知到的关系】");
                foreach (var kv in PerceivedRelations)
                {
                    var p = kv.Value;
                    lines.Add($"  感觉{p.SourceId}对己: {p.Type}={p.Value:F0} (可信度:{p.Confidence:F1}, 来源:{p.Source})");
                }
            }

            return lines.Count > 1 ? string.Join("\n", lines) : "";
        }
    }
}
