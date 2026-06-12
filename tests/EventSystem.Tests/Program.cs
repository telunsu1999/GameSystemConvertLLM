using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

class Program
{
    static int p, f;
    static void Ok(bool c, string n) { if(c){p++;Console.WriteLine($"  PASS: {n}");}else{f++;Console.WriteLine($"  FAIL: {n}");} }

    static void Main()
    {
        Console.WriteLine("=== EventSystem Tests ===\n");
        Record(); Perceive(); Spread(); Query(); Scorer();
        TagIndex(); EdgeCases(); Combinations();
        Console.WriteLine($"\n=== {p} passed, {f} failed ===");
    }

    static void Record()
    {
        Console.WriteLine("-- Record --");
        var es = new EventSystem();
        var id = es.Record("death", new[]{"mine","danger"}, new Dictionary<string,object>{{"who","閻灝浼?}});
        Ok(!string.IsNullOrEmpty(id), "鏉╂柨娲栭棃鐐碘敄ID");
        Ok(id.Length == 32, "ID 32娴?);
        Ok(es.Record("weather",new string[0],null).Length == 32, "缁岀皪ags");
        Ok(es.Record("social",null,null).Length == 32, "null tags");
        var all = es.Record("trade",new[]{"economy"},null);
        Ok(id != all, "ID閸烆垯绔?);
    }

    static void Perceive()
    {
        Console.WriteLine("-- Perceive --");
        var es = new EventSystem();
        var id = es.Record("death",new[]{"mine"},new Dictionary<string,object>{{"who","閻灝浼?}});

        es.Perceive(id,"guard","witnessed");
        Ok(es.Query(new EventQuery{NpcId="guard"}).Count==1, "閻╊喚婢掗崥搴″讲閻?);
        Ok(es.Query(new EventQuery{NpcId="blacksmith"}).Count==0, "閺堫亝鍔呴惌銉ょ瑝閸欘垳鐓?);

        es.Perceive(id,"guard","rumor");
        var p = es.Query(new EventQuery{NpcId="guard"})[0].Perceptions;
        Ok(p.Count(pp=>pp.NpcId=="guard")==2, "婢舵碍顐奸幇鐔虹叀娑撳秷顩惄?);

        es.Perceive(id,"blacksmith","told_by");
        Ok(es.Query(new EventQuery{NpcId="blacksmith"}).Count==1, "闁句礁灏冩稊鐔峰讲閻?);

        string[] hows = {"witnessed","found_trace","told_by","player_told","rumor","gm_inject"};
        var id2 = es.Record("combat",new[]{"battle"},null);
        for(int i=0;i<hows.Length;i++) es.Perceive(id2,$"npc_{i}",hows[i]);
        Ok(es.Query(new EventQuery{NpcId="npc_0",SourceFilter=new[]{"witnessed"}}).Count==1, "witnessed鏉╁洦鎶?);
        Ok(es.Query(new EventQuery{NpcId="npc_5",SourceFilter=new[]{"gm_inject"}}).Count==1, "閼奉亜鐣炬稊濉皁w鏉╁洦鎶?);
    }

    static void Spread()
    {
        Console.WriteLine("-- Spread --");
        var es = new EventSystem();
        var id = es.Record("death",new[]{"mine"},new Dictionary<string,object>{{"who","閻灝浼?}});
        es.Perceive(id,"guard","witnessed");

        es.Spread(id,"guard","blacksmith","told_by");
        var be = es.Query(new EventQuery{NpcId="blacksmith"});
        Ok(be.Count==1, "娴肩姵鎸遍幋鎰");
        Ok(be[0].Perceptions.Any(pp=>pp.NpcId=="blacksmith"&&pp.From=="guard"), "from濮濓絿鈥?);

        es.Spread(id,"blacksmith","merchant","told_by");
        Ok(es.Query(new EventQuery{NpcId="merchant"}).Count==1, "闁炬儳绱℃导鐘虫尡");

        var id2 = es.Record("treasure",new[]{"treasure"},null);
        es.Spread(id2,"unknown","blacksmith","told_by");
        Ok(es.Query(new EventQuery{NpcId="blacksmith",Tags=new[]{"treasure"}}).Count==0, "濠ф劒绗夐惌銉﹀剰娑撳秳绱堕幘?);

        es.Spread(id,"guard","player","閸愭瑥婀穱锟犲櫡");
        Ok(es.Query(new EventQuery{NpcId="player"})[0].Perceptions.Any(pp=>pp.How=="閸愭瑥婀穱锟犲櫡"), "閼奉亞鏁県ow鐎涙顑佹稉?);
    }

    static void Query()
    {
        Console.WriteLine("-- Query --");
        var es = new EventSystem();
        var id1 = es.Record("death",new[]{"mine","danger"},new Dictionary<string,object>{{"who","A"}});
        var id2 = es.Record("death",new[]{"mine","danger"},new Dictionary<string,object>{{"who","B"}});
        var id3 = es.Record("weather",new[]{"weather"},null);
        var id4 = es.Record("trade",new[]{"trade"},null);
        es.Perceive(id1,"smith","witnessed");
        es.Perceive(id2,"smith","rumor");
        es.Perceive(id2,"guard","witnessed");
        es.Perceive(id3,"smith","witnessed");
        es.Perceive(id4,"merchant","witnessed");

        Ok(es.Query(new EventQuery{NpcId="smith"}).Count==3, "smith 3閺?);
        Ok(es.Query(new EventQuery{NpcId="guard"}).Count==1, "guard 1閺?);
        Ok(es.Query(new EventQuery{NpcId="smith",Known=false,Tags=new[]{"trade"}}).Count==1, "smith閺堫亞鐓rade");
        Ok(es.Query(new EventQuery{Tags=new[]{"mine"}}).Count==2, "mine閺嶅洨顒?閺?);
        Ok(es.Query(new EventQuery{Tags=new[]{"mine","weather"}}).Count==3, "mine+weather 3閺?);
        Ok(es.Query(new EventQuery{NpcId="smith",SourceFilter=new[]{"witnessed"}}).Count==2, "witnessed 2閺?);
        Ok(es.Query(new EventQuery{NpcId="smith",SourceFilter=new[]{"rumor"}}).Count==1, "rumor 1閺?);
        Ok(es.Query(new EventQuery{RecentDays=1}).Count==4, "1婢垛晛鍞?閺?);
        Ok(es.Query(new EventQuery{Limit=1}).Count==1, "limit=1");
        Ok(es.Query(new EventQuery{Limit=0}).Count==0, "limit=0");
        Ok(es.Query(new EventQuery()).Count==4, "缁岀儤鐓＄拠?閸忋劑鍎?);
        Ok(es.Query(new EventQuery{NpcId="none"}).Count==0, "娑撳秴鐡ㄩ崷鈭烶C=0");
        Ok(es.Query(new EventQuery{Tags=new[]{"fishing"}}).Count==0, "娑撳秴灏柊?0");

        var desc = es.Query(new EventQuery{SortBy="time_desc"});
        for(int i=1;i<desc.Count;i++) Ok(desc[i-1].Time>=desc[i].Time, "time_desc");

        var asc = es.Query(new EventQuery{SortBy="time_asc"});
        for(int i=1;i<asc.Count;i++) Ok(asc[i-1].Time<=asc[i].Time, "time_asc");
    }

    static void Scorer()
    {
        Console.WriteLine("-- Scorer --");
        var s = new DefaultScorer();
        var now = DateTime.Now;
        var d = new Event{Type="death",Time=now,Data=new Dictionary<string,object>()};
        var c = new Event{Type="combat",Time=now,Data=new Dictionary<string,object>()};
        var t = new Event{Type="trade",Time=now,Data=new Dictionary<string,object>()};
        var w = new Event{Type="weather",Time=now,Data=new Dictionary<string,object>()};
        var u = new Event{Type="unknown",Time=now,Data=new Dictionary<string,object>()};

        Ok(s.Score(d) > s.Score(c), "death>combat");
        Ok(s.Score(c) > s.Score(t), "combat>trade");
        Ok(s.Score(t) > s.Score(w), "trade>weather");
        Ok(s.Score(w) > s.Score(u), "weather>unknown");
        Ok(s.Score(u) == 0, "unknown=0");

        var old = new Event{Type="death",Time=now.AddDays(-100),Data=new Dictionary<string,object>()};
        Ok(s.Score(d) > s.Score(old), "鏉╂垶婀?鏉╂粌褰?);
        Ok(s.Score(old) >= 0, "鏉╂粌褰?=0");

        var es = new EventSystem();
        var id = es.Record("death",new[]{"mine"},null);
        es.Perceive(id,"smith","witnessed");
        var scored = es.Query(new EventQuery{NpcId="smith",SortBy="score_desc",Scorer=s});
        Ok(scored.Count==1, "score閹烘帒绨崣顖滄暏");
    }

    static void TagIndex()
    {
        Console.WriteLine("-- Tag Index --");
        var es = new EventSystem();
        es.Record("test",new[]{"mine","danger","cavern"},null);
        es.Record("test",new[]{"weather","sunny"},null);
        Ok(es.Query(new EventQuery{Tags=new[]{"mine"}}).Count==1, "mine");
        Ok(es.Query(new EventQuery{Tags=new[]{"danger"}}).Count==1, "danger");
        Ok(es.Query(new EventQuery{Tags=new[]{"cavern"}}).Count==1, "cavern");
        Ok(es.Query(new EventQuery{Tags=new[]{"mine","sunny"}}).Count==2, "mine+sunny");

        var tags = new List<string>();
        for(int i=0;i<50;i++) tags.Add($"tag_{i}");
        es.Record("test",tags.ToArray(),null);
        Ok(es.Query(new EventQuery{Tags=new[]{"tag_0"}}).Count==1, "50閺嶅洨顒穞ag_0");
        Ok(es.Query(new EventQuery{Tags=new[]{"tag_49"}}).Count==1, "50閺嶅洨顒穞ag_49");
        Ok(es.Query(new EventQuery{Tags=new[]{"tag_99"}}).Count==0, "娑撳秴鐡ㄩ崷銊︾垼缁?);
    }

    static void EdgeCases()
    {
        Console.WriteLine("-- Edge Cases --");
        var es = new EventSystem();
        Ok(es.Query(new EventQuery()).Count==0, "缁屽搫鐡ㄩ崒?閺?);
        Ok(es.Query(new EventQuery{NpcId="any"}).Count==0, "缁屽搫鐡ㄩ崒鈭烶C閺屻儴顕?閺?);

        var id = es.Record("test",new[]{"tag"},null);
        es.Perceive(id,"npc1","witnessed");
        Ok(es.Query(new EventQuery{NpcId="npc1",Known=null}).Count==1, "Known=null姒涙顓婚惌?);
        Ok(es.Query(new EventQuery{NpcId="npc1",Known=true}).Count==1, "Known=true");
        Ok(es.Query(new EventQuery{NpcId="npc2",Known=null}).Count==0, "閺冪姵鍔呴惌?0");
        Ok(es.Query(new EventQuery{NpcId="npc2",Known=false}).Count==1, "Known=false");

        for(int i=0;i<100;i++) es.Record($"t_{i%10}",new[]{$"tag_{i%20}"},null);
        Ok(es.Query(new EventQuery{Tags=new[]{"tag_0"}}).Count>=5, "100娴滃娆㈤幍褰掑櫤");

        var id2 = es.Record("popular",new[]{"gossip"},null);
        for(int i=0;i<20;i++) es.Perceive(id2,$"npc_{i}","rumor");
        Ok(es.Query(new EventQuery{Tags=new[]{"gossip"}})[0].Perceptions.Count==20, "20閺夆剝鍔呴惌?);
    }

    static void Combinations()
    {
        Console.WriteLine("-- Combinations --");
        var es = new EventSystem();
        var s = new DefaultScorer();
        string[] npcs = {"smith","guard","merchant","bard","farmer"};
        string[] types = {"death","trade","social","combat","weather"};
        string[] allTags = {"mine","city","tavern","market","farm"};

        for(int i=0;i<20;i++)
        {
            var id = es.Record(types[i%5],new[]{allTags[i%5],$"tag_{i}"},new Dictionary<string,object>{{"idx",i}});
            for(int j=0;j<3;j++) es.Perceive(id,npcs[(i+j)%5],j==0?"witnessed":"rumor");
        }

        Ok(es.Query(new EventQuery()).Count==20, "閹鍙?0娴滃娆?);

        var q = new EventQuery { NpcId="smith",Tags=new[]{"mine","tavern"},RecentDays=1,
            SourceFilter=new[]{"witnessed"},SortBy="score_desc",Scorer=s,Limit=3 };
        var r = es.Query(q);
        Ok(r.Count<=3, "閸忋劎绮嶉崥鍧檌mit<=3");

        Ok(es.Query(new EventQuery{SortBy="score_desc",Scorer=s,Limit=5}).Count==5, "娴犲懏甯撴惔?limit");
        Ok(es.Query(new EventQuery{NpcId="smith",Known=false}).Count>=1, "smith閺堝婀惌銉ょ皑娴?);
    }
}
