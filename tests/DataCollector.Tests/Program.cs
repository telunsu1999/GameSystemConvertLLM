using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

class Program
{
    static int p, f;
    static void Ok(bool c, string n) { if(c){p++;Console.WriteLine($"  PASS: {n}");}else{f++;Console.WriteLine($"  FAIL: {n}");} }

    static void Main()
    {
        Console.WriteLine("=== DataCollector Tests ===\n");

        var attrs = new Attributes();
        var records = new RecordModule();
        var semantic = new SemanticEngine();
        var repoRoot = FindRepoRoot();
        var configDir = Path.Combine(repoRoot, "configs", "collect");
        var rulesDir = Path.Combine(repoRoot, "configs", "semantic");

        var dc = new DataCollector.DataCollector(attrs, records, semantic, configDir, rulesDir);

        // Setup data
        attrs.Set("currentHP", 45, "number", new[]{"vital","hp","combat"});
        attrs.Set("maxHP", 100, "number", new[]{"vital","hp"});
        attrs.Set("mp", 20, "number", new[]{"vital","combat"});
        attrs.Set("mp_max", 80, "number", new[]{"vital"});
        attrs.Set("location", "閸﹂绗呴崺搴ｎ儑娑撳鐪?, "string", new[]{"world"});
        attrs.Set("weapon_name", "濞ｎ剛浼€闂€鍨ⅳ", "string", new[]{"equip"});
        attrs.Set("weapon_durability", 45, "number", new[]{"equip"});
        attrs.Set("weapon_max", 100, "number", new[]{"equip"});

        records.Remember(new RecordSource{Method="self"}, "death", new[]{"mine","danger"}, new Dictionary<string,object>{{"who","閻灝浼?},{"where","閻寧绀?}}, 0);
        records.Remember(new RecordSource{Method="rumor",FromNpcId="adventurer"}, "death", new[]{"mine","danger"}, new Dictionary<string,object>{{"who","閻灝浼?},{"where","閻寧绀?}}, 0);
        records.Remember(new RecordSource{Method="self"}, "treasure_found", new[]{"treasure"}, new Dictionary<string,object>{{"item","閸欍倛鈧胶娈戦幎銈堥煩缁?}}, 0);

        // CollectRaw
        var raw = dc.CollectRaw("adventurer", new CollectConfig{
            AttrTags = new[]{"vital","hp","combat","equip"},
            EventTags = new[]{"mine","treasure"},
            EventDays = 7,
            EventLimit = 5
        });
        Ok(raw.ContainsKey("currentHP"), "Raw: currentHP");
        Ok(raw.ContainsKey("maxHP"), "Raw: maxHP");
        Ok(raw.ContainsKey("weapon_name"), "Raw: weapon_name");
        Ok(raw.ContainsKey("memory_events"), "Raw: memory_events");
        var mems = (List<Dictionary<string,object>>)raw["memory_events"];
        Ok(mems.Count >= 1, "Raw: events count >= 1");
        Ok(mems.Any(m=>m.TryGetValue("source",out var s) && s.ToString()=="rumor"), "Raw: source=rumor");

        // Collect with semantic
        var result = dc.Collect("adventurer", new CollectConfig{
            AttrTags = new[]{"vital","hp","combat","equip","world"},
            EventTags = new[]{"mine","treasure"},
            RuleFiles = new[]{"adventurer_status","equipment"},
            EventDays = 7,
            EventLimit = 5
        });
        Ok(result.RawData.Count > 0, "Collect: raw data present");
        Ok(result.SemanticTexts.Count > 0, "Collect: semantic texts present");

        // Config-driven
        var cfgResult = dc.Collect("adventurer", "dungeon_scene");
        Ok(cfgResult.RawData.Count > 0, "Config-driven: raw data");
        Ok(cfgResult.SemanticTexts.Count > 0, "Config-driven: semantic texts");

        // Missing config
        bool threw = false;
        try { dc.Collect("adventurer", "nonexistent"); }
        catch (FileNotFoundException) { threw = true; }
        Ok(threw, "Missing config throws");

        Console.WriteLine($"\n=== {p} passed, {f} failed ===");
    }

    static string FindRepoRoot()
    {
        var d = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while(d!=null){if(Directory.Exists(Path.Combine(d.FullName,"configs")))return d.FullName;d=d.Parent;}
        throw new Exception("repo root not found");
    }
}
