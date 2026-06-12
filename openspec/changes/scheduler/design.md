## Context

Scheduler 是 GameLoop 系统的第二层模块，建立在 WorldClock 之上。解决"谁在什么时间做什么"的调度问题。设计目标：轻量、可组合、与现有语义管道对接。

## Goals / Non-Goals

**Goals:**
- 每个实体独立持有 Scheduler 实例
- 参数化 Action：ActionType + Dictionary<string,object> Params
- tags 支持全局检索和语义化
- 订阅 WorldClock 事件驱动（OnHourChanged / OnTick）
- JSON 配置加载
- 纯查询接口：DueNow() 和 Upcoming()
- 不执行任何动作（只存储和触发查询）

**Non-Goals:**
- 不执行动作（ActionResolver 职责）
- 不包含条件触发（TriggerSystem 职责）
- 不做跨实体调度

## Decisions

### Decision 1: 每个实体一个 Scheduler

**选择**: 每个 NPC/Zone/Resource 持有独立的 Scheduler 实例。

**备选**: 全局 Scheduler 管理所有实体的所有定时器。

**理由**: 实体销毁时 Scheduler 自动释放，无需从全局队列删除。调试时只看一个实体的队列。和属性模块（每 NPC 独立 AttributeModule）设计一致。

### Decision 2: 参数化 Action

**选择**: `ActionType + Params` 替代单一 string。

```csharp
{ ActionType: "goto",    Params: { location: "铁匠铺" } }
{ ActionType: "social",  Params: { activity: "dinner" } }
{ ActionType: "spawn",   Params: { monster: "slime", count: 3 } }
```

**理由**: 避免 string 爆炸（"goto_workshop"/"goto_tavern_lunch"/...）。和 EventSystem 的 `Record(type, tags, data)` 格式统一。

### Decision 3: 订阅 WorldClock 事件驱动

**选择**: Scheduler.Check(snap) 由外部在订阅 WorldClock.OnHourChanged / OnTick 时调用。

**理由**: WorldClock 已有事件机制，不改任何代码。Scheduler 只暴露 Check() 方法，不主动轮询。

### Decision 4: tags 模式

**选择**: 每个 ScheduleItem 携带 tags。

**理由**: 和 AttributeModule.Set(name, value, type, tags)、EventSystem.Record(type, tags, data) 的 tags 模式一致。可全局检索（"所有带 combat 标签的定时任务"）。

## Risks / Trade-offs

- [Trade-off] 大量实体 = 大量 Scheduler 实例 → 每个 Scheduler 队列通常 5-10 条，内存开销可忽略
- [Risk] 不同 Scheduler 订阅不同事件粒度（OnTick vs OnHourChanged）→ 需文档说明
