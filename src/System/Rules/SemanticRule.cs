using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameLoop
{
    /// <summary>
    /// A single semantic rule, deserialized from JSON config.
    /// Type determines which fields are used.
    /// </summary>
    public class SemanticRule
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("output")]
        public string Output { get; set; }

        // ratio_status
        [JsonProperty("current")]
        public string Current { get; set; }

        [JsonProperty("max")]
        public string Max { get; set; }

        [JsonProperty("thresholds")]
        public List<RuleThreshold> Thresholds { get; set; }

        // key_value
        [JsonProperty("field")]
        public string Field { get; set; }

        [JsonProperty("template")]
        public string Template { get; set; }

        // inventory_list
        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("prefix")]
        public string Prefix { get; set; }

        [JsonProperty("separator")]
        public string Separator { get; set; } = ", ";

        // event_summary
        [JsonProperty("match")]
        public Dictionary<string, object> Match { get; set; }

        // comparison
        [JsonProperty("left")]
        public string Left { get; set; }

        [JsonProperty("right")]
        public string Right { get; set; }

        [JsonProperty("cases")]
        public List<ComparisonCase> Cases { get; set; }

        // conditional
        [JsonProperty("conditions")]
        public List<ConditionClause> Conditions { get; set; }
    }

    public class RuleThreshold
    {
        [JsonProperty("ratio")]
        public List<double> Ratio { get; set; } = new List<double>();

        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public class ComparisonCase
    {
        [JsonProperty("op")]
        public string Op { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public class ConditionClause
    {
        [JsonProperty("field")]
        public string Field { get; set; }

        [JsonProperty("op")]
        public string Op { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }

        // For ratio computation in conditions
        [JsonProperty("compute")]
        public string Compute { get; set; }

        [JsonProperty("field_c")]
        public string FieldC { get; set; }

        [JsonProperty("field_m")]
        public string FieldM { get; set; }
    }
}
