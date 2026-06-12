using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameLoop
{
    public class ActionItem
    {
        public string ActionType;
        public Dictionary<string, object> Params;
    }

    public class CurrentAction
    {
        public string ActionType;
        public Dictionary<string, object> Params;
        public string Status;       // "executing" / "interrupted"
        public int StartedTick;
    }

    public class ActionMapping
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("effect")]
        public string Effect { get; set; }

        [JsonProperty("interruptible")]
        public bool Interruptible { get; set; }

        [JsonProperty("check")]
        public List<Condition> Check { get; set; } = new List<Condition>();

        [JsonProperty("steps")]
        public List<ActionStep> Steps { get; set; } = new List<ActionStep>();

        [JsonProperty("on_interrupt")]
        public List<ActionStep> OnInterrupt { get; set; } = new List<ActionStep>();

        [JsonProperty("on_condition")]
        public ActionChain OnCondition { get; set; }

        [JsonProperty("level")]
        public string Level { get; set; }

        [JsonProperty("params")]
        public List<ParamSchema> Params { get; set; }
    }

    public class ActionStep
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("field")]
        public string Field { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }

        [JsonProperty("event_type")]
        public string EventType { get; set; }

        [JsonProperty("event_tags")]
        public string[] EventTags { get; set; }

        [JsonProperty("event_data")]
        public Dictionary<string, object> EventData { get; set; }

        [JsonProperty("action_type")]
        public string ActionType { get; set; }

        [JsonProperty("tick_offset")]
        public int? TickOffset { get; set; }
    }

    public class ActionChain
    {
        [JsonProperty("condition")]
        public List<Condition> Condition { get; set; } = new List<Condition>();

        [JsonProperty("steps")]
        public List<ActionStep> Steps { get; set; } = new List<ActionStep>();
    }

    public class ParamSchema
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }  // "choice" / "free" / "number" / "string"

        [JsonProperty("options")]
        public List<string> Options { get; set; }

        [JsonProperty("options_from")]
        public string OptionsFrom { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }
}
