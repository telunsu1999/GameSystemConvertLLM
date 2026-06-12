using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameLoop
{
    public class Condition
    {
        [JsonProperty("field")]
        public string Field { get; set; } = string.Empty;

        [JsonProperty("op")]
        public string Op { get; set; } = "eq";

        [JsonProperty("value")]
        public object Value { get; set; }
    }

    public class TriggerRule
    {
        [JsonProperty("mode")]
        public string Mode { get; set; } = "llm";

        [JsonProperty("conditions")]
        public List<Condition> Conditions { get; set; } = new List<Condition>();

        [JsonProperty("action_type")]
        public string ActionType { get; set; } = string.Empty;

        [JsonProperty("params")]
        public Dictionary<string, object> Params { get; set; } = new Dictionary<string, object>();

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("tags")]
        public string[] Tags { get; set; } = Array.Empty<string>();
    }

    public class TriggerResult
    {
        public List<TriggerRule> DirectActions { get; set; } = new List<TriggerRule>();
        public List<TriggerRule> LlmOptions { get; set; } = new List<TriggerRule>();
    }

    public class TriggerConfig
    {
        [JsonProperty("rules")]
        public List<TriggerRule> Rules { get; set; } = new List<TriggerRule>();
    }
}
