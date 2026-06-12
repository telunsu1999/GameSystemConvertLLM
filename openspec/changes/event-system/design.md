## Context

事件系统是纯存储层。事件独立存在，通过感知列表记录哪些NPC知道了该事件。传播由外部游戏行为驱动（NPC对话、玩家告知等）。评分模块独立可替换。

## Goals / Non-Goals

**Goals:**
- 事件ID由模块内部生成
- 感知分两步：Record(无人感知) → Perceive(添加感知)
- 查询条件对象支持多维度任意组合
- 评分内嵌到Query中，策略可替换
- 不修改属性（事件只是记忆）
- 类型/标签/How 不硬编码

**Non-Goals:**
- 不自动传播
- 不处理坐标
- 不持久化（内存）
- 不限制事件数量

## Decisions

### D1: 事件ID内部生成

Record 返回 GUID 字符串。调用方持此ID做后续 Perceive/Spread。

### D2: Perceive 和 Spread 分离

Perceive: NPC通过某种方式得知事件（目睹/发现痕迹/听说）。
Spread: 已知道事件的NPC告诉另一个NPC。

### D3: EventQuery 组合查询

```csharp
var q = new EventQuery {
    NpcId = "blacksmith",
    Tags = new[]{"mine"},
    RecentDays = 7,
    Known = true,
    SourceFilter = new[]{"witnessed","told_by"},
    SortBy = "score_desc",
    Scorer = myScorer,
    Limit = 5
};
```

所有字段可选。null字段在过滤时跳过。

### D4: IScoringStrategy 可替换

```csharp
public interface IScoringStrategy {
    double Score(Event evt);
}
```

默认实现：type权重 + recency衰减。外部可实现自定义策略。

### D5: Query默化值

SourceFilter=null → 不过滤来源。
Known=null → 不过滤已知/未知。
SortBy默认"time_desc"。
Scorer=null → 只用SortBy排序（不调评分）。
Limit=null → 返回全部。

## Risks / Trade-offs

- [Risk] 感知列表随传播增长 → 每个事件维护 NPC 列表，千级 NPC 时需关注。当前原型几十个 NPC 无压力。
- [Risk] 评分策略复杂时可拖慢 Query → Mitigation: 可替换为轻量策略
