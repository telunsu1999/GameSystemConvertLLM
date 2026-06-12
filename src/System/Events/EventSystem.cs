using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLoop
{
    public class EventSystem
    {
        private readonly Dictionary<string, Event> _events = new Dictionary<string, Event>();
        private readonly Dictionary<string, HashSet<string>> _tagIndex = new Dictionary<string, HashSet<string>>();

        public string Record(string type, string[] tags, Dictionary<string, object> data)
        {
            var evt = new Event
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = type,
                Tags = tags ?? Array.Empty<string>(),
                Time = DateTime.Now,
                Data = data ?? new Dictionary<string, object>()
            };
            _events[evt.Id] = evt;
            AddToTagIndex(evt.Id, evt.Tags);
            Logger.Debug("Event", $"Record: {type}", new { id = evt.Id[..6], tags });
            return evt.Id;
        }

        public void Perceive(string eventId, string npcId, string how, string from = null)
        {
            if (!_events.TryGetValue(eventId, out var evt)) return;
            evt.Perceptions.Add(new Perception
            {
                NpcId = npcId,
                When = DateTime.Now,
                How = how,
                From = from
            });
        }

        public void Spread(string eventId, string fromNpc, string toNpc, string how)
        {
            if (!_events.TryGetValue(eventId, out var evt)) return;
            if (!evt.Perceptions.Any(p => p.NpcId == fromNpc)) return;

            evt.Perceptions.Add(new Perception
            {
                NpcId = toNpc,
                When = DateTime.Now,
                How = how,
                From = fromNpc
            });
        }

        public List<Event> Query(EventQuery q)
        {
            var candidates = _events.Values.AsEnumerable();

            // 闁哄秴娲ㄩ閿嬫交閸ャ劍濮?
            if (q.Tags != null && q.Tags.Length > 0)
            {
                var matchIds = new HashSet<string>();
                foreach (var tag in q.Tags)
                    if (_tagIndex.TryGetValue(tag, out var set))
                        matchIds.UnionWith(set);
                candidates = candidates.Where(e => matchIds.Contains(e.Id));
            }

            // 闁哄啫鐖煎Λ鎸庢交閸ャ劍濮?
            if (q.RecentDays.HasValue)
            {
                var cutoff = DateTime.Now.AddDays(-q.RecentDays.Value);
                candidates = candidates.Where(e => e.Time >= cutoff);
            }

            // NPC 闁规壆鍠撻悡鈩冩交閸ャ劍濮?
            if (!string.IsNullOrEmpty(q.NpcId))
            {
                // Known=null defaults to Known=true (show only events this NPC knows)
                if (q.Known == false)
                    candidates = candidates.Where(e => !e.Perceptions.Any(p => p.NpcId == q.NpcId));
                else
                    candidates = candidates.Where(e => e.Perceptions.Any(p => p.NpcId == q.NpcId));
            }

            // 闁哄鍎茬花顔芥交閸ャ劍濮?
            if (q.SourceFilter != null && q.SourceFilter.Length > 0 && !string.IsNullOrEmpty(q.NpcId))
            {
                candidates = candidates.Where(e =>
                    e.Perceptions.Any(p => p.NpcId == q.NpcId && q.SourceFilter.Contains(p.How)));
            }

            // 闁圭儤甯掔花?
            var list = candidates.ToList();
            if (q.Scorer != null)
            {
                foreach (var e in list) e.Data["_score"] = q.Scorer.Score(e);
            }

            switch (q.SortBy)
            {
                case "time_desc":  list.Sort((a, b) => b.Time.CompareTo(a.Time)); break;
                case "time_asc":   list.Sort((a, b) => a.Time.CompareTo(b.Time)); break;
                case "score_desc":
                    list.Sort((a, b) =>
                    {
                        double sa = a.Data.TryGetValue("_score", out var sv) ? Convert.ToDouble(sv) : 0;
                        double sb = b.Data.TryGetValue("_score", out var sw) ? Convert.ToDouble(sw) : 0;
                        return sb.CompareTo(sa);
                    });
                    break;
            }

            // Limit
            if (q.Limit.HasValue && list.Count > q.Limit.Value)
                list = list.Take(q.Limit.Value).ToList();

            return list;
        }

        private void AddToTagIndex(string eventId, string[] tags)
        {
            foreach (var tag in tags)
            {
                if (!_tagIndex.ContainsKey(tag))
                    _tagIndex[tag] = new HashSet<string>();
                _tagIndex[tag].Add(eventId);
            }
        }
    }
}
