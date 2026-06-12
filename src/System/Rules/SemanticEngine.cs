using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace GameLoop
{
    /// <summary>
    /// Universal rule-driven semantic engine.
    /// Pure function: rules + data 闁?text dictionary.
    /// Knows nothing about NPCs, games, or any specific domain.
    /// </summary>
    public class SemanticEngine
    {
        // 闁冲厜鍋撻柍鍏夊亾 Public API 闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾

        /// <summary>
        /// Evaluate all rules against data, returning output_field 闁?text.
        /// </summary>
        public Dictionary<string, string> EvaluateAll(
            List<SemanticRule> rules,
            Dictionary<string, object> data)
        {
            var result = new Dictionary<string, string>();
            foreach (var rule in rules)
            {
                var text = Evaluate(rule, data);
                if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(rule.Output))
                {
                    result[rule.Output] = text;
                }
            }
            return result;
        }

        /// <summary>
        /// Evaluate a single rule.
        /// </summary>
        public string Evaluate(SemanticRule rule, Dictionary<string, object> data)
        {
            return rule.Type switch
            {
                "ratio_status"    => EvalRatioStatus(rule, data),
                "key_value"       => EvalKeyValue(rule, data),
                "inventory_list"  => EvalInventoryList(rule, data),
                "event_summary"   => EvalEventSummary(rule, data),
                "comparison"      => EvalComparison(rule, data),
                "conditional"     => EvalConditional(rule, data),
                _                 => null
            };
        }

        /// <summary>
        /// Load rules from a single JSON file.
        /// </summary>
        public List<SemanticRule> LoadRules(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var wrapper = JsonConvert.DeserializeObject<RuleFile>(json);
            return wrapper?.Rules ?? new List<SemanticRule>();
        }

        /// <summary>
        /// Load and merge rules from multiple JSON files (in order).
        /// </summary>
        public List<SemanticRule> LoadRules(string[] filePaths)
        {
            var all = new List<SemanticRule>();
            foreach (var path in filePaths)
            {
                if (File.Exists(path))
                    all.AddRange(LoadRules(path));
            }
            return all;
        }

        // 闁冲厜鍋撻柍鍏夊亾 Evaluators 闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾

        private string EvalRatioStatus(SemanticRule rule, Dictionary<string, object> data)
        {
            if (!TryGetDouble(data, rule.Current, out var current)) return null;
            if (!TryGetDouble(data, rule.Max, out var max) || max <= 0) return null;

            var ratio = current / max;
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;

            if (rule.Thresholds == null) return null;

            foreach (var t in rule.Thresholds)
            {
                if (t.Ratio != null && t.Ratio.Count >= 2)
                {
                    if (ratio >= t.Ratio[0] && ratio <= t.Ratio[1])
                        return t.Text;
                }
            }
            return null;
        }

        private string EvalKeyValue(SemanticRule rule, Dictionary<string, object> data)
        {
            if (!data.TryGetValue(rule.Field, out var value) || value == null)
                return null;

            var template = rule.Template ?? "{value}";
            return template.Replace("{value}", value.ToString());
        }

        private string EvalInventoryList(SemanticRule rule, Dictionary<string, object> data)
        {
            if (!data.TryGetValue(rule.Field, out var raw)) return null;

            // Inventory can be Dictionary<string,int> or List<object> from JSON
            var items = new List<KeyValuePair<string, int>>();

            if (raw is Dictionary<string, int> dict)
            {
                items.AddRange(dict.Select(kv => kv));
            }
            else if (raw is Dictionary<string, object> dictObj)
            {
                foreach (var kv in dictObj)
                {
                    if (kv.Value is long l)
                        items.Add(new KeyValuePair<string, int>(kv.Key, (int)l));
                    else if (kv.Value is int i)
                        items.Add(new KeyValuePair<string, int>(kv.Key, i));
                }
            }
            else if (raw is Newtonsoft.Json.Linq.JObject jObj)
            {
                foreach (var prop in jObj.Properties())
                    items.Add(new KeyValuePair<string, int>(prop.Name, prop.Value.ToObject<int>()));
            }

            if (items.Count == 0) return null;

            var fmt = rule.Format ?? "{name}x{count}";
            var sep = rule.Separator ?? ", ";

            var parts = items.Select(item =>
                fmt.Replace("{name}", item.Key)
                   .Replace("{count}", item.Value.ToString())
            );

            var prefix = rule.Prefix ?? "";
            return prefix + string.Join(sep, parts);
        }

        private string EvalEventSummary(SemanticRule rule, Dictionary<string, object> data)
        {
            if (!data.TryGetValue("memory_events", out var raw)) return null;

            var events = raw as System.Collections.IList;
            if (events == null) return null;

            foreach (var evtObj in events)
            {
                if (evtObj is Dictionary<string, object> evt && MatchEvent(evt, rule.Match))
                {
                    var text = rule.Template ?? "";
                    foreach (var kv in evt)
                    {
                        text = text.Replace("{" + kv.Key + "}", kv.Value?.ToString() ?? "");
                    }
                    return text;
                }
            }
            return null;
        }

        private string EvalComparison(SemanticRule rule, Dictionary<string, object> data)
        {
            if (!TryGetDouble(data, rule.Left, out var left)) return null;
            if (!TryGetDouble(data, rule.Right, out var right)) return null;

            if (rule.Cases == null) return null;

            foreach (var c in rule.Cases)
            {
                bool match = c.Op switch
                {
                    "lt"  => left < right,
                    "lte" => left <= right,
                    "gt"  => left > right,
                    "gte" => left >= right,
                    "eq"  => Math.Abs(left - right) < 0.001,
                    _     => false
                };

                if (match)
                {
                    return c.Text
                        .Replace("{diff}", Math.Abs(right - left).ToString("F0"))
                        .Replace("{left}", left.ToString("F0"))
                        .Replace("{right}", right.ToString("F0"));
                }
            }
            return null;
        }

        private string EvalConditional(SemanticRule rule, Dictionary<string, object> data)
        {
            if (rule.Conditions == null || rule.Conditions.Count == 0) return null;

            foreach (var cond in rule.Conditions)
            {
                if (cond.Compute == "ratio")
                {
                    if (!TryGetDouble(data, cond.FieldC, out var current)) return null;
                    if (!TryGetDouble(data, cond.FieldM, out var max) || max <= 0) return null;
                    var ratio = current / max;
                    if (ratio < 0) ratio = 0;
                    if (ratio > 1) ratio = 1;
                    var target = Convert.ToDouble(cond.Value);
                    if (!CompareDouble(ratio, cond.Op, target)) return null;
                }
                else
                {
                    // Try numeric first, fall back to string comparison
                    if (TryGetDouble(data, cond.Field, out var numVal))
                    {
                        var target = Convert.ToDouble(cond.Value);
                        if (!CompareDouble(numVal, cond.Op, target)) return null;
                    }
                    else if (data.TryGetValue(cond.Field, out var strVal))
                    {
                        var a = strVal?.ToString() ?? "";
                        var b = cond.Value?.ToString() ?? "";
                        bool match = cond.Op switch
                        {
                            "eq"  => a == b,
                            "neq" => a != b,
                            _    => false
                        };
                        if (!match) return null;
                    }
                    else return null;
                }
            }

            return rule.Template ?? "";
        }

        private bool CompareDouble(double a, string op, double b)
        {
            return op switch
            {
                "lt"  => a < b,
                "lte" => a <= b,
                "gt"  => a > b,
                "gte" => a >= b,
                "eq"  => Math.Abs(a - b) < 0.001,
                _    => false
            };
        }

        // 闁冲厜鍋撻柍鍏夊亾 Helpers 闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾

        private bool MatchEvent(Dictionary<string, object> evt, Dictionary<string, object> match)
        {
            if (match == null) return true;
            foreach (var kv in match)
            {
                if (!evt.TryGetValue(kv.Key, out var val)) return false;
                if (val?.ToString() != kv.Value?.ToString()) return false;
            }
            return true;
        }

        private bool TryGetDouble(Dictionary<string, object> data, string key, out double value)
        {
            value = 0;
            if (string.IsNullOrEmpty(key)) return false;
            if (!data.TryGetValue(key, out var raw)) return false;
            try
            {
                value = Convert.ToDouble(raw);
                return true;
            }
            catch { return false; }
        }

        // 闁冲厜鍋撻柍鍏夊亾 JSON wrapper 闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾闁冲厜鍋撻柍鍏夊亾

        private class RuleFile
        {
            [JsonProperty("rules")]
            public List<SemanticRule> Rules { get; set; }
        }
    }
}
