using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLoop
{
    /// <summary>
    /// Continuous movement action — replaces the old GotoAction + ArriveAction pair.
    /// Uses MapSystem for pathfinding and advances movement tick by tick.
    ///
    /// Params:
    ///   target (string) — destination node ID.
    /// </summary>
    public class MoveToAction : IContinuousAction
    {
        // === IAction ===
        public string ActionType => "move_to";
        public bool Interruptible => true;

        // === Internal state (persists across ticks) ===
        private List<string>? _remainingPath;
        private int _ticksToNextNode;
        private int _totalLegs;
        private int _completedLegs;
        private string _destination = "";
        private MapSystem? _map;

        public bool CanExecute(ActionContext ctx)
        {
            var target = ctx.Params.TryGetValue("target", out var t) ? t?.ToString() : null;
            if (string.IsNullOrEmpty(target)) return false;

            var currentLoc = ctx.Attrs.Get("location")?.Value?.ToString();
            if (string.IsNullOrEmpty(currentLoc)) return false;

            _map = ctx.Map;
            if (_map == null) return false;

            if (currentLoc == target) return true; // already there

            var path = _map.FindPathOnGraph(currentLoc, target);
            return path?.Reachable == true;
        }

        public IEnumerable<IAction> Execute(ActionContext ctx)
        {
            var target = ctx.Params["target"].ToString();
            var currentLoc = ctx.Attrs.Get("location")?.Value?.ToString() ?? "";
            _destination = target;
            _map = ctx.Map;

            // Same location — nothing to do
            if (currentLoc == target)
            {
                IsCompleted = true;
                yield break;
            }

            var pathResult = _map.FindPathOnGraph(currentLoc, target);
            if (pathResult == null || !pathResult.Reachable)
            {
                IsCompleted = true;
                yield break;
            }

            // pathResult.NodePath includes the start node, skip it
            _remainingPath = pathResult.NodePath.Skip(1).ToList();
            _totalLegs = _remainingPath.Count;
            _completedLegs = 0;

            if (_remainingPath.Count == 0)
            {
                IsCompleted = true;
                yield break;
            }

            // Set initial moving state
            ctx.Attrs.Set("moving", 1, "number", new[] { "travel" });
            ctx.Attrs.Set("destination", _destination, "string", new[] { "travel" });
            ctx.Attrs.Set("travel_progress", $"0/{_totalLegs}", "string", new[] { "travel" });

            // First leg duration
            var firstEdge = _map.GetEdge(currentLoc, _remainingPath[0]);
            _ticksToNextNode = firstEdge?.TimeCostTicks ?? 1;

            // Record departure
            ctx.Records?.Remember(
                new RecordSource { Method = "self", Reliability = 1f },
                "travel", new[] { "travel" },
                new Dictionary<string, object> { { "action", "depart" }, { "from", currentLoc }, { "to", target } },
                ctx.Snap.Tick);

            // No child actions — OnTick drives everything from here
            yield break;
        }

        // === IContinuousAction ===

        public IEnumerable<IAction> OnTick(ActionContext ctx)
        {
            if (IsCompleted) yield break;

            _ticksToNextNode--;

            // Update progress every tick
            if (_totalLegs > 0)
            {
                double frac = _completedLegs + (1.0 - (double)_ticksToNextNode / Math.Max(1, GetCurrentLegTicks()));
                ctx.Attrs.Set("travel_progress",
                    $"{frac:F1}/{_totalLegs}", "string", new[] { "travel" });
            }

            // Still en route to next node
            if (_ticksToNextNode > 0)
                yield break;

            // === Arrived at next node ===
            var arrivedNodeId = _remainingPath[0];
            _remainingPath.RemoveAt(0);
            _completedLegs++;

            // Update location
            var prevLoc = ctx.Attrs.Get("location")?.Value?.ToString();
            ctx.Attrs.Set("location", arrivedNodeId, "string", new[] { "world" });

            // Notify MapSystem
            _map?.UnregisterEntity(GetEntityId(ctx), prevLoc ?? "");
            _map?.RegisterEntity(GetEntityId(ctx), arrivedNodeId);

            // Record pass-through event
            ctx.Records?.Remember(
                new RecordSource { Method = "self", Reliability = 1f },
                "travel", new[] { "travel" },
                new Dictionary<string, object> { { "action", "pass_through" }, { "at", arrivedNodeId } },
                ctx.Snap.Tick);

            // More nodes remain
            if (_remainingPath.Count > 0)
            {
                var nextEdge = _map?.GetEdge(arrivedNodeId, _remainingPath[0]);
                _ticksToNextNode = nextEdge?.TimeCostTicks ?? 1;
                yield break;
            }

            // === Final destination reached ===
            ctx.Attrs.Set("moving", 0, "number", new[] { "travel" });
            ctx.Attrs.Set("destination", "", "string", new[] { "travel" });
            ctx.Attrs.Set("travel_progress", $"{_totalLegs}/{_totalLegs}", "string", new[] { "travel" });

            ctx.Records?.Remember(
                new RecordSource { Method = "self", Reliability = 1f },
                "travel", new[] { "travel" },
                new Dictionary<string, object> { { "action", "arrive" }, { "at", _destination } },
                ctx.Snap.Tick);

            IsCompleted = true;
        }

        public bool IsCompleted { get; private set; }

        public void OnInterrupt(ActionContext ctx)
        {
            ctx.Attrs.Set("moving", 0, "number", new[] { "travel" });
            ctx.Attrs.Set("destination", "", "string", new[] { "travel" });

            var interruptedAt = _remainingPath?.Count > 0 ? _remainingPath[0] : _destination ?? "?";
            ctx.Records?.Remember(
                new RecordSource { Method = "self", Reliability = 1f },
                "travel", new[] { "travel" },
                new Dictionary<string, object> { { "action", "interrupted" }, { "at", interruptedAt } },
                ctx.Snap.Tick);

            IsCompleted = true;
        }

        private int GetCurrentLegTicks() => _ticksToNextNode > 0 ? _ticksToNextNode : 1;

        /// <summary>Try to extract entity ID from the attribute store.</summary>
        private static string GetEntityId(ActionContext ctx)
        {
            return ctx.Attrs.Get("id")?.Value?.ToString()
                ?? ctx.Attrs.Get("name")?.Value?.ToString()
                ?? "unknown";
        }
    }

    public class TakeDamageAction : IAction
    {
        public string ActionType => "take_damage";
        public bool Interruptible => false;

        public bool CanExecute(ActionContext ctx)
        {
            var dead = ctx.Attrs.Get("dead");
            return dead == null || Convert.ToInt64(dead.Value) != 1;
        }

        public IEnumerable<IAction> Execute(ActionContext ctx)
        {
            var amount = Convert.ToInt64(ctx.Params.ContainsKey("amount")
                ? ctx.Params["amount"] : 0);

            var hp = ctx.Attrs.Get("hp");
            var currentHp = hp != null ? Convert.ToInt64(hp.Value) : 0;
            var newHp = currentHp - amount;

            ctx.Attrs.Set("hp", newHp, "number", new[] { "vital", "hp" });

            ctx.Records?.Remember(
                new RecordSource { Method = "self", Reliability = 1f },
                "combat", new[] { "combat", "danger" },
                new Dictionary<string, object> { { "action", "damage_taken" }, { "amount", amount } },
                ctx.Snap.Tick);

            if (newHp <= 0)
            {
                ctx.Attrs.Set("dead", 1, "number", new[] { "combat", "death" });
                ctx.Records?.Remember(
                    new RecordSource { Method = "self", Reliability = 1f },
                    "death", new[] { "combat", "death" },
                    new Dictionary<string, object>(),
                    ctx.Snap.Tick);
            }

            yield break;
        }

        public void OnInterrupt(ActionContext ctx) { }
    }

    public class TradeAction : IAction
    {
        public string ActionType => "trade";
        public bool Interruptible => false;

        public bool CanExecute(ActionContext ctx)
        {
            var dead = ctx.Attrs.Get("dead");
            if (dead != null && Convert.ToInt64(dead.Value) == 1) return false;

            var gold = ctx.Attrs.Get("gold");
            var cost = ctx.Params.ContainsKey("cost") ? Convert.ToInt64(ctx.Params["cost"]) : 0;
            return gold != null && Convert.ToInt64(gold.Value) >= cost;
        }

        public IEnumerable<IAction> Execute(ActionContext ctx)
        {
            var cost = ctx.Params.ContainsKey("cost") ? Convert.ToInt64(ctx.Params["cost"]) : 0;
            var item = ctx.Params.ContainsKey("item") ? ctx.Params["item"].ToString() : "item";
            var count = ctx.Params.ContainsKey("count") ? Convert.ToInt64(ctx.Params["count"]) : 1;

            var gold = ctx.Attrs.Get("gold");
            var currentGold = gold != null ? Convert.ToInt64(gold.Value) : 0;
            ctx.Attrs.Set("gold", currentGold - cost, "number", new[] { "currency" });

            var itemKey = item + "_owned";
            var owned = ctx.Attrs.Get(itemKey);
            var currentOwned = owned != null ? Convert.ToInt64(owned.Value) : 0;
            ctx.Attrs.Set(itemKey, currentOwned + count, "number", new[] { "trade" });

            ctx.Records?.Remember(
                new RecordSource { Method = "self", Reliability = 1f },
                "trade", new[] { "trade" },
                new Dictionary<string, object> { { "item", item }, { "count", count }, { "cost", cost } },
                ctx.Snap.Tick);

            yield break;
        }

        public void OnInterrupt(ActionContext ctx) { }
    }
}
