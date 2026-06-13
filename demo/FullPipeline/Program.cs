using System;
using System.Collections.Generic;
using System.IO;

class Program
{
    static void Main()
    {
        var root = FindRoot();
        var attrs = new Attributes();
        var records = new RecordModule();
        var semantic = new SemanticEngine();
        var dc = new DataCollector.DataCollector(
            attrs, records, semantic,
            Path.Combine(root, "configs", "collect"),
            Path.Combine(root, "configs", "semantic"));
        var pt = new PromptTemplate.PromptTemplate(
            Path.Combine(root, "src", "PromptTemplate", "Templates"));

        // ==== INPUT ====
        Console.WriteLine("===== INPUT =====");
        Console.WriteLine("--- raw attributes ---");
        attrs.Set("name", "鐢啯绀囬崗?, "string", new[]{"identity"});
        attrs.Set("personality", "閸曞洦鏆嶉妴浣稿珶閸旂偨鈧椒绗夐幀鏇炴倖閼?, "string", new[]{"identity"});
        attrs.Set("prompt_template", "default", "string", new[]{"meta"});
        attrs.Set("hp", 85, "number", new[]{"vital","hp"});
        attrs.Set("hp_max", 100, "number", new[]{"vital","hp"});
        attrs.Set("stamina", 40, "number", new[]{"vital"});
        attrs.Set("stamina_max", 100, "number", new[]{"vital"});
        attrs.Set("iron_owned", 2, "number", new[]{"trade"});
        attrs.Set("iron_needed", 10, "number", new[]{"trade"});
        attrs.Set("gold", 15, "number", new[]{"currency"});
        attrs.Set("location", "闁句礁灏冮柧?, "string", new[]{"world"});
        attrs.Set("time", "閺冣晜娅?, "string", new[]{"world"});
        attrs.Set("weather", "闂冩潙銇?, "string", new[]{"world"});
        attrs.Set("affection", 65, "number", new[]{"social"});
        attrs.Set("affection_max", 100, "number", new[]{"social"});
        foreach(var kv in attrs.Export()) Console.WriteLine($"  {kv.Key}={kv.Value.Value}");

        records.Remember(new RecordSource{Method="self"}, "death", new[]{"mine","danger"},
            new Dictionary<string,object>{{"who","閻灝浼?},{"location","閻寧绀?}}, 0);
        records.Remember(new RecordSource{Method="self"}, "trade", new[]{"iron","trade"},
            new Dictionary<string,object>{{"item","闁句線鏁?},{"change","濞戙劋鐜?}}, 0);
        records.Remember(new RecordSource{Method="rumor",FromNpcId="blacksmith"}, "death", new[]{"mine","danger"},
            new Dictionary<string,object>{{"who","閻灝浼?},{"location","閻寧绀?}}, 0);
        records.Remember(new RecordSource{Method="witnessed",FromNpcId="blacksmith"}, "trade", new[]{"iron","trade"},
            new Dictionary<string,object>{{"item","闁句線鏁?},{"change","濞戙劋鐜?}}, 0);
        records.Remember(new RecordSource{Method="self"}, "injury", new[]{"mine","danger"},
            new Dictionary<string,object>{{"who","閸愭帡娅撻懓?},{"location","閻寧绀?}}, 0);
        records.Remember(new RecordSource{Method="told_by",FromNpcId="blacksmith"}, "injury", new[]{"mine","danger"},
            new Dictionary<string,object>{{"who","閸愭帡娅撻懓?},{"location","閻寧绀?}}, 0);

        Console.WriteLine("\n--- events ---");
        foreach(var evt in records.Recall(new[]{"mine","iron","trade","danger"}, 7, null))
        {
            var desc = evt.Data.TryGetValue("who", out var w) ? w
                     : evt.Data.TryGetValue("item", out var it) ? it : "?";
            Console.WriteLine($"  [{evt.Type}] {desc} (how={evt.Source.Method})");
        }

        // ==== OUTPUT ====
        Console.WriteLine("\n===== OUTPUT (semantic) =====");
        var result = dc.Collect("blacksmith", new CollectConfig{
            AttrTags = new[]{"vital","hp","trade","world","social"},
            EventTags = new[]{"mine","iron","danger","trade"},
            RuleFiles = new[]{"npc_state","npc_danger","social","adventure_memory"},
            EventDays=7, EventLimit=5
        });
        foreach(var kv in result.SemanticTexts) Console.WriteLine($"  [{kv.Key}] {kv.Value}");

        // ==== PROMPT ====
        Console.WriteLine("\n===== OUTPUT (prompt) =====");
        var prompt = pt.Build(attrs, result, "閹垫捇鈧?0閹跺﹪鎼ч崜?(闁句線鏁稉宥堝喕)",
            new List<string>{"閼洪亶鎸堕崢璇蹭紣娴兼碍鏁圭拹顓㈡惂闁?(鐎瑰鍙?","閼奉亜绻侀崢鑽ょ唵濞茬偞瀵查惌?(閸楅亶娅撻崗宥堝瀭)"});
        Console.WriteLine(prompt);
    }

    static string FindRoot()
    {
        var d = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while(d!=null){if(Directory.Exists(Path.Combine(d.FullName,"configs")))return d.FullName;d=d.Parent;}
        throw new Exception("root not found");
    }
}
