using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameLoop
{
    public class ScheduleItem
    {
        [JsonProperty("action_type")]
        public string ActionType { get; set; } = string.Empty;

        [JsonProperty("tags")]
        public string[] Tags { get; set; } = Array.Empty<string>();

        [JsonProperty("params")]
        public Dictionary<string, object> Params { get; set; } = new Dictionary<string, object>();

        [JsonProperty("trigger_tick")]
        public int TriggerTick { get; set; }

        [JsonProperty("interval")]
        public int? Interval { get; set; }

        public bool IsOneTime => Interval == null;

        /// <summary>
        /// Pre-resolved action instance (for child actions from parent Execute).
        /// If set, bypasses string-based Resolve lookup. Stored as object to avoid
        /// circular project dependency on ActionResolver.
        /// </summary>
        public object ResolvedAction { get; set; }

        public ScheduleItem Clone()
        {
            return new ScheduleItem
            {
                ActionType = ActionType,
                Tags = (string[])Tags.Clone(),
                Params = new Dictionary<string, object>(Params),
                TriggerTick = TriggerTick,
                Interval = Interval,
                ResolvedAction = ResolvedAction
            };
        }
    }

    public class ScheduleConfig
    {
        [JsonProperty("items")]
        public List<ScheduleConfigItem> Items { get; set; } = new List<ScheduleConfigItem>();
    }

    public class ScheduleConfigItem
    {
        [JsonProperty("action_type")]
        public string ActionType { get; set; } = string.Empty;

        [JsonProperty("tags")]
        public string[] Tags { get; set; } = Array.Empty<string>();

        [JsonProperty("params")]
        public Dictionary<string, object> Params { get; set; } = new Dictionary<string, object>();

        [JsonProperty("at_hour")]
        public int? AtHour { get; set; }

        [JsonProperty("at_tick")]
        public int? AtTick { get; set; }

        [JsonProperty("every_hour")]
        public int? EveryHour { get; set; }

        [JsonProperty("every_tick")]
        public int? EveryTick { get; set; }
    }
}
