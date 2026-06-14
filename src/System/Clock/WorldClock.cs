using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace GameLoop
{
    public class WorldClock
    {
        private readonly ClockConfig _config;
        private readonly Dictionary<int, string> _seasonNames;
        private readonly List<TimeBlockConfig> _timeBlocks;
        private int _tick;

        // === Events ===
        public event Action<TickSnapshot> OnTick;
        public event Action<TickSnapshot> OnTimeBlockChanged;
        public event Action<TickSnapshot> OnHourChanged;
        public event Action<TickSnapshot> OnDayChanged;
        public event Action<TickSnapshot> OnSeasonChanged;
        public event Action<TickSnapshot> OnYearChanged;

        // === Properties ===
        public int Tick => _tick;
        public int Minute => (_tick * _config.MinutesPerTick) % 60;
        public int Hour   => ((_tick * _config.MinutesPerTick) / 60) % 24;
        public int DayOfSeason => ((_tick * _config.MinutesPerTick) / 1440) % _config.DaysPerSeason + 1;
        public string Season => _seasonNames[SeasonIndex];
        public int SeasonIndex => ((_tick * _config.MinutesPerTick) / 1440 / _config.DaysPerSeason) % _config.Seasons.Count;
        public int Year => (_tick * _config.MinutesPerTick / 1440 / _config.DaysPerSeason / _config.Seasons.Count) + 1;
        public int Month => (SeasonIndex * 3) + ((DayOfSeason - 1) / 10) + 1;
        public int Second => 0;
        public string TimeBlock => GetTimeBlock(Hour);

        private WorldClock(ClockConfig config)
        {
            _config = config;
            _seasonNames = new Dictionary<int, string>();
            foreach (var s in config.Seasons)
                _seasonNames[s.Index] = s.Name;
            _timeBlocks = config.TimeBlocks.OrderBy(t => t.StartHour).ToList();
        }

        // === Factory ===
        public static WorldClock FromConfig(string jsonPath)
        {
            var json = File.ReadAllText(jsonPath);
            var config = JsonConvert.DeserializeObject<ClockConfig>(json);
            if (config == null)
                throw new ArgumentException("Failed to parse clock config");
            Validate(config);
            return new WorldClock(config);
        }

        // === Advance ===
        public void Advance(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                var oldBlock = TimeBlock;
                var oldHour = Hour;
                var oldDay = DayOfSeason;
                var oldSeason = SeasonIndex;
                var oldYear = Year;

                _tick++;

                var snap = Snapshot();
                OnTick?.Invoke(snap);

                if (TimeBlock != oldBlock)
                {
                    Logger.Debug("Clock", $"TimeBlock: {oldBlock} -> {TimeBlock}", new { tick = _tick });
                    OnTimeBlockChanged?.Invoke(snap);
                }

                if (Hour != oldHour)
                {
                    Logger.Debug("Clock", $"Hour: {oldHour} -> {Hour}", new { tick = _tick });
                    OnHourChanged?.Invoke(snap);
                }

                if (DayOfSeason != oldDay)
                    OnDayChanged?.Invoke(snap);

                if (SeasonIndex != oldSeason)
                    OnSeasonChanged?.Invoke(snap);

                if (Year != oldYear)
                    OnYearChanged?.Invoke(snap);
            }
        }

        // === Snapshot ===
        public TickSnapshot Snapshot()
        {
            return new TickSnapshot
            {
                Tick = _tick,
                Minute = Minute,
                Hour = Hour,
                Day = DayOfSeason,
                Month = Month,
                Second = Second,
                Season = Season,
                Year = Year,
                TimeBlock = TimeBlock
            };
        }

        // === Helpers ===
        private string GetTimeBlock(int hour)
        {
            foreach (var tb in _timeBlocks)
            {
                if (hour >= tb.StartHour && hour <= tb.EndHour)
                    return tb.Name;
            }
            return "???";
        }

        private static void Validate(ClockConfig config)
        {
            if (config.MinutesPerTick <= 0)
                throw new ArgumentException("minutes_per_tick must be > 0");
            if (config.DaysPerSeason <= 0)
                throw new ArgumentException("days_per_season must be > 0");
            if (config.Seasons == null || config.Seasons.Count == 0)
                throw new ArgumentException("seasons must not be empty");

            var indices = new HashSet<int>();
            foreach (var s in config.Seasons)
            {
                if (indices.Contains(s.Index))
                    throw new ArgumentException($"Duplicate season index: {s.Index}");
                indices.Add(s.Index);
            }

            if (config.TimeBlocks == null || config.TimeBlocks.Count == 0)
                throw new ArgumentException("time_blocks must not be empty");

            // Validate full 0-23 coverage
            var covered = new bool[24];
            foreach (var tb in config.TimeBlocks)
            {
                if (tb.StartHour < 0 || tb.StartHour > 23 || tb.EndHour < 0 || tb.EndHour > 23)
                    throw new ArgumentException($"TimeBlock hours must be 0-23: {tb.Name}");
                if (tb.StartHour > tb.EndHour)
                    throw new ArgumentException($"TimeBlock start > end: {tb.Name}");
                for (int h = tb.StartHour; h <= tb.EndHour; h++)
                    covered[h] = true;
            }
            for (int h = 0; h < 24; h++)
            {
                if (!covered[h])
                    throw new ArgumentException($"TimeBlocks do not cover hour {h}");
            }
        }
    }
}
