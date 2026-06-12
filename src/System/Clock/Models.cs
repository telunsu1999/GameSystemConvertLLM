using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameLoop
{
    public struct TickSnapshot
    {
        public int Tick;
        public int Minute;
        public int Hour;
        public int Day;
        public string Season;
        public int Year;
        public string TimeBlock;
    }

    public class ClockConfig
    {
        [JsonProperty("minutes_per_tick")]
        public int MinutesPerTick { get; set; } = 10;

        [JsonProperty("days_per_season")]
        public int DaysPerSeason { get; set; } = 30;

        [JsonProperty("seasons")]
        public List<SeasonConfig> Seasons { get; set; } = new List<SeasonConfig>();

        [JsonProperty("time_blocks")]
        public List<TimeBlockConfig> TimeBlocks { get; set; } = new List<TimeBlockConfig>();
    }

    public class SeasonConfig
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("index")]
        public int Index { get; set; }
    }

    public class TimeBlockConfig
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("start_hour")]
        public int StartHour { get; set; }

        [JsonProperty("end_hour")]
        public int EndHour { get; set; }
    }
}
