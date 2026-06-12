using System;
using System.Collections.Generic;
using System.IO;

class Program
{
    static int pass = 0, fail = 0;
    static string root;

    static void Main()
    {
        root = FindRoot();
        Console.WriteLine($"Root: {root}\n");

        TestTickAdvancement();
        TestDerivedProperties();
        TestSeasonYearBoundaries();
        TestTimeBlockDerivation();
        TestEventFiring();
        TestEventOrderMultiBoundary();
        TestSnapshotState();
        TestFromConfig();
        TestConfigValidation();

        Console.WriteLine($"\n=== {pass} pass, {fail} fail ===");
        Environment.Exit(fail > 0 ? 1 : 0);
    }

    static WorldClock LoadClock() =>
        WorldClock.FromConfig(Path.Combine(root, "configs", "clock", "world_clock.json"));

    static void TestTickAdvancement()
    {
        Console.WriteLine("--- Tick Advancement ---");
        var c = LoadClock();
        Assert(0, c.Tick, "init");
        c.Advance(1);
        Assert(1, c.Tick, "advance 1");
        c.Advance(10);
        Assert(11, c.Tick, "advance 10");
    }

    static void TestDerivedProperties()
    {
        Console.WriteLine("--- Derived Properties ---");
        var c = LoadClock();
        Assert(0, c.Minute, "tick0 min");
        Assert(0, c.Hour, "tick0 hour");
        Assert(1, c.DayOfSeason, "tick0 day");
        Assert("Spring", c.Season, "tick0 season");
        Assert(1, c.Year, "tick0 year");

        c.Advance(6);
        Assert(0, c.Minute, "tick6 min");
        Assert(1, c.Hour, "tick6 hour");

        c.Advance(6);
        Assert(2, c.Hour, "tick12 hour");

        var c2 = LoadClock();
        c2.Advance(144);
        Assert(2, c2.DayOfSeason, "day2");
        Assert(0, c2.Hour, "day2 hour0");
    }

    static void TestSeasonYearBoundaries()
    {
        Console.WriteLine("--- Season/Year Boundaries ---");
        var ticksPerDay = 24 * 60 / 10;

        var c = LoadClock();
        c.Advance(ticksPerDay * 30 - 1);
        Assert(30, c.DayOfSeason, "season end day");
        Assert("Spring", c.Season, "still spring");
        c.Advance(1);
        Assert(1, c.DayOfSeason, "new season day1");
        Assert("Summer", c.Season, "now summer");

        var c2 = LoadClock();
        c2.Advance(ticksPerDay * 30 * 4);
        Assert(1, c2.DayOfSeason, "year wrap day");
        Assert("Spring", c2.Season, "year wrap spring");
        Assert(2, c2.Year, "year 2");
    }

    static void TestTimeBlockDerivation()
    {
        Console.WriteLine("--- TimeBlock ---");
        var c = LoadClock();
        Assert("鍑屾櫒", c.TimeBlock, "hour0");
        c.Advance(36); Assert("鏃╂櫒", c.TimeBlock, "hour6");
        c.Advance(18); Assert("涓婂崍", c.TimeBlock, "hour9");
        c.Advance(18); Assert("鍗堝悗", c.TimeBlock, "hour12");
        c.Advance(12); Assert("涓嬪崍", c.TimeBlock, "hour14");
        c.Advance(24); Assert("鍌嶆櫄", c.TimeBlock, "hour18");
        c.Advance(12); Assert("澶滄櫄", c.TimeBlock, "hour20");
        c.Advance(18); Assert("澶滄櫄", c.TimeBlock, "hour23");
    }

    static void TestEventFiring()
    {
        Console.WriteLine("--- Event Firing ---");
        var c = LoadClock();
        c.Advance(5); // tick 5, still hour 0

        int tickCount = 0, hourCount = 0, blockCount = 0, dayCount = 0;
        c.OnTick += _ => tickCount++;
        c.OnHourChanged += _ => hourCount++;
        c.OnTimeBlockChanged += _ => blockCount++;
        c.OnDayChanged += _ => dayCount++;

        // Advance 3 ticks: tick 5鈫?. At tick 6, hour 0鈫?.
        c.Advance(3);
        Assert(3, tickCount, "3 ticks fired");
        Assert(1, hourCount, "1 hour change (5鈫? crosses hour 1)");
        Assert(0, blockCount, "no block change");
        Assert(0, dayCount, "no day change");

        // Now advance within same hour
        tickCount = hourCount = 0;
        c.Advance(3); // tick 8鈫?1, stays in hour 1
        Assert(3, tickCount, "3 more ticks");
        Assert(0, hourCount, "no hour change this time");
    }

    static void TestEventOrderMultiBoundary()
    {
        Console.WriteLine("--- Event Order ---");
        var c = LoadClock();
        var events = new List<string>();
        c.OnTimeBlockChanged += _ => events.Add("block");
        c.OnHourChanged += _ => events.Add("hour");
        c.OnDayChanged += _ => events.Add("day");
        c.OnSeasonChanged += _ => events.Add("season");

        var ticksPerDay = 24 * 60 / 10;
        c.Advance(ticksPerDay * 30 - 2);
        events.Clear();
        c.Advance(2); // cross day+season boundary

        int hourIdx = events.IndexOf("hour");
        int dayIdx = events.IndexOf("day");
        int seasonIdx = events.IndexOf("season");
        Assert(hourIdx >= 0 && dayIdx >= 0 && seasonIdx >= 0, "all three fired");
        Assert(hourIdx < dayIdx, "hour before day");
        Assert(dayIdx < seasonIdx, "day before season");
    }

    static void TestSnapshotState()
    {
        Console.WriteLine("--- Snapshot State ---");
        var c = LoadClock();
        c.Advance(36);
        var snap = c.Snapshot();
        Assert(36, snap.Tick, "snap tick");
        Assert(6, snap.Hour, "snap hour");
        Assert(0, snap.Minute, "snap min");
        Assert(1, snap.Day, "snap day");
        Assert("Spring", snap.Season, "snap season");
        Assert("鏃╂櫒", snap.TimeBlock, "snap block");

        TickSnapshot? eventSnap = null;
        c.OnDayChanged += s => eventSnap = s;
        c.Advance(24 * 60 / 10 * 30);
        Assert(1, eventSnap?.Day ?? -1, "event snap shows new day");
    }

    static void TestFromConfig()
    {
        Console.WriteLine("--- FromConfig ---");
        var c = LoadClock();
        Assert(c != null, "clock loaded");
        Assert(0, c.Tick, "starts at 0");

        try
        {
            WorldClock.FromConfig(Path.Combine(root, "nonexistent.json"));
            Assert(false, "should have thrown");
        }
        catch (Exception)
        {
            Assert(true, "missing file throws");
        }
    }

    static void TestConfigValidation()
    {
        Console.WriteLine("--- Config Validation ---");

        var badBlocks = new ClockConfig
        {
            MinutesPerTick = 10, DaysPerSeason = 30,
            Seasons = new List<SeasonConfig> { new SeasonConfig { Name = "S", Index = 0 } },
            TimeBlocks = new List<TimeBlockConfig>
            {
                new TimeBlockConfig { Name = "A", StartHour = 0, EndHour = 5 },
                new TimeBlockConfig { Name = "B", StartHour = 7, EndHour = 23 }
            }
        };
        try { ValidateConfig(badBlocks); Assert(false, "gap should fail"); }
        catch (ArgumentException) { Assert(true, "gap detected"); }

        var badSeasons = new ClockConfig
        {
            MinutesPerTick = 10, DaysPerSeason = 30,
            Seasons = new List<SeasonConfig>
            {
                new SeasonConfig { Name = "A", Index = 0 },
                new SeasonConfig { Name = "B", Index = 0 }
            },
            TimeBlocks = new List<TimeBlockConfig>
            {
                new TimeBlockConfig { Name = "All", StartHour = 0, EndHour = 23 }
            }
        };
        try { ValidateConfig(badSeasons); Assert(false, "dup season should fail"); }
        catch (ArgumentException) { Assert(true, "dup season detected"); }
    }

    static void ValidateConfig(ClockConfig config)
    {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, Newtonsoft.Json.JsonConvert.SerializeObject(config));
        try { WorldClock.FromConfig(tmp); }
        finally { File.Delete(tmp); }
    }

    static void Assert(bool cond, string msg)
    {
        if (cond) { pass++; Console.WriteLine($"  [PASS] {msg}"); }
        else { fail++; Console.WriteLine($"  [FAIL] {msg}"); }
    }

    static void Assert(int expected, int actual, string msg)
    {
        Assert(expected == actual, $"{msg}: expected {expected}, got {actual}");
    }

    static void Assert(string expected, string actual, string msg)
    {
        Assert(expected == actual, $"{msg}: expected '{expected}', got '{actual}'");
    }

    static string FindRoot()
    {
        var d = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (d != null)
        {
            if (Directory.Exists(Path.Combine(d.FullName, "configs"))) return d.FullName;
            d = d.Parent;
        }
        throw new Exception("root not found");
    }
}
