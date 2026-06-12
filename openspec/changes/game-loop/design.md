# GameLoop Architecture Design

## Context

当前系统由5个独立模块组成（AttributeModule, EventSystem, SemanticEngine, DataCollector, PromptTemplate），
每个模块都是纯函数式工具——它们处理数据但不驱动流程。缺少一个"心跳"来：

- 推进游戏时间
- 调度NPC日常行为
- 检测决策触发条件
- 编排LLM流水线
- 将LLM决策落地为世界状态变更

GameLoop是这个"心跳"——独立于Unity引擎，可单独测试，不包含任何业务知识。

## Goals / Non-Goals

**Goals:**
- 可独立运行、测试的C#库（netstandard2.1）
- 基于Tick的时间推进模型
- 配置驱动的NPC时间表和触发器
- 异步LLM决策编排（适配后续LLMClient）
- 决策结果落地为属性变更和事件产生
- 与现有5个模块对接

**Non-Goals:**
- 不包含游戏渲染/UI
- 不实现LLM调用本身（由LLMClient模块负责）
- 不做实时多人同步
- 不实现物理/碰撞/寻路

## Architecture Overview

```mermaid
flowchart TB
    subgraph "外部调用"
        GAME[Game Engine / Demo]
    end

    subgraph "GameLoop (新增)"
        CLK[WorldClock]
        SCH[Scheduler]
        TRG[TriggerSystem]
        DEC[DecisionManager]
        RES[ActionResolver]
        LOOP[LoopController]
    end

    subgraph "现有模块"
        ATTR[AttributeModule]
        EVT[EventSystem]
        DC[DataCollector]
        PT[PromptTemplate]
    end

    subgraph "待实现"
        LLM[LLMClient]
    end

    subgraph "WorldState"
        GLOB[GlobalState]
        NPC1[PerNpcState_1]
        NPC2[PerNpcState_2]
    end

    GAME -->|Tick(n)| LOOP
    LOOP --> CLK
    CLK -->|time changed| SCH
    CLK -->|time changed| TRG
    SCH -->|npc moved| TRG
    TRG -->|decide needed| DEC
    DEC -->|collect context| DC
    DC --> ATTR
    DC --> EVT
    DC -->|raw data| DEC
    DEC -->|render prompt| PT
    PT -->|prompt text| DEC
    DEC -->|async request| LLM
    LLM -->|choice index| RES
    RES -->|apply changes| ATTR
    RES -->|produce events| EVT
```

## Module Breakdown

### 1. WorldClock (世界时钟)

```
职责: 维护游戏内时间，处理Tick推进
输入: Tick(n) 调用
输出: 当前游戏时间（时/日/月/季/年），时间变更事件
```

| 特性 | 说明 |
|------|------|
| 时间单位 | 1 Tick = 10分钟（可配置） |
| 一日 | 144 Tick (24h) |
| 一季 | 30日（可配置） |
| 一年 | 4季 |
| 推进方式 | `Advance(n)` 一口气推进n个Tick，每个Tick触发回调 |

```csharp
class WorldClock {
    int Tick { get; }
    int Hour { get; }       // 0-23
    int Day { get; }        // 1-30
    Season Season { get; }   // Spring/Summer/Autumn/Winter
    int Year { get; }

    void Advance(int ticks); // 推进n个Tick，每Tick触发 OnTick 事件
    event Action<int> OnTick;
}
```

### 2. Scheduler (NPC日程调度)

```
职责: 根据JSON配置的每日时间表，在相应时间将NPC移动到目标位置
输入: 当前Tick + NPC配置
输出: NPC位置变更事件
```

**配置文件结构** (`configs/schedules/{npc_id}.json`):

```json
{
  "npc_id": "blacksmith",
  "schedule": [
    { "hour": 6,  "location": "铁匠铺", "activity": "work_start" },
    { "hour": 12, "location": "酒馆",   "activity": "lunch" },
    { "hour": 14, "location": "铁匠铺", "activity": "work_afternoon" },
    { "hour": 18, "location": "酒馆",   "activity": "social" },
    { "hour": 22, "location": "家",     "activity": "sleep" }
  ]
}
```

Tick每推进一格，Scheduler检查每个NPC："当前时间是否跨过了下一个schedule时间点？" 如果是，更新NPC的location属性并产生position_change事件。

### 3. TriggerSystem (条件触发器)

```
职责: 检查触发条件，当满足时发出决策请求
输入: Tick + WorldState + NPC状态
输出: 待决策的NPC列表 + 触发原因 + 场景类型
```

**触发类型:**

| 类型 | 条件 | 示例 |
|------|------|------|
| `schedule_change` | NPC到达schedule新时间点 | 6:00 → 到达铁匠铺，检查今天的生产计划 |
| `player_approach` | 玩家进入NPC感知范围 | 玩家走到铁匠面前 |
| `state_alert` | NPC属性跨过阈值 | HP < 20% |
| `event_reaction` | 新事件被NPC感知 | 听说矿洞出事了 |
| `periodic` | 每N Tick自动检查 | 每2小时思考一次当前状态 |
| `combat_turn` | 战斗中轮到该NPC | 战斗回合制决策 |

**触发器配置** (`configs/triggers/{trigger_set}.json`):

```json
{
  "trigger_set": "combat_triggers",
  "rules": [
    {
      "type": "state_alert",
      "condition": {"field":"hp_ratio","op":"lt","value":0.2},
      "scene": "combat",
      "priority": 10
    },
    {
      "type": "periodic",
      "interval_ticks": 12,
      "scene": "idle_check",
      "priority": 1
    }
  ]
}
```

### 4. DecisionManager (LLM决策编排)

```
职责: 收集"需要决策"的NPC → 跑完整LLM流水线 → 收集结果
输入: 待决策NPC列表 + 触发原因
输出: DecisionResult列表（NPCID + LLM选择的行动序号）
```

**决策流程（单个NPC）:**

```
触发原因 → 选择CollectConfig(基于scene类型)
         → DataCollector.Collect(npcId, config)
         → 选择模板(attrs.prompt_template)
         → PromptTemplate.Build(attrs, collected, task, options)
         → LLMClient.Send(prompt) [异步]
         → 解析返回的数字 → DecisionResult
```

**优先级队列:**

```
10: combat    (战斗决策，需立即处理)
 8: interact  (玩家交互，近乎实时)
 5: event_rx  (事件反应，可延迟1-2 Tick)
 3: sched_chg (日程触发，可延迟)
 1: periodic  (周期性检查，批量处理)
```

高优先级同步等结果，低优先级进入异步队列——下个Tick收结果。

### 5. ActionResolver (行动落地)

```
职责: LLM选了几号 → 查映射表 → 修改属性/产生事件
输入: NPCID + choice_index + 场景配置
输出: 无（副作用：修改AttributeModule和EventSystem）
```

**行动映射配置** (`configs/actions/{action_set}.json`):

```json
{
  "scene": "blacksmith_work",
  "options": [
    { "index": 0, "action": "trade",   "params": {"item":"iron","amount":5,"cost":50} },
    { "index": 1, "action": "goto",    "params": {"location":"矿洞"} }
  ],
  "effects": {
    "trade": {
      "attr_changes": {"gold":"decrease","iron_owned":"increase"},
      "events": [{"type":"trade","tags":["iron","economy"]}]
    },
    "goto": {
      "attr_changes": {"location":"change"},
      "events": [{"type":"npc_move","tags":["travel"]}]
    }
  }
}
```

### 6. LoopController (主循环控制器)

```
职责: 组装上述5个模块，暴露统一API
输入: Tick(n)
输出: TickResult（本Tick发生的所有变化摘要）
```

## Data Flow (单Tick 生命周期)

```
Tick(n) 调用
  |
  v
1. WorldClock.Advance(n)
   |-- 每个子Tick:
       |-- 2. Scheduler.CheckAll() → NPC位置变更
       |-- 3. TriggerSystem.CheckAll() → 待决策列表
       |-- 4. DecisionManager.Process(列表) → 异步发LLM请求
       |     |-- 高优先级: 同步等结果
       |     |-- 低优先级: 入队列，上轮结果出队列
       |-- 5. ActionResolver.Apply(本轮结果) → 属性/事件变更
       |-- 6. 收集所有变更 → TickSummary
  |
  v
返回 TickResult[]
```

## Key Architecture Decisions

### Decision 1: Tick模型 — 逻辑时间批量推进

**选择**: 1 Tick = 10分钟游戏时间，外部调用 `Tick(n)` 一口推进n个Tick

**理由**:
- 适合星露谷物语风格（时间按格子跳）
- 不和现实时间绑定，方便测试
- 批量推进时LLM低优先级决策不阻塞

### Decision 2: WorldState — 分层设计

**选择**: GlobalState(时间/天气/区域) + PerNpcState(每个NPC独立AttributeModule)

**理由**:
- 与现有AttributeModule完全兼容
- NPC之间天然隔离
- 全局状态通过EventSystem共享（NPC"感知"天气变化）

### Decision 3: 触发器 — 混合模型

**选择**: Scheduler(时间驱动) + TriggerSystem(条件驱动)

**理由**:
- 日程是NPC行为的骨架，用时间驱动最自然
- 其他触发（状态/事件/交互）用条件驱动，灵活
- 两者都产生"决策请求"，统一进入DecisionManager

### Decision 4: LLM执行 — 优先级异步

**选择**: 高优先级同步等结果，低优先级异步队列

| 优先级 | 处理模式 | 延迟 |
|--------|---------|------|
| combat(10) | 同步 | 0 Tick |
| interact(8) | 同步 | 0 Tick |
| event_rx(5) | 异步 | 1-2 Tick |
| sched_chg(3) | 异步批量 | 1 Tick |
| periodic(1) | 异步批量 | 可跨Tick |

**理由**:
- 战斗/交互需要即时响应
- 日常思考可以批量处理，减少LLM调用频率
- 异步模式允许LLM调用失败重试而不卡游戏

### Decision 5: ActionResolver — 配置驱动映射

**选择**: JSON配置 `choice_index → action → effects`

**理由**:
- LLM只返回数字（简单、可靠）
- 数字→行动的映射由配置文件定义
- 同一场景可切换不同映射（不同NPC性格 → 不同效果表）
- 不需要重新调LLM就能改行动效果

## Config File Map

```
configs/
  schedules/           ← 新增: NPC日程
    blacksmith.json
    merchant.json
  triggers/            ← 新增: 触发规则
    combat.json
    daily_life.json
  actions/             ← 新增: 行动映射
    blacksmith_work.json
    combat_generic.json
  collect/             ← 已有: 收集配置
    dungeon_scene.json
    fft_combat.json
    fft_social.json
  semantic/            ← 已有: 语义规则
    15 files
```

## Integration Points (与现有模块)

| GameLoop模块 | 依赖 | 调用方式 |
|-------------|------|---------|
| Scheduler | AttributeModule(NPC) | `attrs.Set("location", ...)` |
| Scheduler | EventSystem | `events.Record("position_change", ...)` |
| TriggerSystem | AttributeModule(NPC) | `attrs.Get("hp")` 读状态 |
| TriggerSystem | EventSystem | `events.Query(...)` 检查新事件 |
| DecisionManager | DataCollector | `dc.Collect(npcId, config)` |
| DecisionManager | PromptTemplate | `pt.Build(attrs, collected, task, options)` |
| ActionResolver | AttributeModule(NPC) | `attrs.Set(...)` 写属性 |
| ActionResolver | EventSystem | `events.Record(...)` + `events.Perceive(...)` |

## API Surface

```csharp
public class GameLoop {
    // 初始化
    GameLoop(GameLoopConfig config);  // 加载所有schedule/trigger/action配置

    // 注册NPC
    void RegisterNpc(string npcId, AttributeModule attrs);

    // 推进时间
    List<TickResult> Tick(int count);

    // 玩家交互（高优先级同步）
    int Interact(string npcId, string scene, List<string> options);
}

public class TickResult {
    string Tick;
    List<string> NpcMoved;         // "blacksmith → 铁匠铺"
    List<string> TriggersFired;    // "blacksmith: schedule_change"
    List<string> DecisionsMade;    // "blacksmith → 收购铁锭"
    List<string> EventsProduced;   // "trade: 铁锭+5"
}
```

## Test Strategy

1. **WorldClock 单元测试**: 推进Tick验证时间计算、跨越日/季边界
2. **Scheduler 单元测试**: 加载配置文件、验证NPC在正确时间移动
3. **TriggerSystem 单元测试**: 各类型触发器条件判断
4. **ActionResolver 单元测试**: LLM选择→行动映射→属性/事件变化
5. **集成测试**: 完整Tick→结果流水线（不依赖真实LLM，用Mock替）
6. **Demo**: 模拟铁匠一天的生活（6:00工作 → 12:00吃饭 → 18:00社交）

## 模块边界

```
GameLoop (新增)
  依赖: AttributeModule, EventSystem, DataCollector, PromptTemplate
  不依赖: SemanticEngine (通过DataCollector间接用)
          LLMClient (未来依赖，接口预留)
          Unity引擎
  对外暴露: GameLoop类 (Unity通过调用 Tick/Interact 来驱动)
```
