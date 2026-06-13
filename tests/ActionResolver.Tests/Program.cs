using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

class Program
{
    static int pass = 0, fail = 0;
    static string root;

    static void Main()
    {
        root = FindRoot();
        Console.WriteLine($"Root: {root}\n");

        TestJsonLoadMapping();
        TestGenericActionSteps();
        TestTemplateSubstitution();
        TestGotoAction();
        TestArriveAction();
        TestTakeDamageAction();
        TestTakeDamageDeathChain();
        TestInterrupt();
        TestCurrentState();
        TestCSharpOverridesJson();

        Console.WriteLine($"\n=== {pass} pass, {fail} fail ===");
        Environment.Exit(fail > 0 ? 1 : 0);
    }

    static Attributes Attrs => new Attributes();
    static RecordModule Records => new RecordModule();
    static Scheduler Sched => new Scheduler();
    static TickSnapshot Snap => new TickSnapshot { Tick = 100 };

    // === JSON LoadMapping ===
    static void TestJsonLoadMapping()
    {
        Console.WriteLine("--- JSON LoadMapping ---");
        var ar = new ActionResolver();
        ar.LoadMapping(Path.Combine(root, "configs", "actions", "guard.json"));

        var ctx = new ActionContext { Attrs = Attrs, Records = Records, Scheduler = Sched, Snap = Snap, Params = new Dictionary<string, object>() };
        var item = new ActionItem { ActionType = "ignore", Params = new Dictionary<string, object>() };
        var result = ar.Execute(item, ctx.Attrs, ctx.Scheduler, ctx.Snap, ctx.Records);
        Assert(result.Completed, "ignore completed");
    }

    // === GenericAction Steps ===
    static void TestGenericActionSteps()
    {
        Console.WriteLine("--- GenericAction steps ---");
        var ar = new ActionResolver();
        ar.LoadMapping(Path.Combine(root, "configs", "actions", "guard.json"));

        var attrs = Attrs;
        var records = Records;
        var ctx = new ActionContext { Attrs = attrs, Records = records, Scheduler = Sched, Snap = Snap, Params = new Dictionary<string, object>() };
        var item = new ActionItem { ActionType = "alert", Params = new Dictionary<string, object>() };
        ar.Execute(item, ctx.Attrs, ctx.Scheduler, ctx.Snap, ctx.Records);

        var alert = attrs.Get("alert_state");
        Assert(alert != null, "alert_state set");
        Assert(Convert.ToInt64(alert.Value) == 1, "alert_state=1");
    }

    static void TestTemplateSubstitution()
    {
        Console.WriteLine("--- Template substitution ---");
        var ar = new ActionResolver();
        var mapping = new ActionMapping
        {
            Steps = new List<ActionStep>
            {
                new ActionStep { Type = "set_attr", Field = "target", Value = "{params.location}" }
            }
        };
        ar.Register(new GenericAction("go", mapping));

        var attrs = Attrs;
        var ctx = new ActionContext { Attrs = attrs, Records = Records, Scheduler = Sched, Snap = Snap,
            Params = new Dictionary<string, object> { { "location", "闁版帡顩? } } };
        var item = new ActionItem { ActionType = "go", Params = new Dictionary<string, object> { { "location", "闁版帡顩? } } };
        ar.Execute(item, ctx.Attrs, ctx.Scheduler, ctx.Snap, ctx.Records);

        var target = attrs.Get("target");
        Assert(target.Value.ToString() == "闁版帡顩?, "template resolved");
    }

    // === GotoAction ===
    static void TestGotoAction()
    {
        Console.WriteLine("--- GotoAction ---");
        var ar = new ActionResolver();
        ar.Register(new GotoAction());

        var attrs = Attrs;
        attrs.Set("location", "闁句礁灏冮柧?, "string", new string[0]);
        var records = Records;
        var sched = Sched;
        var ctx = new ActionContext { Attrs = attrs, Records = records, Scheduler = sched, Snap = Snap,
            Params = new Dictionary<string, object> { { "location", "闁版帡顩? } } };
        var item = new ActionItem { ActionType = "goto", Params = new Dictionary<string, object> { { "location", "闁版帡顩? } } };
        ar.Execute(item, ctx.Attrs, ctx.Scheduler, ctx.Snap, ctx.Records);

        var moving = attrs.Get("moving");
        Assert(Convert.ToInt64(moving.Value) == 1, "moving=1");
        Assert(attrv(attrs, "prev_location") == "闁句礁灏冮柧?, "prev_location saved");

        // Check child action scheduled
        var upcoming = sched.Upcoming(5);
        Assert(upcoming.Any(u => u.ActionType == "arrive"), "arrive scheduled");
    }

    static void TestArriveAction()
    {
        Console.WriteLine("--- ArriveAction ---");
        var ar = new ActionResolver();
        ar.Register(new ArriveAction { Location = "闁版帡顩? });

        var attrs = Attrs;
        attrs.Set("moving", 1, "number", new[]{"travel"});
        var ctx = new ActionContext { Attrs = attrs, Records = Records, Scheduler = Sched, Snap = Snap,
            Params = new Dictionary<string, object>() };
        var item = new ActionItem { ActionType = "arrive", Params = new Dictionary<string, object>() };
        ar.Execute(item, ctx.Attrs, ctx.Scheduler, ctx.Snap, ctx.Records);

        Assert(attrv(attrs, "location") == "闁版帡顩?, "location updated");
        Assert(Convert.ToInt64(attrs.Get("moving").Value) == 0, "moving=0");
    }

    // === TakeDamage ===
    static void TestTakeDamageAction()
    {
        Console.WriteLine("--- TakeDamageAction ---");
        var ar = new ActionResolver();
        ar.Register(new TakeDamageAction());

        var attrs = Attrs;
        attrs.Set("hp", 100, "number", new[]{"hp"});
        var ctx = new ActionContext { Attrs = attrs, Records = Records, Scheduler = Sched, Snap = Snap,
            Params = new Dictionary<string, object> { { "amount", 30 } } };
        var item = new ActionItem { ActionType = "take_damage", Params = new Dictionary<string, object> { { "amount", 30 } } };
        ar.Execute(item, ctx.Attrs, ctx.Scheduler, ctx.Snap, ctx.Records);

        Assert(attrv(attrs, "hp") == "70", "hp=70 after 30 damage");
        Assert(attrs.Get("dead") == null, "not dead");
    }

    static void TestTakeDamageDeathChain()
    {
        Console.WriteLine("--- TakeDamage death chain ---");
        var ar = new ActionResolver();
        ar.Register(new TakeDamageAction());

        var attrs = Attrs;
        attrs.Set("hp", 20, "number", new[]{"hp"});
        var ctx = new ActionContext { Attrs = attrs, Records = Records, Scheduler = Sched, Snap = Snap,
            Params = new Dictionary<string, object> { { "amount", 30 } } };
        var item = new ActionItem { ActionType = "take_damage", Params = new Dictionary<string, object> { { "amount", 30 } } };
        ar.Execute(item, ctx.Attrs, ctx.Scheduler, ctx.Snap, ctx.Records);

        Assert(Convert.ToInt64(attrs.Get("dead").Value) == 1, "dead=1 after fatal damage");
    }

    // === Interrupt ===
    static void TestInterrupt()
    {
        Console.WriteLine("--- Interrupt ---");
        var ar = new ActionResolver();
        ar.Register(new GotoAction());

        var attrs = Attrs;
        attrs.Set("moving", 1, "number", new[]{"travel"});
        var ctx = new ActionContext { Attrs = attrs, Records = Records, Scheduler = Sched, Snap = Snap,
            Params = new Dictionary<string, object> { { "location", "闁版帡顩? } } };
        var item = new ActionItem { ActionType = "goto", Params = new Dictionary<string, object> { { "location", "闁版帡顩? } } };
        ar.Execute(item, ctx.Attrs, ctx.Scheduler, ctx.Snap, ctx.Records);

        // Interrupt
        ar.Interrupt(attrs, Sched, Snap, Records);
        Assert(Convert.ToInt64(attrs.Get("moving").Value) == 0, "moving=0 after interrupt");
    }

    // === Current State ===
    static void TestCurrentState()
    {
        Console.WriteLine("--- Current State ---");
        var ar = new ActionResolver();
        ar.Register(new GotoAction());

        Assert(ar.Current == null, "null when idle");

        var attrs = Attrs;
        var ctx = new ActionContext { Attrs = attrs, Records = Records, Scheduler = Sched, Snap = Snap,
            Params = new Dictionary<string, object> { { "location", "闁版帡顩? } } };
        var item = new ActionItem { ActionType = "goto", Params = new Dictionary<string, object> { { "location", "闁版帡顩? } } };
        ar.Execute(item, ctx.Attrs, ctx.Scheduler, ctx.Snap, ctx.Records);

        // GotoAction yields child actions, so _current stays set
        var cur = ar.Current;
        Assert(cur != null, "current not null");
        Assert(cur.ActionType == "goto", "current is goto");
    }

    // === C# overrides JSON ===
    static void TestCSharpOverridesJson()
    {
        Console.WriteLine("--- C# overrides JSON ---");
        var ar = new ActionResolver();
        ar.LoadMapping(Path.Combine(root, "configs", "actions", "guard.json"));

        // Register a C# action with same name as JSON
        ar.Register(new GotoAction());

        // Execute should use C# version
        var attrs = Attrs;
        attrs.Set("location", "闁句礁灏冮柧?, "string", new string[0]);
        var ctx = new ActionContext { Attrs = attrs, Events = Events, Scheduler = Sched, Snap = Snap,
            Params = new Dictionary<string, object> { { "location", "闁版帡顩? } } };
        var item = new ActionItem { ActionType = "goto", Params = new Dictionary<string, object> { { "location", "闁版帡顩? } } };
        ar.Execute(item, ctx.Attrs, ctx.Events, ctx.Scheduler, ctx.Snap);

        Assert(Convert.ToInt64(attrs.Get("moving").Value) == 1, "C# goto executed, not JSON");
    }

    // === Helpers ===
    static string attrv(Attributes a, string name) => a.Get(name)?.Value?.ToString() ?? "";

    static void Assert(bool cond, string msg)
    {
        if (cond) { pass++; Console.WriteLine($"  [PASS] {msg}"); }
        else { fail++; Console.WriteLine($"  [FAIL] {msg}"); }
    }

    static void Assert(int expected, int actual, string msg)
    {
        Assert(expected == actual, $"{msg}: expected {expected}, got {actual}");
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
