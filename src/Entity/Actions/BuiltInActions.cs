using System;
using System.Collections.Generic;

namespace GameLoop
{
    public class GotoAction : IAction
    {
        public string ActionType => "goto";
        public bool Interruptible => true;
        private string _target;

        public bool CanExecute(ActionContext ctx)
        {
            var dead = ctx.Attrs.Get("dead");
            return dead == null || Convert.ToInt64(dead.Value) != 1;
        }

        public IEnumerable<IAction> Execute(ActionContext ctx)
        {
            _target = ctx.Params.ContainsKey("location")
                ? ctx.Params["location"].ToString()
                : "???";

            ctx.Attrs.Set("moving", 1, "number", new[] { "travel" });
            ctx.Attrs.Set("prev_location",
                ctx.Attrs.Get("location")?.Value ?? "",
                "string", new[] { "travel" });

            ctx.Events.Record("npc_depart", new[] { "travel" },
                new Dictionary<string, object> { { "to", _target } });

            // Return child action that will be scheduled
            yield return new ArriveAction { Location = _target };
        }

        public void OnInterrupt(ActionContext ctx)
        {
            ctx.Attrs.Set("moving", 0, "number", new[] { "travel" });
            ctx.Events.Record("npc_interrupted", new[] { "travel" },
                new Dictionary<string, object>());
        }
    }

    public class ArriveAction : IAction
    {
        public string ActionType => "arrive";
        public bool Interruptible => false;
        public string Location;

        public bool CanExecute(ActionContext ctx) => true;

        public IEnumerable<IAction> Execute(ActionContext ctx)
        {
            ctx.Attrs.Set("location", Location, "string", new[] { "world" });
            ctx.Attrs.Set("moving", 0, "number", new[] { "travel" });
            ctx.Events.Record("npc_arrive", new[] { "travel" },
                new Dictionary<string, object> { { "at", Location } });
            yield break;
        }

        public void OnInterrupt(ActionContext ctx) { }
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

            ctx.Events.Record("damage_taken", new[] { "combat" },
                new Dictionary<string, object> { { "amount", amount } });

            if (newHp <= 0)
            {
                ctx.Attrs.Set("dead", 1, "number", new[] { "combat", "death" });
                ctx.Events.Record("death", new[] { "combat", "death" },
                    new Dictionary<string, object>());
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

            ctx.Events.Record("trade_completed", new[] { "trade" },
                new Dictionary<string, object> {
                    { "item", item }, { "count", count }, { "cost", cost }
                });

            yield break;
        }

        public void OnInterrupt(ActionContext ctx) { }
    }
}
