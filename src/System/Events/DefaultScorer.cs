using System;
using System.Collections.Generic;

namespace GameLoop
{
    /// <summary>
    /// 濮掓稒顭堥鑽ゆ嫚閸曨偄鐎荤紒娑欑墱閺嗘劙鏁嶅杈潶闁搞劌顑嗗鍫ユ煂?+ 闁哄啫鐖煎Λ璺ㄦ偘閺夊灝娅?
    /// </summary>
    public class DefaultScorer : IScoringStrategy
    {
        private static readonly Dictionary<string, double> _typeWeights = new Dictionary<string, double>
        {
            ["death"]          = 10,
            ["combat"]         = 8,
            ["quest_complete"] = 7,
            ["treasure_found"] = 6,
            ["trade"]          = 5,
            ["social"]         = 3,
            ["weather"]        = 1
        };

        public double Score(Event evt)
        {
            var typeWeight = _typeWeights.TryGetValue(evt.Type, out var w) ? w : 0;
            var daysAgo = (DateTime.Now - evt.Time).TotalDays;
            var decay = Math.Pow(0.95, Math.Max(0, daysAgo));
            return typeWeight * decay;
        }
    }
}
