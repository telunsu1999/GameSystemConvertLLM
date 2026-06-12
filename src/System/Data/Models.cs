using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameLoop
{
    /// <summary>
    /// 闁衡偓閸洘鑲犻梺鏉跨Ф閻ゅ棝濡存笟绐糘N闁告瑯鍨扮花顓㈠礆濡も偓鐎垫煡濡?
    /// </summary>
    public class CollectConfig
    {
        [JsonProperty("attr_tags")]
        public string[] AttrTags { get; set; } = new string[0];

        [JsonProperty("event_tags")]
        public string[] EventTags { get; set; } = new string[0];

        [JsonProperty("rule_files")]
        public string[] RuleFiles { get; set; } = new string[0];

        [JsonProperty("event_days")]
        public int? EventDays { get; set; }

        [JsonProperty("event_limit")]
        public int? EventLimit { get; set; }

        [JsonProperty("event_sort_by")]
        public string EventSortBy { get; set; }

        [JsonProperty("event_source_filter")]
        public string[] EventSourceFilter { get; set; }
    }

    /// <summary>
    /// 闁衡偓閸洘鑲犵紓浣规尰閻忓鏁嶅顒€鏂у┑顔碱儐閺嗙喖骞?+ 閻犲浂鍘虹粻鐔煎礌閺嶃劍鐎柡?
    /// </summary>
    public class CollectResult
    {
        public Dictionary<string, object> RawData { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, string> SemanticTexts { get; set; } = new Dictionary<string, string>();
    }
}
