using System;
using System.Collections.Generic;

class Program
{
    static int passed = 0, failed = 0;

    static void Check(bool condition, string name)
    {
        if (condition) { passed++; Console.WriteLine($"  PASS: {name}"); }
        else { failed++; Console.WriteLine($"  FAIL: {name}"); }
    }

    static void Main()
    {
        Console.WriteLine("=== Attributes Tests ===\n");

        // Set/Get
        var m = new Attributes();
        m.Set("hp", 85, "number", new[] { "vital" });
        Check(m.Get("hp").Value.Equals(85), "Set/Get value");
        Check(m.Get("hp").Type == "number", "Set/Get type");
        Check(m.Has("hp"), "Has existing");
        Check(!m.Has("x"), "Has missing");

        // Overwrite
        m.Set("hp", 50, "number", new[] { "vital", "combat" });
        Check(m.Get("hp").Value.Equals(50), "Overwrite value");
        Check(m.Get("hp").Tags.Length == 2, "Overwrite tags");

        // Type mismatch
        bool threw = false;
        try { m.Set("hp", "bad", "number", null); }
        catch (ArgumentException) { threw = true; }
        Check(threw, "Type mismatch throws");

        // SetValue
        m.SetValue("hp", 30);
        Check(m.Get("hp").Value.Equals(30), "SetValue updates");

        bool threw2 = false;
        try { m.SetValue("x", 1); }
        catch (ArgumentException) { threw2 = true; }
        Check(threw2, "SetValue missing throws");

        // SetTags
        m.SetTags("hp", new[] { "vital", "debuff" });
        Check(m.Get("hp").Value.Equals(30), "SetTags preserves value");

        // Remove
        m.Set("gold", 100, "number", new[] { "currency" });
        m.Remove("gold");
        Check(m.Get("gold") == null, "Remove returns null");

        // GetByTags
        m.Set("str", 12, "number", new[] { "stat" });
        m.Set("mp", 50, "number", new[] { "vital" });
        var vital = m.GetByTags(new[] { "vital" });
        Check(vital.Count == 2, "GetByTags count");

        // LoadFrom merge
        var m2 = new Attributes();
        m2.Set("hp", 85, "number", new[] { "vital" });
        m2.Set("str", 12, "number", new[] { "stat" });
        var src = new Dictionary<string, AttributeValue> {
            ["hp"] = new AttributeValue(50, "number", new[]{"vital"}),
            ["gold"] = new AttributeValue(200, "number", new[]{"currency"})
        };
        m2.LoadFrom(src);
        Check(m2.Get("hp").Value.Equals(50), "LoadFrom overwrites");
        Check(m2.Get("str").Value.Equals(12), "LoadFrom preserves");
        Check(m2.Get("gold").Value.Equals(200), "LoadFrom adds");

        // Export
        var e = m2.Export();
        Check(e.Count == 3, "Export count");

        // GetAllTags
        var tags = m2.GetAllTags();
        Check(tags.Length >= 3, "GetAllTags");

        Console.WriteLine($"\n=== {passed} passed, {failed} failed ===");
    }
}
