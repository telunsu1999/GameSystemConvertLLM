using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLoop
{
    public class GenericAction : IAction
    {
        private readonly ActionMapping _mapping;

        public string ActionType { get; }
        public bool Interruptible => _mapping.Interruptible;

        public GenericAction(string actionType, ActionMapping mapping)
        {
            ActionType = actionType;
            _mapping = mapping;
        }

        public bool CanExecute(ActionContext ctx)
        {
            return CheckConditions(_mapping.Check, ctx.Attrs);
        }

        public IEnumerable<IAction> Execute(ActionContext ctx)
        {
            foreach (var step in _mapping.Steps)
            {
                ExecuteStep(step, ctx);
            }

            // Check on_condition chain
            if (_mapping.OnCondition != null && CheckConditions(_mapping.OnCondition.Condition, ctx.Attrs))
            {
                foreach (var step in _mapping.OnCondition.Steps)
                {
                    ExecuteStep(step, ctx);
                }
            }

            yield break;
        }

        public void OnInterrupt(ActionContext ctx)
        {
            if (_mapping.OnInterrupt != null)
            {
                foreach (var step in _mapping.OnInterrupt)
                {
                    ExecuteStep(step, ctx);
                }
            }
        }

        private void ExecuteStep(ActionStep step, ActionContext ctx)
        {
            switch (step.Type)
            {
                case "set_attr":
                case "add_attr":
                case "sub_attr":
                    ExecuteAttrStep(step, ctx);
                    break;
                case "record_event":
                    ExecuteEventStep(step, ctx);
                    break;
                case "schedule":
                    ExecuteScheduleStep(step, ctx);
                    break;
            }
        }

        private void ExecuteAttrStep(ActionStep step, ActionContext ctx)
        {
            var field = ResolveField(step.Field, ctx);
            var val = ResolveValue(step.Value, ctx);
            var current = ctx.Attrs.Get(field);

            switch (step.Type)
            {
                case "set_attr":
                    if (val is int iv) ctx.Attrs.Set(field, iv, "number", Array.Empty<string>());
                    else if (val is long lv) ctx.Attrs.Set(field, lv, "number", Array.Empty<string>());
                    else if (val is string sv) ctx.Attrs.Set(field, sv, "string", Array.Empty<string>());
                    else if (val is bool bv) ctx.Attrs.Set(field, bv, "bool", Array.Empty<string>());
                    else ctx.Attrs.Set(field, val?.ToString() ?? "", "string", Array.Empty<string>());
                    break;

                case "add_attr":
                    if (current != null && val is long addL)
                        ctx.Attrs.Set(field, Convert.ToInt64(current.Value) + addL, "number", Array.Empty<string>());
                    else if (current != null && val is int addI)
                        ctx.Attrs.Set(field, Convert.ToInt64(current.Value) + addI, "number", Array.Empty<string>());
                    break;

                case "sub_attr":
                    if (current != null && val is long subL)
                        ctx.Attrs.Set(field, Convert.ToInt64(current.Value) - subL, "number", Array.Empty<string>());
                    else if (current != null && val is int subI)
                        ctx.Attrs.Set(field, Convert.ToInt64(current.Value) - subI, "number", Array.Empty<string>());
                    break;
            }
        }

        private void ExecuteEventStep(ActionStep step, ActionContext ctx)
        {
            var data = new Dictionary<string, object>();
            if (step.EventData != null)
            {
                foreach (var kv in step.EventData)
                {
                    data[kv.Key] = ResolveValue(kv.Value, ctx);
                }
            }

            ctx.Events.Record(
                step.EventType ?? step.ActionType,
                step.EventTags ?? Array.Empty<string>(),
                data);
        }

        private void ExecuteScheduleStep(ActionStep step, ActionContext ctx)
        {
            var tick = ctx.Snap.Tick + (step.TickOffset ?? 0);
            ctx.Scheduler.Add(
                step.ActionType,
                Array.Empty<string>(),
                BuildScheduleParams(step, ctx),
                atTick: tick);
        }

        private Dictionary<string, object> BuildScheduleParams(ActionStep step, ActionContext ctx)
        {
            var result = new Dictionary<string, object>();
            if (step.EventData != null)
            {
                foreach (var kv in step.EventData)
                {
                    result[kv.Key] = ResolveValue(kv.Value, ctx);
                }
            }
            return result;
        }

        private string ResolveField(string field, ActionContext ctx)
        {
            return field ?? "";
        }

        private object ResolveValue(object value, ActionContext ctx)
        {
            if (!(value is string s)) return value;
            if (!s.Contains("{") || !s.Contains("}")) return value;

            // {params.xxx}
            if (s.StartsWith("{params.") && s.EndsWith("}"))
            {
                var key = s.Substring(8, s.Length - 9);
                if (ctx.Params != null && ctx.Params.TryGetValue(key, out var v))
                    return v;
                return s;
            }

            // {attr.xxx}
            if (s.StartsWith("{attr.") && s.EndsWith("}"))
            {
                var key = s.Substring(6, s.Length - 7);
                var attr = ctx.Attrs.Get(key);
                return attr?.Value ?? s;
            }

            return s;
        }

        private bool CheckConditions(List<Condition> conditions, Attributes attrs)
        {
            if (conditions == null || conditions.Count == 0) return true;

            foreach (var c in conditions)
            {
                var attr = attrs.Get(c.Field);
                if (attr == null) return false;

                if (!EvaluateCondition(c, attr.Value))
                    return false;
            }
            return true;
        }

        private bool EvaluateCondition(Condition c, object actual)
        {
            if (actual is long l) return CompareLong(l, c.Op, ToLong(c.Value));
            if (actual is int i) return CompareLong(i, c.Op, ToLong(c.Value));
            if (actual is string s) return s == (c.Value?.ToString() ?? "") && c.Op == "eq";
            return false;
        }

        private bool CompareLong(long a, string op, long b)
        {
            switch (op)
            {
                case "eq":  return a == b;
                case "ne":  return a != b;
                case "gt":  return a > b;
                case "gte": return a >= b;
                case "lt":  return a < b;
                case "lte": return a <= b;
                default: return false;
            }
        }

        private long ToLong(object v)
        {
            if (v is long l) return l;
            if (v is int i) return i;
            return 0;
        }
    }
}
