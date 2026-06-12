using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace GameLoop
{
    public class Scheduler : IEntityComponent
    {
        // Internal sorted list: items ordered by TriggerTick ascending
        private readonly List<ScheduleItem> _items = new List<ScheduleItem>();

        // Tag index: tag 閳?indices into _items
        private readonly Dictionary<string, HashSet<int>> _tagIndex = new Dictionary<string, HashSet<int>>();

        // === Registration ===

        public void Add(string actionType, string[] tags, Dictionary<string, object> parameters,
                        int? atTick = null, int? atHour = null,
                        int? everyTick = null, int? everyHour = null)
        {
            tags = tags ?? Array.Empty<string>();
            parameters = parameters ?? new Dictionary<string, object>();

            int? intervalTicks = null;
            int triggerTick;

            if (atTick.HasValue)
                triggerTick = atTick.Value;
            else if (atHour.HasValue)
                triggerTick = HourToTick(atHour.Value);
            else
                throw new ArgumentException("Must specify atTick, atHour, everyTick, or everyHour");

            if (everyTick.HasValue)
                intervalTicks = everyTick.Value;
            else if (everyHour.HasValue)
                intervalTicks = HourToTick(everyHour.Value);

            var item = new ScheduleItem
            {
                ActionType = actionType,
                Tags = tags,
                Params = new Dictionary<string, object>(parameters),
                TriggerTick = triggerTick,
                Interval = intervalTicks
            };

            Insert(item);
            Logger.Debug("Scheduler", $"Added: {actionType}", new { triggerTick = triggerTick, interval = intervalTicks, tags });
        }

        /// <summary>
        /// Add a pre-resolved action instance (used for child actions from parent Execute).
        /// The 'action' object will be stored as-is and passed back via ScheduleItem.ResolvedAction.
        /// </summary>
        public void AddResolved(object action, string actionType, int atTick)
        {
            var item = new ScheduleItem
            {
                ActionType = actionType,
                Tags = Array.Empty<string>(),
                Params = new Dictionary<string, object>(),
                TriggerTick = atTick,
                Interval = null,
                ResolvedAction = action
            };
            Insert(item);
            Logger.Debug("Scheduler", $"AddResolved: {actionType}", new { atTick });
        }

        // === Query ===

        public List<ScheduleItem> DueNow(int tick)
        {
            var result = new List<ScheduleItem>();
            // Items are sorted by TriggerTick, so collect from start while TriggerTick <= tick
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].TriggerTick <= tick)
                    result.Add(_items[i]);
                else
                    break;
            }
            return result;
        }

        public List<ScheduleItem> Upcoming(int count)
        {
            var result = new List<ScheduleItem>();
            for (int i = 0; i < _items.Count && result.Count < count; i++)
                result.Add(_items[i]);
            return result;
        }

        public List<ScheduleItem> GetByTags(string[] tags)
        {
            if (tags == null || tags.Length == 0)
                return new List<ScheduleItem>();

            var matchIndices = new HashSet<int>();
            bool first = true;

            foreach (var tag in tags)
            {
                if (!_tagIndex.TryGetValue(tag, out var set))
                    return new List<ScheduleItem>();

                if (first)
                {
                    matchIndices.UnionWith(set);
                    first = false;
                }
                else
                {
                    matchIndices.IntersectWith(set);
                }
            }

            return matchIndices.Select(i => _items[i]).ToList();
        }

        // === Check ===

        public List<ScheduleItem> Check(TickSnapshot snap)
        {
            var triggered = new List<ScheduleItem>();
            var toRemove = new List<int>();
            var toReinsert = new List<ScheduleItem>();

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].TriggerTick <= snap.Tick)
                {
                    var item = _items[i];
                    triggered.Add(item.Clone());
                    toRemove.Add(i);

                    if (!item.IsOneTime)
                    {
                        var reinsert = item.Clone();
                        reinsert.TriggerTick = item.TriggerTick + (item.Interval ?? 0);
                        toReinsert.Add(reinsert);
                    }
                }
                else
                {
                    break; // sorted 閳?no more due items
                }
            }

            // Remove triggered items (reverse order to preserve indices)
            for (int j = toRemove.Count - 1; j >= 0; j--)
                RemoveAt(toRemove[j]);

            // Reinsert repeats
            foreach (var ri in toReinsert)
                Insert(ri);

            if (triggered.Count > 0)
                Logger.Debug("Scheduler", $"Check: {triggered.Count} triggered", new { tick = snap.Tick, actions = triggered.Select(t => t.ActionType).ToList() });

            return triggered;
        }

        // === Management ===

        public void Remove(string actionType)
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].ActionType == actionType)
                    RemoveAt(i);
            }
        }

        public void Clear()
        {
            _items.Clear();
            _tagIndex.Clear();
        }

        // === Config ===

        public void LoadConfig(string jsonPath)
        {
            var json = File.ReadAllText(jsonPath);
            var config = JsonConvert.DeserializeObject<ScheduleConfig>(json);
            if (config?.Items == null) return;

            foreach (var c in config.Items)
            {
                Add(c.ActionType, c.Tags, c.Params,
                    atTick: c.AtTick, atHour: c.AtHour,
                    everyTick: c.EveryTick, everyHour: c.EveryHour);
            }
        }

        // === Internal ===

        private void Insert(ScheduleItem item)
        {
            // Find insertion point (keep sorted by TriggerTick)
            int idx = 0;
            while (idx < _items.Count && _items[idx].TriggerTick <= item.TriggerTick)
                idx++;

            _items.Insert(idx, item);

            // Update tag index 閳?shift all indices >= idx
            foreach (var kv in _tagIndex)
            {
                var shifted = new HashSet<int>();
                foreach (var i in kv.Value)
                    shifted.Add(i >= idx ? i + 1 : i);
                kv.Value.Clear();
                foreach (var s in shifted)
                    kv.Value.Add(s);
            }

            // Add new item's tags
            foreach (var tag in item.Tags)
            {
                if (!_tagIndex.ContainsKey(tag))
                    _tagIndex[tag] = new HashSet<int>();
                _tagIndex[tag].Add(idx);
            }
        }

        private void RemoveAt(int idx)
        {
            _items.RemoveAt(idx);

            // Update tag index
            var emptyTags = new List<string>();
            foreach (var kv in _tagIndex)
            {
                kv.Value.Remove(idx);
                var shifted = new HashSet<int>();
                foreach (var i in kv.Value)
                    shifted.Add(i > idx ? i - 1 : i);
                kv.Value.Clear();
                foreach (var s in shifted)
                    kv.Value.Add(s);
                if (kv.Value.Count == 0)
                    emptyTags.Add(kv.Key);
            }
            foreach (var t in emptyTags)
                _tagIndex.Remove(t);
        }

        private static int HourToTick(int hour)
        {
            return hour * 6;
        }

        public void OnTick(TickContext ctx) { }
    }
}
