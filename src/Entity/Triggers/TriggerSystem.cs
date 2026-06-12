using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace GameLoop
{
    public class TriggerSystem : IEntityComponent
    {
        private readonly List<TriggerRule> _rules = new List<TriggerRule>();

        public void LoadRules(string jsonPath)
        {
            var json = File.ReadAllText(jsonPath);
            var config = JsonConvert.DeserializeObject<TriggerConfig>(json);
            if (config?.Rules != null)
                _rules.AddRange(config.Rules);
        }

        public TriggerResult Evaluate(
            string changeType,
            string changeKey,
            Attributes attrs)
        {
            var result = new TriggerResult();

            foreach (var rule in _rules)
            {
                if (!MatchConditions(rule.Conditions, attrs))
                    continue;

                if (rule.Mode == "direct")
                    result.DirectActions.Add(rule);
                else
                    result.LlmOptions.Add(rule);
            }

            if (result.DirectActions.Count > 0 || result.LlmOptions.Count > 0)
                Logger.Debug("Trigger", $"Evaluate: direct={result.DirectActions.Count} llm={result.LlmOptions.Count}", new { changeType, changeKey });

            return result;
        }

        private bool MatchConditions(List<Condition> conditions, Attributes attrs)
        {
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
            if (actual is long l) return CompareLong(l, c.Op, c.Value);
            if (actual is int i) return CompareLong(i, c.Op, c.Value);
            if (actual is double d) return CompareDouble(d, c.Op, c.Value);
            if (actual is float f) return CompareDouble(f, c.Op, c.Value);
            if (actual is bool b) return CompareBool(b, c.Op, c.Value);
            if (actual is string s) return CompareString(s, c.Op, c.Value);
            return false;
        }

        private bool CompareLong(long actual, string op, object expected)
        {
            long exp = ToLong(expected);
            switch (op)
            {
                case "eq":  return actual == exp;
                case "ne":  return actual != exp;
                case "gt":  return actual > exp;
                case "gte": return actual >= exp;
                case "lt":  return actual < exp;
                case "lte": return actual <= exp;
                default: return false;
            }
        }

        private bool CompareDouble(double actual, string op, object expected)
        {
            double exp = ToDouble(expected);
            switch (op)
            {
                case "eq":  return actual == exp;
                case "ne":  return actual != exp;
                case "gt":  return actual > exp;
                case "gte": return actual >= exp;
                case "lt":  return actual < exp;
                case "lte": return actual <= exp;
                default: return false;
            }
        }

        private bool CompareString(string actual, string op, object expected)
        {
            string exp = expected?.ToString() ?? "";
            switch (op)
            {
                case "eq":  return actual == exp;
                case "ne":  return actual != exp;
                default: return false;
            }
        }

        private bool CompareBool(bool actual, string op, object expected)
        {
            bool exp = ToBool(expected);
            switch (op)
            {
                case "eq":  return actual == exp;
                case "ne":  return actual != exp;
                default: return false;
            }
        }

        private long ToLong(object v)
        {
            if (v is long l) return l;
            if (v is int i) return i;
            if (v is double d) return (long)d;
            return 0;
        }

        private double ToDouble(object v)
        {
            if (v is double d) return d;
            if (v is float f) return f;
            if (v is long l) return l;
            if (v is int i) return i;
            return 0;
        }

        private bool ToBool(object v)
        {
            if (v is bool b) return b;
            if (v is string s) return bool.TryParse(s, out var parsed) && parsed;
            if (v is long l) return l != 0;
            return false;
        }

        public void OnTick(TickContext ctx) { }
    }
}
