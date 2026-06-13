using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLoop
{
    /// <summary>
    /// Global append-only record log. Used for debug, replay, and world-state inspection.
    /// Never queried during runtime game logic — per-entity RecordModule handles that.
    /// </summary>
    public class WorldLog
    {
        private readonly List<Record> _all = new List<Record>();

        public IReadOnlyList<Record> All => _all;
        public int Count => _all.Count;

        public void Append(Record record)
        {
            _all.Add(record);
        }

        public List<Record> Dump(int? sinceTick = null, int? untilTick = null)
        {
            IEnumerable<Record> query = _all;
            if (sinceTick.HasValue)
                query = query.Where(r => r.Tick >= sinceTick.Value);
            if (untilTick.HasValue)
                query = query.Where(r => r.Tick <= untilTick.Value);
            return query.OrderBy(r => r.Tick).ToList();
        }

        public List<Record> DumpByType(string type, int? sinceTick = null)
        {
            var query = _all.Where(r => r.Type == type);
            if (sinceTick.HasValue)
                query = query.Where(r => r.Tick >= sinceTick.Value);
            return query.OrderBy(r => r.Tick).ToList();
        }
    }
}
