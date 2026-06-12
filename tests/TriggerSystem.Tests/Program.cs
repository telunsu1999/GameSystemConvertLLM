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

        TestLoadRules();
        TestEvaluateMatch();
        TestEvaluateNoMatch();
        TestDirectMode();
        TestLlmMode();
        TestConditionEq();
        TestConditionNe();
        TestConditionGt();
        TestConditionGte();
        TestConditionLt();
        TestConditionLte();
        TestConditionString();
        TestEvaluateStateless();
        TestTagsAccessible();
        TestMultipleRules();
        TestMultipleDirectAndLlm();
        TestRuleWithMultipleConditions();

        Console.WriteLine($"\n=== {pass} pass, {fail} fail ===");
        Environment.Exit(fail > 0 ? 1 : 0);
    }

    static TriggerSystem LoadSys(string name)
    {
        var sys = new TriggerSystem();
        sys.LoadRules(Path.Combine(root, "configs", "triggers", name));
        return sys;
    }

    static TriggerSystem BuildSys(List<TriggerRule> rules)
    {
        var cfg = new TriggerConfig();
        cfg.Rules = rules;
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, Newtonsoft.Json.JsonConvert.SerializeObject(cfg));
        var sys = new TriggerSystem();
        sys.LoadRules(tmp);
        File.Delete(tmp);
        return sys;
    }

    static Condition Cond(string field, string op, object value)
    {
        return new Condition { Field = field, Op = op, Value = value };
    }

    static TriggerRule Rule(string mode, string actionType, Condition[] conditions, string[] tags = null)
    {
        var r = new TriggerRule { Mode = mode, ActionType = actionType };
        if (conditions != null) r.Conditions = new List<Condition>(conditions);
        if (tags != null) r.Tags = tags;
        return r;
    }

    static Attributes MakeAttrs(Dictionary<string, object> vals)
    {
        var a = new Attributes();
        foreach (var kv in vals)
        {
            if (kv.Value is int iv) a.Set(kv.Key, iv, "number", new string[0]);
            else if (kv.Value is long lv) a.Set(kv.Key, lv, "number", new string[0]);
            else if (kv.Value is string sv) a.Set(kv.Key, sv, "string", new string[0]);
            else if (kv.Value is bool bv) a.Set(kv.Key, bv, "bool", new string[0]);
        }
        return a;
    }

    // === 5.1 LoadRules ===
    static void TestLoadRules()
    {
        Console.WriteLine("--- LoadRules ---");
        var sys = LoadSys("guard.json");
        var attrs = MakeAttrs(new Dictionary<string,object>{ {"dead",1} });
        var r = sys.Evaluate("attr", "dead", attrs);
        Assert(r.DirectActions.Count + r.LlmOptions.Count > 0, "rules loaded and matching");
    }

    // === 5.2 EvaluateMatch ===
    static void TestEvaluateMatch()
    {
        Console.WriteLine("--- Evaluate match ---");
        var sys = LoadSys("guard.json");
        var attrs = MakeAttrs(new Dictionary<string,object>{ {"dead",1} });
        var r = sys.Evaluate("attr", "dead", attrs);
        Assert(r.DirectActions.Count > 0, "matched dead=1");
    }

    // === 5.3 EvaluateNoMatch ===
    static void TestEvaluateNoMatch()
    {
        Console.WriteLine("--- Evaluate no match ---");
        var sys = LoadSys("guard.json");
        var attrs = MakeAttrs(new Dictionary<string,object>{ {"hp",50} });
        var r = sys.Evaluate("attr", "hp", attrs);
        Assert(r.DirectActions.Count == 0 && r.LlmOptions.Count == 0, "no rule for hp=50");
    }

    // === 5.4 Direct vs LLM ===
    static void TestDirectMode()
    {
        Console.WriteLine("--- Direct mode ---");
        var sys = LoadSys("guard.json");
        var attrs = MakeAttrs(new Dictionary<string,object>{ {"on_trap",1} });
        var r = sys.Evaluate("attr", "on_trap", attrs);
        Assert(r.DirectActions.Count >= 1, "direct action found");
    }

    static void TestLlmMode()
    {
        Console.WriteLine("--- LLM mode ---");
        var sys = LoadSys("guard.json");
        var attrs = MakeAttrs(new Dictionary<string,object>{ {"enemy_visible",1} });
        var r = sys.Evaluate("attr", "enemy_visible", attrs);
        Assert(r.LlmOptions.Count >= 1, "llm option found");
    }

    // === 5.5 Condition operators ===
    static void TestConditionEq()
    {
        Console.WriteLine("--- Condition eq ---");
        var sys = LoadSys("guard.json");
        var attrs = MakeAttrs(new Dictionary<string,object>{ {"dead",1} });
        var r = sys.Evaluate("attr", "dead", attrs);
        Assert(r.DirectActions.Any(a => a.ActionType == "play_anim"), "dead=1 triggers play_anim");
    }

    static void TestConditionNe()
    {
        Console.WriteLine("--- Condition ne ---");
        var rules = new List<TriggerRule> {
            Rule("direct", "act_alive", new[]{ Cond("state","ne","dead") })
        };
        var sys = BuildSys(rules);
        var attrs = MakeAttrs(new Dictionary<string,object>{ {"state","alive"} });
        var r = sys.Evaluate("attr", "state", attrs);
        Assert(r.DirectActions.Any(a => a.ActionType == "act_alive"), "ne matches");
    }

    static void TestConditionGt()
    {
        Console.WriteLine("--- Condition gt ---");
        var sys = LoadSys("merchant.json");
        var attrs = MakeAttrs(new Dictionary<string,object>{ {"hour",22} });
        var r = sys.Evaluate("attr", "hour", attrs);
        Assert(r.DirectActions.Any(a => a.ActionType == "go_home"), "hour>=20 triggers go_home");
    }

    static void TestConditionGte()
    {
        Console.WriteLine("--- Condition gte ---");
        var sys = LoadSys("merchant.json");
        var attrs = MakeAttrs(new Dictionary<string,object>{ {"hour",20} });
        var r = sys.Evaluate("attr", "hour", attrs);
        Assert(r.DirectActions.Any(a => a.ActionType == "go_home"), "hour=20 triggers at boundary");
    }

    static void TestConditionLt()
    {
        Console.WriteLine("--- Condition lt ---");
        var sys = LoadSys("guard.json");
        var attrs = MakeAttrs(new Dictionary<string,object>{ {"hp",15} });
        var r = sys.Evaluate("attr", "hp", attrs);
        Assert(r.LlmOptions.Any(a => a.ActionType == "survival_decision"), "hp<30 triggers");
    }

    static void TestConditionLte()
    {
        Console.WriteLine("--- Condition lte ---");
        var sys = LoadSys("merchant.json");
        var attrs = MakeAttrs(new Dictionary<string,object>{ {"gold",3} });
        var r = sys.Evaluate("attr", "gold", attrs);
        Assert(r.DirectActions.Any(a => a.ActionType == "close_shop"), "gold<5 triggers");
    }

    static void TestConditionString()
    {
        Console.WriteLine("--- Condition string ---");
        var rules = new List<TriggerRule> {
            Rule("direct", "drink", new[]{ Cond("location","eq","闁版帡顩?) })
        };
        var sys = BuildSys(rules);
        var attrs = MakeAttrs(new Dictionary<string,object>{ {"location","闁版帡顩?} });
        var r = sys.Evaluate("attr", "location", attrs);
        Assert(r.DirectActions.Count == 1, "string eq matches");
    }

    // === 5.7 Statelessness ===
    static void TestEvaluateStateless()
    {
        Console.WriteLine("--- Stateless ---");
        var sys = LoadSys("guard.json");
        var attrs = MakeAttrs(new Dictionary<string,object>{ {"hp",10} });
        var r1 = sys.Evaluate("attr", "hp", attrs);
        var r2 = sys.Evaluate("attr", "hp", attrs);
        Assert(r1.LlmOptions.Count == r2.LlmOptions.Count, "same count");
        Assert(r1.DirectActions.Count == r2.DirectActions.Count, "same direct count");
    }

    // === 5.8 Tags ===
    static void TestTagsAccessible()
    {
        Console.WriteLine("--- Tags ---");
        var sys = LoadSys("guard.json");
        var attrs = MakeAttrs(new Dictionary<string,object>{ {"dead",1} });
        var r = sys.Evaluate("attr", "dead", attrs);
        Assert(r.DirectActions[0].Tags.Contains("death"), "has death tag");
    }

    // === Multiple rules ===
    static void TestMultipleRules()
    {
        Console.WriteLine("--- Multiple rules match ---");
        var rules = new List<TriggerRule> {
            Rule("direct", "a", new[]{ Cond("x","eq",1) }),
            Rule("llm",    "b", new[]{ Cond("x","eq",1) })
        };
        var sys = BuildSys(rules);
        var attrs = MakeAttrs(new Dictionary<string,object>{ {"x",1} });
        var r = sys.Evaluate("attr", "x", attrs);
        Assert(1, r.DirectActions.Count, "1 direct");
        Assert(1, r.LlmOptions.Count, "1 llm");
    }

    static void TestMultipleDirectAndLlm()
    {
        Console.WriteLine("--- Multiple LLM options ---");
        var sys = LoadSys("merchant.json");
        var attrs = MakeAttrs(new Dictionary<string,object>{ {"player_near",1} });
        var r = sys.Evaluate("attr", "player_near", attrs);
        Assert(3, r.LlmOptions.Count, "3 llm options for player_near");
    }

    static void TestRuleWithMultipleConditions()
    {
        Console.WriteLine("--- Multiple conditions (AND) ---");
        var rules = new List<TriggerRule> {
            Rule("direct", "flee", new[]{ Cond("hp","lt",50), Cond("enemy","eq",1) })
        };
        var sys = BuildSys(rules);

        var attrs = MakeAttrs(new Dictionary<string,object>{ {"hp",30}, {"enemy",1} });
        var r = sys.Evaluate("attr", "hp", attrs);
        Assert(1, r.DirectActions.Count, "both conditions met");

        var attrs2 = MakeAttrs(new Dictionary<string,object>{ {"hp",30}, {"enemy",0} });
        var r2 = sys.Evaluate("attr", "hp", attrs2);
        Assert(0, r2.DirectActions.Count, "only hp met, not enemy");
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
