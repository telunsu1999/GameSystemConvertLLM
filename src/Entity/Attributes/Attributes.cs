using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLoop
{
    /// <summary>
    /// 闁诲繒鍋熼崑鐐哄焵椤戭剙鍟犻崑鎾剁箔鐞涒€充壕闁稿本鐟ч悷婵嬫煕濞嗘ê鐏犵憸鏉款樀婵?
    /// </summary>
    public class AttributeValue
    {
        public object Value { get; }
        public string Type { get; }
        public string[] Tags { get; }

        public AttributeValue(object value, string type, string[] tags)
        {
            Value = value;
            Type = type;
            Tags = tags ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// 闂佺粯鐟崜娑㈡偟濞戞矮娌柣鎰ˉ閸嬫捁顦查柣掳鍔戝畷鎺楀Ω鏈笟鈧畷绋款渻鐏忔牕浜鹃柛灞捐壘鐠佹煡鏌熺粙鍨偓钘壝归崶顒€绀嗗Λ棰佺劍瀹曟煡鏌熼弶璺ㄧ闁汇倕瀚幏?NPC/闂佺粯澹曢弲娑㈩敊?闂佺粯銇涢弲娑㈠箹?闂?
    /// 濡ょ姷鍋炴繛濠囧箺閻㈠憡鍎嶉柛鏇ㄥ灡閺嗘盯鏌涙繝鍕付闁宦板姂瀹曟帡鈥﹂幒鏃傤槷闁汇埄鍨芥俊鍥╂偖椤愶箑鍨傞悗锝庡亞婢р€澄旈悩鑼跺闁硅渹鍗冲浠嬪炊閳哄﹤濮扮紓浣风┒閸ㄥ湱妲愰埡鍛?
    /// </summary>
    public class Attributes : IEntityComponent
    {
        private readonly Dictionary<string, AttributeValue> _attrs
            = new Dictionary<string, AttributeValue>();

        private readonly Dictionary<string, HashSet<string>> _tagIndex
            = new Dictionary<string, HashSet<string>>();

        // 闂佸啿鍘滈崑鎾绘煃閸忓浜?婵犫拃鍛粶闁搞劌缍婂銊ф喆閸曨厼寮?闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜?

        /// <summary>
        /// 闁荤姳绀佹晶浠嬫偪閸℃瑤娌柣鎰ˉ閸嬫捁顦伴柍褜鍓欓崐鎼佸垂閸楃儐鍤堥柣鎴灻悘妤呮偡閺囩偞顥犳繛鎻掞躬婵℃挳宕掑鍡╂蕉闂佹悶鍔岄鍕箔婢舵劕绀岀憸鐗堝笒鐢娊鏌￠崘顓炵厫濠殿喖绻樺畷?ArgumentException闂?
        /// </summary>
        public void Set(string name, object value, string type, string[] tags)
        {
            ValidateType(name, value, type);

            // 闁诲繒鍋熼崑鐐哄焵椤戭剙鍟崵鎺楁倵濞戞顏勶耿椤忓牆绫嶉悹浣告贡缁€澶愭煕韫囨挾澧旂紒顔惧Х濡叉劙濮€閻樼數鈹涙繛鎴炴惄閸樺彞绨洪梻鍌氬閸婃牕螞椤愶箑鍐€闁搞儮鏅╅崝?
            if (_attrs.TryGetValue(name, out var old))
                RemoveFromIndex(name, old.Tags);

            var attr = new AttributeValue(value, type, tags);
            _attrs[name] = attr;
            AddToIndex(name, tags);
        }

        /// <summary>
        /// 闂佸憡鐟禍娆撳箞閵娾晛缁╅柣鐔稿閸嬫挾绮悰鈥充壕闁稿本姘ㄥ锝夋煙椤戭剙鍟扮粻鎴濐渻閵堝牜鍤欓柣掳鍔戝畷鐑藉Ω閵堝洨鎳嶇紓渚囧亯椤曆囨倵閻戣棄绀岀憸鐗堝笒鐢娊鏌?
        /// </summary>
        public void SetValue(string name, object value)
        {
            if (!_attrs.TryGetValue(name, out var existing))
                throw new ArgumentException($"属性 '{name}' 不存在");

            ValidateType(name, value, existing.Type);
            _attrs[name] = new AttributeValue(value, existing.Type, existing.Tags);
        }

        /// <summary>
        /// 设置属性的标签
        /// </summary>
        public void SetTags(string name, string[] tags)
        {
            if (!_attrs.TryGetValue(name, out var existing))
                throw new ArgumentException($"属性 '{name}' 不存在");

            RemoveFromIndex(name, existing.Tags);
            _attrs[name] = new AttributeValue(existing.Value, existing.Type, tags);
            AddToIndex(name, tags);
        }

        /// <summary>
        /// 闂佸憡甯炴繛鈧繛鍛捣娴狅箓鎮欑划鐟颁壕鐟滄繄妲愬┑瀣Е閻忕偟鍋撻ˇ褎绻涢幘铏櫧闁诡喖锕浠嬪炊閳哄﹤濮扮紓浣风┒閸ㄥ湱妲愰埡鍛?
        /// </summary>
        public void Remove(string name)
        {
            if (_attrs.TryGetValue(name, out var existing))
            {
                RemoveFromIndex(name, existing.Tags);
                _attrs.Remove(name);
            }
        }

        /// <summary>
        /// 闂佸搫琚崕鎾敋濡ゅ啩娌柣鎰ˉ閸嬫捁顦伴柍褜鍓欓崐椋庣箔婢跺备鍋撳☉娅亜锕㈤鍛氦闁哄倹瀵х粈鈧?null闂?
        /// </summary>
        public AttributeValue Get(string name)
        {
            return _attrs.TryGetValue(name, out var attr) ? attr : null;
        }

        public bool Has(string name) => _attrs.ContainsKey(name);

        // 闂佸啿鍘滈崑鎾绘煃閸忓浜?闂佸綊娼х紞濠囧闯濞差亜钃熼柕澶樼厛閸?闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜?

        /// <summary>
        /// 闂佸湱顭堥ˇ浼存偉閿濆洨椹抽柛鎾楀懏鎳€闂備緡鍋勯ˇ顖炴儓濮椻偓楠炩偓鐟滄繄妲愬┑瀣煑闁哄诞鍡楃彲闂傚倸妫楀Λ搴ㄥ焵椤掆偓閸婃悂宕洪崟顖涘仺闁靛濡囬崹鑲╃磼濞戞ê鈷旈柛灞诲妼椤曪綁寮撮悙鍨緰闂?O(1) 闂佸搫琚崕鍙夌珶濮椻偓婵?
        /// </summary>
        public Dictionary<string, AttributeValue> GetByTags(string[] tags)
        {
            var names = new HashSet<string>();
            foreach (var tag in tags)
            {
                if (_tagIndex.TryGetValue(tag, out var set))
                    names.UnionWith(set);
            }

            var result = new Dictionary<string, AttributeValue>();
            foreach (var name in names)
                result[name] = _attrs[name];
            return result;
        }

        /// <summary>
        /// 闂佸吋鍎抽崲鑼躲亹閸ヮ剙绠ラ柍褜鍓熷鍨緞婵犲嫭鍓戦梺璇″劯閸屾繂骞€闂佸憡鍨煎▍锝夌嵁閸ャ劍浜ら柛銉㈡櫆閻ｉ亶鏌″鍛窛妞ゆ帞鍏橀弫宥囦沪閽樺顔岄梻浣瑰絻缁夋煡鍩€?
        /// </summary>
        public string[] GetAllTags() => _tagIndex.Keys.ToArray();

        // 闂佸啿鍘滈崑鎾绘煃閸忓浜?闁诲海鏁搁崢褔宕ｉ崱娆屽亾閻㈤潧甯堕柛?闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜?

        /// <summary>
        /// 闂佸綊娼х紞濠囧闯閾忓厜鍋撻悽闈涘付闁告瑥妫濇俊鎾磼濮橆厼鈧亶鏌涘顒傂㈤柣顐ｏ耿楠炩偓鐟滄垿銆呴锔藉剮闁哄秶鏁哥粈澶愭煕濡厧鏋戞繝鈧笟鈧幆鍐礋椤忓棛顔旈梺浼欑稻閻燂綁鍩€?
        /// </summary>
        public void LoadFrom(Dictionary<string, AttributeValue> source)
        {
            foreach (var kv in source)
            {
                Set(kv.Key, kv.Value.Value, kv.Value.Type, kv.Value.Tags);
            }
        }

        /// <summary>
        /// 闁诲海鏁搁崢褔宕甸銏犵闁靛ň鏅涢崝銉╂倶閻愰潧浠﹂柍褜鍎稿鍥╊槷婵炴挻纰嶇粙鎴λ囨繝姘劸闁靛鍎崇喊宥夋煕閹烘搩娈旈悗闈涘级閹峰懐鎹勯妸锔芥闂?
        /// </summary>
        public Dictionary<string, AttributeValue> Export()
        {
            return new Dictionary<string, AttributeValue>(_attrs);
        }

        // 闂佸啿鍘滈崑鎾绘煃閸忓浜?闂佸憡鍔曢幊姗€宕曢幘顔兼閻熸瑥瀚妴?闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜鹃梺鍐插帨閸嬫捇鏌嶉崗澶婁壕闂佸啿鍘滈崑鎾绘煃閸忓浜?

        private void ValidateType(string name, object value, string type)
        {
            bool valid = type switch
            {
                "number" => value is int || value is long || value is float || value is double,
                "string" => value is string,
                "bool"   => value is bool,
                _        => false
            };

            if (!valid)
                throw new ArgumentException(
                    $"属性 '{name}': 值的类型 '{value?.GetType().Name}' 与声明的类型 '{type}' 不匹配。");
        }

        private void AddToIndex(string name, string[] tags)
        {
            if (tags == null) return;
            foreach (var tag in tags)
            {
                if (!_tagIndex.ContainsKey(tag))
                    _tagIndex[tag] = new HashSet<string>();
                _tagIndex[tag].Add(name);
            }
        }

        private void RemoveFromIndex(string name, string[] tags)
        {
            if (tags == null) return;
            foreach (var tag in tags)
            {
                if (_tagIndex.TryGetValue(tag, out var set))
                {
                    set.Remove(name);
                    if (set.Count == 0)
                        _tagIndex.Remove(tag);
                }
            }
        }
        public void OnTick(TickContext ctx)
        {
            Set("hour", ctx.Hour, "number", new[] { "world" });
            Set("time_block", ctx.TimeBlock, "string", new[] { "world" });
            Set("day", ctx.Day, "number", new[] { "world" });
            Set("season", ctx.Season, "string", new[] { "world" });
        }
    }
}
