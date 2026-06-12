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

        TestAddAtTick();
        TestAddAtHour();
        TestAddEveryTick();
        TestAddEveryHour();
        TestAddNoTiming();
        TestDueNow();
        TestDueNowNotYetDue();
        TestDueNowOneTimeConsumed();
        TestUpcoming();
        TestUpcomingLimit();
        TestGetByTags();
        TestGetByTagsMulti();
        TestRemove();
        TestRemoveNonExistent();
        TestClear();
        TestCheck();
        TestCheckRepeating();
        TestLoadConfig();

        Console.WriteLine($"\n=== {pass} pass, {fail} fail ===");
        Environment.Exit(fail > 0 ? 1 : 0);
    }

    // === Add ===
    static void TestAddAtTick()
    {
        Console.WriteLine("--- Add atTick ---");
        var s = new Scheduler();
        s.Add("goto", new[]{"daily"}, new Dictionary<string,object>{{"location","workshop"}}, atTick: 36);
        var items = s.Upcoming(1);
        Assert(1, items.Count, "item added");
        Assert("goto", items[0].ActionType, "action type");
        Assert(36, items[0].TriggerTick, "trigger tick");
    }

    static void TestAddAtHour()
    {
        Console.WriteLine("--- Add atHour ---");
        var s = new Scheduler();
        s.Add("goto", new[]{"daily"}, new Dictionary<string,object>{{"location","tavern"}}, atHour: 12);
        var items = s.Upcoming(1);
        Assert(72, items[0].TriggerTick, "hour 12 = tick 72");
    }

    static void TestAddEveryTick()
    {
        Console.WriteLine("--- Add everyTick ---");
        var s = new Scheduler();
        s.Add("spawn", new[]{"combat"}, new Dictionary<string,object>{{"monster","slime"}}, atTick: 0, everyTick: 12);
        var items = s.Upcoming(1);
        Assert(0, items[0].TriggerTick, "first at 0");
        Assert(12, items[0].Interval ?? -1, "interval 12");
    }

    static void TestAddEveryHour()
    {
        Console.WriteLine("--- Add everyHour ---");
        var s = new Scheduler();
        s.Add("spawn", new[]{"combat"}, new Dictionary<string,object>{{"monster","boss"}}, atHour: 0, everyHour: 24);
        var items = s.Upcoming(1);
        Assert(0, items[0].TriggerTick, "first at 0");
        Assert(144, items[0].Interval ?? -1, "24h = 144 ticks");
    }

    static void TestAddNoTiming()
    {
        Console.WriteLine("--- Add no timing ---");
        var s = new Scheduler();
        try
        {
            s.Add("idle", new string[0], new Dictionary<string,object>());
            Assert(false, "should throw");
        }
        catch (ArgumentException)
        {
            Assert(true, "throws on no timing");
        }
    }

    // === DueNow ===
    static void TestDueNow()
    {
        Console.WriteLine("--- DueNow ---");
        var s = new Scheduler();
        s.Add("a", new string[0], new Dictionary<string,object>(), atTick: 50);
        s.Add("b", new string[0], new Dictionary<string,object>(), atTick: 72);
        s.Add("c", new string[0], new Dictionary<string,object>(), atTick: 100);

        var due = s.DueNow(72);
        Assert(2, due.Count, "2 items due");
        Assert("a", due[0].ActionType, "first a");
        Assert("b", due[1].ActionType, "second b");
    }

    static void TestDueNowNotYetDue()
    {
        Console.WriteLine("--- DueNow not yet due ---");
        var s = new Scheduler();
        s.Add("a", new string[0], new Dictionary<string,object>(), atTick: 100);
        var due = s.DueNow(50);
        Assert(0, due.Count, "nothing due");
    }

    static void TestDueNowOneTimeConsumed()
    {
        Console.WriteLine("--- DueNow one-time consumed ---");
        var s = new Scheduler();
        s.Add("a", new string[0], new Dictionary<string,object>(), atTick: 50);
        s.Check(new TickSnapshot{Tick=50});
        var due = s.DueNow(100);
        Assert(0, due.Count, "consumed item gone");
    }

    // === Upcoming ===
    static void TestUpcoming()
    {
        Console.WriteLine("--- Upcoming ---");
        var s = new Scheduler();
        s.Add("a", new string[0], new Dictionary<string,object>(), atTick: 144);
        s.Add("b", new string[0], new Dictionary<string,object>(), atTick: 72);
        s.Add("c", new string[0], new Dictionary<string,object>(), atTick: 108);

        var up = s.Upcoming(3);
        Assert(3, up.Count, "all 3 returned");
        Assert(72,  up[0].TriggerTick, "first by tick");
        Assert(108, up[1].TriggerTick, "second by tick");
        Assert(144, up[2].TriggerTick, "third by tick");
    }

    static void TestUpcomingLimit()
    {
        Console.WriteLine("--- Upcoming limit ---");
        var s = new Scheduler();
        s.Add("a", new string[0], new Dictionary<string,object>(), atTick: 10);
        s.Add("b", new string[0], new Dictionary<string,object>(), atTick: 20);
        s.Add("c", new string[0], new Dictionary<string,object>(), atTick: 30);
        var up = s.Upcoming(2);
        Assert(2, up.Count, "limited to 2");
    }

    // === Tags ===
    static void TestGetByTags()
    {
        Console.WriteLine("--- GetByTags single ---");
        var s = new Scheduler();
        s.Add("work", new[]{"daily","work"}, new Dictionary<string,object>(), atTick: 10);
        s.Add("rest", new[]{"daily","rest"}, new Dictionary<string,object>(), atTick: 20);
        s.Add("combat", new[]{"combat"}, new Dictionary<string,object>(), atTick: 30);

        var result = s.GetByTags(new[]{"work"});
        Assert(1, result.Count, "1 work item");
        Assert("work", result[0].ActionType, "work action");
    }

    static void TestGetByTagsMulti()
    {
        Console.WriteLine("--- GetByTags multi ---");
        var s = new Scheduler();
        s.Add("a", new[]{"daily","work"}, new Dictionary<string,object>(), atTick: 10);
        s.Add("b", new[]{"daily","rest"}, new Dictionary<string,object>(), atTick: 20);
        s.Add("c", new[]{"combat"}, new Dictionary<string,object>(), atTick: 30);

        var result = s.GetByTags(new[]{"daily"});
        Assert(2, result.Count, "2 daily items");
    }

    // === Remove / Clear ===
    static void TestRemove()
    {
        Console.WriteLine("--- Remove ---");
        var s = new Scheduler();
        s.Add("goto", new[]{"daily"}, new Dictionary<string,object>(), atTick: 10);
        s.Add("social", new[]{"daily"}, new Dictionary<string,object>(), atTick: 20);
        s.Remove("goto");
        var up = s.Upcoming(5);
        Assert(1, up.Count, "1 remaining");
        Assert("social", up[0].ActionType, "social remains");
    }

    static void TestRemoveNonExistent()
    {
        Console.WriteLine("--- Remove non-existent ---");
        var s = new Scheduler();
        s.Add("a", new[]{"tag"}, new Dictionary<string,object>(), atTick: 10);
        s.Remove("nonexistent");
        Assert(1, s.Upcoming(1).Count, "still there");
    }

    static void TestClear()
    {
        Console.WriteLine("--- Clear ---");
        var s = new Scheduler();
        s.Add("a", new[]{"t1"}, new Dictionary<string,object>(), atTick: 10);
        s.Add("b", new[]{"t2"}, new Dictionary<string,object>(), atTick: 20);
        s.Clear();
        Assert(0, s.Upcoming(10).Count, "empty");
        Assert(0, s.GetByTags(new[]{"t1"}).Count, "tag index empty");
    }

    // === Check ===
    static void TestCheck()
    {
        Console.WriteLine("--- Check ---");
        var s = new Scheduler();
        s.Add("goto", new[]{"daily"}, new Dictionary<string,object>{{"loc","A"}}, atTick: 50);
        s.Add("rest", new[]{"daily"}, new Dictionary<string,object>{{"loc","B"}}, atTick: 100);

        var triggered = s.Check(new TickSnapshot{Tick=60});
        Assert(1, triggered.Count, "1 triggered");
        Assert("goto", triggered[0].ActionType, "goto triggered");
        Assert("A", triggered[0].Params["loc"].ToString(), "param preserved");

        Assert(1, s.Upcoming(1).Count, "1 still in queue");
    }

    static void TestCheckRepeating()
    {
        Console.WriteLine("--- Check repeating ---");
        var s = new Scheduler();
        s.Add("spawn", new[]{"combat"}, new Dictionary<string,object>{{"m","slime"}}, atTick: 0, everyTick: 12);

        // First trigger at 0
        var t1 = s.Check(new TickSnapshot{Tick=0});
        Assert(1, t1.Count, "first trigger");

        // Should have re-enqueued at 12
        var up = s.Upcoming(1);
        Assert(12, up[0].TriggerTick, "re-enqueued at 12");
        Assert("spawn", up[0].ActionType, "same action type");

        // Second trigger at 12
        var t2 = s.Check(new TickSnapshot{Tick=12});
        Assert(1, t2.Count, "second trigger");
        Assert(24, s.Upcoming(1)[0].TriggerTick, "re-enqueued at 24");
    }

    // === Config ===
    static void TestLoadConfig()
    {
        Console.WriteLine("--- LoadConfig ---");
        var s = new Scheduler();
        s.LoadConfig(Path.Combine(root, "configs", "schedules", "blacksmith.json"));
        var up = s.Upcoming(10);
        Assert(5, up.Count, "5 items loaded");
        Assert("goto", up[0].ActionType, "first is goto");
    }

    // === Helpers ===
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
