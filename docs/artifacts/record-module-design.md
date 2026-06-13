# RecordModule 架构（Revised）

## 核心思路

RecordModule 只做一件事：**维护一个 NPC 的"已知事件"列表**。事件的来源由外部调用者决定，接口不假设来源类型。

---

## 数据模型

```csharp
/// 记录来源 — 完全由外部传入，不预设类型
public class RecordSource
{
    public string Method { get; set; }      // "self" | "witnessed" | "overheard" | "told" | "rumored" | "inferred"
    public string FromNpcId { get; set; }   // 谁做的/谁说的 (self 时为 null)
    public float Reliability { get; set; }  // 0-1，信息来源可靠度
    public List<SourceRevision> History { get; set; } // 来源变更历史
}

public class SourceRevision
{
    public string Method;         // 之前是什么来源
    public string NewMethod;      // 更新为什么来源
    public float NewReliability;  // 更新后的可靠度
    public int UpdatedAt;         // tick
    public string Reason;         // 为什么更新 ("witnessed_myself", "confirmed_by_guard", ...)
}

public class Record
{
    public string Id;
    public string Type;                     // "talk", "combat", "travel", "trade"...
    public string[] Tags;                   // ["social"], ["danger"], ...
    public Dictionary<string, object> Data; // 自由数据
    public int Tick;                        // 发生的 tick
    public RecordSource Source;             // 来源信息
}
```

---

## CRUD 接口

```csharp
// ============================================================
// 写入
// ============================================================

/// 记录一件事。source 完全由调用方构造。
Record Remember(RecordSource source, string type, string[] tags, 
                Dictionary<string, object> data, int tick)

/// 更新已有记录的来源信息（如：rumored → witnessed）。
/// 同一个事实，信息来源可以升级。
Record Revise(string recordId, RecordSource newSource, int tick, string reason)

/// 按内容查找已有记录（同类型+同标签+数据匹配），用于去重。
Record FindExisting(string type, string[] tags, Dictionary<string, object> dataMatch)

// ============================================================
// 查询
// ============================================================

/// 按标签+天数查询，按时间倒序。
List<Record> Recall(string[] tags, int recentDays, int? limit)

/// 最近 N 条（不限标签）。
List<Record> Recent(int count)

/// 按来源方法查询（如：我只想看"亲眼看到的"）。
List<Record> RecallByMethod(string method, int recentDays, int? limit)

/// 按可靠度查询（如：可靠度 > 0.7 的才信）。
List<Record> RecallByReliability(float minReliability, int recentDays, int? limit)

// ============================================================
// 遗忘
// ============================================================

/// 保留含 keepTags 的记录，删除其余。
int ForgetExcept(string[] keepTags, int olderThanDays)

/// 删除含 dropTags 的记录。
int ForgetTags(string[] dropTags, int olderThanDays)

/// 删除 olderThanDays 之前的全部记录。
int ForgetOlder(int olderThanDays)

// ============================================================
// 语义输出
// ============================================================

/// 生成供 LLM prompt 使用的文本摘要。
string Summarize(string[] tags, int recentDays, int maxRecords)
```

---

## 使用示例

```csharp
var rm = entity.Get<RecordModule>();

// 1. 自己做的事
rm.Remember(
    new RecordSource { Method = "self", Reliability = 1.0f },
    "talk", new[]{"social"}, 
    new Dictionary<string,object>{{"to","merchant"},{"topic","price"}}, 
    tick: 42
);

// 2. 亲眼看到的
rm.Remember(
    new RecordSource { Method = "witnessed", FromNpcId = "guard", Reliability = 0.95f },
    "combat", new[]{"danger","combat"},
    new Dictionary<string,object>{{"target","wolf"},{ "result","fled"}},
    tick: 40
);

// 3. 偷听到的
rm.Remember(
    new RecordSource { Method = "overheard", FromNpcId = "merchant", Reliability = 0.6f },
    "talk", new[]{"social","secret"},
    new Dictionary<string,object>{{"content","price will rise"}},
    tick: 38
);

// 4. 后来亲眼证实了偷听到的事 → 来源升级
rm.Revise(recordId,
    new RecordSource { Method = "witnessed", FromNpcId = "merchant", Reliability = 0.95f },
    tick: 45, reason: "saw_it_myself"
);
// → Record.Source.Method: "overheard" → "witnessed"
// → Record.Source.Reliability: 0.6 → 0.95
// → Record.Source.History += [{Method:"overheard", NewMethod:"witnessed", ...}]

// 5. 听别人说的（谣言）
rm.Remember(
    new RecordSource { Method = "rumored", FromNpcId = "traveler", Reliability = 0.3f },
    "event", new[]{"world","rumor"},
    new Dictionary<string,object>{{"content","dragon spotted"}},
    tick: 30
);

// 6. 查询：最近7天的社交事件（只信可靠度 > 0.5 的）
var reliableSocial = rm.RecallByReliability(0.5f, 7, limit: 5)
    .Where(r => r.Tags.Contains("social"));

// 7. 遗忘：保留 social 和 danger，删掉7天前的其余记忆
rm.ForgetExcept(new[]{"social", "danger"}, olderThanDays: 7);

// 8. 遗忘：删掉7天前的日常琐事
rm.ForgetTags(new[]{"daily"}, olderThanDays: 7);
```

---

---

## 来源升级链

```
rumored (可靠度 0.3)
  └─ Revise() → told (可靠度 0.7, 朋友确认了)
       └─ Revise() → witnessed (可靠度 0.95, 亲眼看到了)
            └─ Revise() → self (可靠度 1.0, 自己参与了)

RecordSource.History 记录完整升级轨迹:
  [{Method:"rumored", NewMethod:"told", NewReliability:0.7, Reason:"confirmed_by_guard"},
   {Method:"told", NewMethod:"witnessed", NewReliability:0.95, Reason:"saw_it_myself"}]
```

## 与现有系统的迁移映射

| 旧 EventSystem API | 新 RecordModule API |
|-------------------|-------------------|
| `Record(type, tags, data)` | `Remember(new RecordSource{Method="self",Reliability=1}, type, tags, data, tick)` |
| `Perceive(eventId, npcId, how, from)` | 删除。感知直接走 `Remember(source, ...)` |
| `Spread(eventId, from, to, how)` | `targetNpc.RecordModule.Remember(new RecordSource{Method=how, FromNpcId=from, Reliability=...}, ...)` |
| `Query(npcId, tags, days, limit)` | `npc.RecordModule.Recall(tags, days, limit)` |

---

## 关键对比

| | 旧 EventSystem | 新 RecordModule |
|---|---|---|
| 事件归属 | 全局 → 通过 Perception 间接关联 NPC | 直接属于 NPC |
| 来源建模 | Perceive/Spread 两步操作 | `RecordSource` 一次性描述 |
| 遗忘 | 不支持 | `ForgetExcept` / `ForgetTags` / `ForgetOlder` |
| 可靠度 | 不支持 | `Reliability` 字段 + `RecallByReliability` |
| 扩展来源 | 需加新方法 | 只需传不同的 `RecordSource.Method` 字符串 |
