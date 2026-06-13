using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace GameLoop
{
    /// <summary>
    /// 闁轰胶澧楀畵渚€寮ㄩ崼鏇熻偁婵☆垪鈧櫕鍋ラ柕鍡楀€芥禒娑㈠触閸繄娼ｉ柟?濞存粌顑勫▎銏ゆ晬鐏炶棄璁查梺顐㈩槼閻ㄧ喖鎮介妸顭戝殧濞戞柨顦槐鈺呭箼鎼存稈鍋?
    /// 闁衡偓椤栨稑鐦梺鏉跨Ф閻ゅ棝寮崶锔筋偨濡炵懓宕慨鈺呭椽鐏炴儳澶嶉柛娆欑悼濞插潡骞掗妷銊ф闁活潿鍔婇埀?
    /// </summary>
    public class DataCollector : IDataCollector
    {
        private readonly Attributes _attrs;
        private readonly RecordModule _records;
        private readonly SemanticEngine _semantic;
        private readonly string _configDir;
        private readonly string _rulesDir;

        public DataCollector(
            Attributes attrs,
            RecordModule records,
            SemanticEngine semantic,
            string configDir,
            string rulesDir)
        {
            _attrs = attrs;
            _records = records;
            _semantic = semantic;
            _configDir = configDir;
            _rulesDir = rulesDir;
        }

        /// <summary>
        /// 濞寸姴閰ｉ崢銈囩磾椤旇姤鐎ù鐘烘硾婵偞娼禒瀣赋缂傚喚鍠栭幃妤呭绩閸洘鑲?
        /// </summary>
        public CollectResult Collect(string npcId, string configName)
        {
            var config = LoadConfig(configName);
            return Collect(npcId, config);
        }

        /// <summary>
        /// 闁烩晛鐡ㄧ敮瀛樺閻樻彃寮抽梺鏉跨Ф閻ゅ棝寮ㄩ崼鏇熻偁闁挎稑鑻懟鐔烘嫬閸愵亝鏆忛悹鍥跺幒缁犵喎顕ｉ弴鐔告儧
        /// </summary>
        public CollectResult Collect(string npcId, CollectConfig config)
        {
            var raw = CollectRaw(npcId, config);

            var rules = LoadRules(config.RuleFiles);
            var texts = _semantic.EvaluateAll(rules, raw);
            Logger.Debug("Collect", $"Collected: {npcId}", new { attrs = raw.Count, texts = texts.Count });
            return new CollectResult
            {
                RawData = raw,
                SemanticTexts = texts
            };
        }

        /// <summary>
        /// 闁告瑯浜濋弫褰掓⒖閸℃鏂у┑顔碱儐閺嗙喖骞戦鍡欑濞戞挸绉烽惃鐔兼偨閵婎煈鍤斿☉鏂款槸缁扁晠骞?
        /// </summary>
        public Dictionary<string, object> CollectRaw(string npcId, CollectConfig config)
        {
            var data = new Dictionary<string, object>();

            // 閻忕偟鍋為埀顑嫭鏆梻?
            var attrs = _attrs.GetByTags(config.AttrTags ?? new string[0]);
            foreach (var kv in attrs)
                data[kv.Key] = kv.Value.Value;

            // Events from per-entity RecordModule
            var evts = _records.Recall(
                config.EventTags ?? new string[0],
                config.EventDays ?? 7,
                config.EventLimit ?? 5);

            var memList = new List<Dictionary<string, object>>();
            foreach (var evt in evts)
            {
                var mem = new Dictionary<string, object>(evt.Data ?? new Dictionary<string, object>())
                {
                    ["type"] = evt.Type,
                    ["source"] = evt.Source.Method
                };
                if (evt.Source.FromNpcId != null)
                    mem["from"] = evt.Source.FromNpcId;
                memList.Add(mem);
            }
            data["memory_events"] = memList;

            return data;
        }

        private CollectConfig LoadConfig(string name)
        {
            var path = Path.Combine(_configDir, $"{name}.json");
            if (!File.Exists(path))
                throw new FileNotFoundException($"闁衡偓閸洘鑲犻梺鏉跨Ф閻ゅ棝寮甸鍛棟闁? {path}");
            return JsonConvert.DeserializeObject<CollectConfig>(File.ReadAllText(path));
        }

        private List<SemanticRule> LoadRules(string[] ruleFiles)
        {
            if (ruleFiles == null || ruleFiles.Length == 0)
                return new List<SemanticRule>();

            var paths = ruleFiles.Select(f => Path.Combine(_rulesDir, $"{f}.json")).ToArray();
            return _semantic.LoadRules(paths);
        }
    }
}
