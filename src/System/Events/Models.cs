using System;
using System.Collections.Generic;

namespace GameLoop
{
    public class Perception
    {
        public string NpcId { get; set; }
        public DateTime When { get; set; }
        public string How { get; set; }
        public string From { get; set; }
    }

    public class Event
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();
        public DateTime Time { get; set; }
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
        public List<Perception> Perceptions { get; set; } = new List<Perception>();
    }

    public class EventQuery
    {
        public string NpcId { get; set; }
        public string[] Tags { get; set; }
        public int? RecentDays { get; set; }
        public bool? Known { get; set; }
        public string[] SourceFilter { get; set; }
        public string SortBy { get; set; } = "time_desc";
        public IScoringStrategy Scorer { get; set; }
        public int? Limit { get; set; }
    }

    public interface IScoringStrategy
    {
        double Score(Event evt);
    }
}
