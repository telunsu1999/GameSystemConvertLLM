## Context

WorldClock 是 GameLoop 系统的基础层模块。后续 Scheduler, TriggerSystem, DecisionManager 都依赖时间源。本模块零业务知识，只做时间计算和事件通知。

基于与用户的深入讨论，选定"被动逻辑时钟 + 可配置日历 + 事件驱动"的方案，而非 RimWorld 的连续 Tick 自驱模式。核心理由：LLM 决策需要可预测的触发锚点，而非连续时间流。

## Goals / Non-Goals

**Goals:**
- 提供 Tick 为基础的游戏内时间系统
- JSON 配置：分钟/Tick、天/季、时间块、季节名全可配置
- 单一真相源：只存储 Tick 总数，所有时间属性计算派生
- 事件通知：TimeBlock / Hour / Day / Season / Year 五个层级
- 被动推进：`Advance(n)` 由外部调用
- netstandard2.1 类库，零外部依赖

**Non-Goals:**
- 不包含暂停/恢复/变速逻辑（GameLoop 层职责）
- 不包含序列化/存档
- 不包含多时区或多时钟
- 不包含定时器自驱
- 不处理任何游戏业务（"什么时候该种作物"等）

## Decisions

### Decision 1: 单一真相源——只存 Tick

**选择**: `int tick` 作为唯一存储字段。Minute, Hour, Day, Season, Year, TimeBlock 全部计算派生。

**备选**: 分别存储 year/month/day/hour/minute。

**理由**: 消除跨字段不一致的可能。推进时间只需 `tick += n`。测试简单——验证 `tick=100时 season==Spring` 而非验证多个字段同步。

### Decision 2: 事件按层级从细到粗触发

**选择**: 每个 Tick 内按 TimeBlock → Hour → Day → Season → Year 顺序检查边界跨越。

**理由**: 下游（Scheduler）只需订阅关心的事件（如 OnHourChanged），无需每 Tick 轮询。顺序触发保证跨多层级边界时（如跨天+跨季）事件按粒度有序触发。

### Decision 3: TimeBlock 基于 JSON 配置

**选择**: 一天被切分为可配置的"语义时间块"，如"凌晨/早晨/上午/午后/下午/傍晚/夜晚"。每个块由 start_hour/end_hour 定义。

**理由**: LLM prompt 中"春日午后"比"Day 5, 14:30"更有语义信息。配置化允许不同世界观自定义时间块。

### Decision 4: 配置结构

```json
{
  "minutes_per_tick": 10,
  "days_per_season": 30,
  "seasons": [
    {"name": "Spring", "index": 0},
    {"name": "Summer", "index": 1},
    {"name": "Autumn", "index": 2},
    {"name": "Winter", "index": 3}
  ],
  "time_blocks": [
    {"name": "凌晨", "start_hour": 0, "end_hour": 5},
    {"name": "早晨", "start_hour": 6, "end_hour": 8},
    {"name": "上午", "start_hour": 9, "end_hour": 11},
    {"name": "午后", "start_hour": 12, "end_hour": 13},
    {"name": "下午", "start_hour": 14, "end_hour": 17},
    {"name": "傍晚", "start_hour": 18, "end_hour": 19},
    {"name": "夜晚", "start_hour": 20, "end_hour": 23}
  ]
}
```

## Risks / Trade-offs

- [Risk] Advance(极大值)可能触发大量事件回调 → 外部调用方应控制单次 Advance 的上限
- [Risk] TimeBlock 配置不当（如 gap）→ FromConfig() 构造时验证配置完整性
- [Trade-off] 不支持"连续时间"（如 14:37）→ 对 LLM 决策场景无影响，时间块足够

## Open Questions

- 是否需要 DayOfWeek（星期）？当前日历设计未包含，可后续添加
