using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameLoop
{
    /// <summary>
    /// JSON config for a game entity. References separate files for schedule,
    /// triggers, and actions rather than embedding them inline.
    /// </summary>
    public class EntityConfig
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("attrs")]
        public List<AttrDef> Attrs { get; set; } = new List<AttrDef>();

        [JsonProperty("schedule")]
        public string SchedulePath { get; set; }

        [JsonProperty("triggers")]
        public string TriggersPath { get; set; }

        [JsonProperty("actions")]
        public List<string> ActionPaths { get; set; } = new List<string>();

        [JsonProperty("goals")]
        public string GoalsPath { get; set; }
    }

    public class AttrDef
    {
        [JsonProperty("key")]
        public string Key { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = "number";

        [JsonProperty("value")]
        public object Value { get; set; }

        [JsonProperty("tags")]
        public string[] Tags { get; set; } = new string[0];
    }
}
