using System.Collections.Generic;

namespace GameLoop
{
    public interface IDataCollector
    {
        CollectResult Collect(string npcId, string configName);
        CollectResult Collect(string npcId, CollectConfig config);
        Dictionary<string, object> CollectRaw(string npcId, CollectConfig config);
    }
}
