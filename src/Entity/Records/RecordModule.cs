using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameLoop
{
    // ================================================================
    // Data Models
    // ================================================================

    public class RecordSource
    {
        public string Method { get; set; } = "";
        public string FromNpcId { get; set; }
        public float Reliability { get; set; } = 1f;
        public List<SourceRevision> History { get; set; } = new List<SourceRevision>();
    }

    public class SourceRevision
    {
        public string Method { get; set; } = "";
        public string NewMethod { get; set; } = "";
        public float NewReliability { get; set; }
        public int UpdatedAt { get; set; }
        public string Reason { get; set; } = "";
    }

    public class Record
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string[] Tags { get; set; } = Array.Empty<string>();
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
        public int Tick { get; set; }
        public RecordSource Source { get; set; } = new RecordSource();
    }

    // ================================================================
    // Component
    // ================================================================

    /// <summary>
    /// Per-entity memory of events this NPC knows about.
    /// All records are stored locally; no global event store needed for queries.
    /// Source is fully caller-defined: self, witnessed, overheard, told, rumored...
    /// </summary>
    public class RecordModule : IEntityComponent
    {
        private readonly List<Record> _records = new List<Record>();
        private readonly Dictionary<string, List<int>> _tagIndex = new Dictionary<string, List<int>>();

        public IReadOnlyList<Record> AllRecords => _records;

        public void OnTick(TickContext ctx) { /* event-driven */ }

        // ================================================================
        // Write
        // ================================================================

        public Record Remember(RecordSource source, string type, string[] tags,
                               Dictionary<string, object> data, int tick)
        {
            var record = new Record
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Type = type,
                Tags = tags ?? Array.Empty<string>(),
                Data = data ?? new Dictionary<string, object>(),
                Tick = tick,
                Source = source,
            };
            _records.Add(record);
            AddToIndex(_records.Count - 1, record.Tags);
            return record;
        }

        public Record Revise(string recordId, RecordSource newSource, int tick, string reason)
        {
            var idx = FindIndex(recordId);
            if (idx < 0) return null;

            var record = _records[idx];
            var old = record.Source;
            old.History.Add(new SourceRevision
            {
                Method = old.Method,
                NewMethod = newSource.Method,
                NewReliability = newSource.Reliability,
                UpdatedAt = tick,
                Reason = reason,
            });
            record.Source = newSource;
            return record;
        }

        public Record FindExisting(string type, string[] tags, Dictionary<string, object> dataMatch)
        {
            if (dataMatch == null || dataMatch.Count == 0) return null;
            return _records.FirstOrDefault(r =>
                r.Type == type &&
                (tags == null || tags.All(t => r.Tags.Contains(t))) &&
                dataMatch.All(kv => r.Data.TryGetValue(kv.Key, out var v) && Equals(v, kv.Value)));
        }

        // ================================================================
        // Query
        // ================================================================

        public List<Record> Recall(string[] tags, int recentDays, int? limit)
        {
            var cutoffTick = recentDays > 0 ? GetCutoffTick(recentDays) : 0;
            IEnumerable<Record> candidates;

            if (tags != null && tags.Length > 0)
            {
                var ids = new HashSet<int>();
                foreach (var tag in tags)
                    if (_tagIndex.TryGetValue(tag, out var indices))
                        ids.UnionWith(indices);
                candidates = ids.Select(i => _records[i]);
            }
            else
            {
                candidates = _records;
            }

            var result = candidates
                .Where(r => r.Tick >= cutoffTick)
                .OrderByDescending(r => r.Tick)
                .AsEnumerable();

            if (limit.HasValue)
                result = result.Take(limit.Value);

            return result.ToList();
        }

        public List<Record> Recent(int count)
        {
            return _records.OrderByDescending(r => r.Tick).Take(count).ToList();
        }

        public List<Record> RecallByMethod(string method, int recentDays, int? limit)
        {
            var cutoffTick = GetCutoffTick(recentDays);
            var query = _records
                .Where(r => r.Tick >= cutoffTick && r.Source.Method == method)
                .OrderByDescending(r => r.Tick);

            IEnumerable<Record> result = query;
            if (limit.HasValue)
                result = query.Take(limit.Value);

            return result.ToList();
        }

        public List<Record> RecallByReliability(float minReliability, int recentDays, int? limit)
        {
            var cutoffTick = GetCutoffTick(recentDays);
            var query = _records
                .Where(r => r.Tick >= cutoffTick && r.Source.Reliability >= minReliability)
                .OrderByDescending(r => r.Tick);

            IEnumerable<Record> result = query;
            if (limit.HasValue)
                result = query.Take(limit.Value);

            return result.ToList();
        }

        // ================================================================
        // Forget
        // ================================================================

        public int ForgetExcept(string[] keepTags, int olderThanDays)
        {
            var cutoffTick = GetCutoffTick(olderThanDays);
            var toRemove = new List<int>();

            for (int i = _records.Count - 1; i >= 0; i--)
            {
                if (_records[i].Tick < cutoffTick && !_records[i].Tags.Any(t => keepTags.Contains(t)))
                    toRemove.Add(i);
            }

            RemoveIndices(toRemove);
            return toRemove.Count;
        }

        public int ForgetTags(string[] dropTags, int olderThanDays)
        {
            var cutoffTick = GetCutoffTick(olderThanDays);
            var toRemove = new List<int>();

            for (int i = _records.Count - 1; i >= 0; i--)
            {
                if (_records[i].Tick < cutoffTick && _records[i].Tags.Any(t => dropTags.Contains(t)))
                    toRemove.Add(i);
            }

            RemoveIndices(toRemove);
            return toRemove.Count;
        }

        public int ForgetOlder(int olderThanDays)
        {
            var cutoffTick = GetCutoffTick(olderThanDays);
            var toRemove = new List<int>();

            for (int i = _records.Count - 1; i >= 0; i--)
            {
                if (_records[i].Tick < cutoffTick)
                    toRemove.Add(i);
            }

            RemoveIndices(toRemove);
            return toRemove.Count;
        }

        // ================================================================
        // Prompt text
        // ================================================================

        public string Summarize(string[] tags, int recentDays, int maxRecords)
        {
            var records = Recall(tags, recentDays, maxRecords);
            if (records.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("【近期记忆】");
            foreach (var r in records)
            {
                var src = r.Source.Method == "self" ? "" : $" (来源:{r.Source.Method}, 可靠:{r.Source.Reliability:F1})";
                sb.AppendLine($"  · {r.Type}{src} @ T{r.Tick}");
            }
            return sb.ToString();
        }

        // ================================================================
        // Helpers
        // ================================================================

        private int FindIndex(string recordId)
        {
            for (int i = _records.Count - 1; i >= 0; i--)
                if (_records[i].Id == recordId)
                    return i;
            return -1;
        }

        private int GetCutoffTick(int recentDays)
        {
            if (recentDays <= 0) return 0;
            var latest = _records.Count > 0 ? _records.Max(r => r.Tick) : 0;
            return Math.Max(0, latest - recentDays * 144);
        }

        private void AddToIndex(int idx, string[] tags)
        {
            foreach (var tag in tags)
            {
                if (!_tagIndex.ContainsKey(tag))
                    _tagIndex[tag] = new List<int>();
                _tagIndex[tag].Add(idx);
            }
        }

        private void RemoveIndices(List<int> sortedDesc)
        {
            if (sortedDesc.Count == 0) return;
            sortedDesc.Sort((a, b) => b.CompareTo(a));
            foreach (var idx in sortedDesc)
                _records.RemoveAt(idx);
            RebuildIndex();
        }

        private void RebuildIndex()
        {
            _tagIndex.Clear();
            for (int i = 0; i < _records.Count; i++)
                AddToIndex(i, _records[i].Tags);
        }
    }
}
