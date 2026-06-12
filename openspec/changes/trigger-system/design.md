## Context

TriggerSystem 是 GameLoop 系统的第三层模块。与 Scheduler（时间驱动）互补，处理条件驱动的行为触发。设计目标：极简、纯函数式、与 Scheduler 保持一致的每实体模式。

## Goals / Non-Goals

**Goals:**
- 每实体独立持有 TriggerSystem 实例
- 纯被动：Evaluate() 由外部调用，不轮询
- 双模式：mode="direct"（直接Action）和 mode="llm"（LLM选项）
- JSON 配置加载
- 条件模型独立（不依赖 SemanticEngine）
- 零内部状态（无冷却、无优先级、无记忆）

**Non-Goals:**
- 不执行动作
- 不调用 LLM
- 不做优先级排序
- 不做冷却管理
- 不做选择——返回所有匹配的规则

## Decisions

### Decision 1: 纯被动评估

**选择**: TriggerSystem 只暴露 `Evaluate(changeType, changeKey, snap, attrs, events)`，由外部（NPC AI / GameLoop）在状态变更后调用。

**备选**: TriggerSystem 订阅其他模块的事件，自主触发。

**理由**: 调用方控制评估时机和频率。同一状态变更可以由调用方决定"触发一次"还是"每 Tick 触发"——TriggerSystem 不替调用方做策略决策。

### Decision 2: 双模式

**选择**: 每条规则配置 `mode: "direct"` 或 `mode: "llm"`。direct 规则直接产出 Action，llm 规则作为 LLM 选项返回。

**理由**: 踩陷阱受伤不需要 LLM（direct），但看到玩家后选择什么态度需要 LLM（llm）。模式是规则的属性，不是 TriggerSystem 的属性。

### Decision 3: 条件模型独立

**选择**: 定义独立的 Condition 模型（Field/Op/Value），不依赖 SemanticEngine。

**理由**: TriggerSystem 是存储层，SemanticEngine 是语义层。保持层级分离。条件评估逻辑简单（数值比较），不必引入额外依赖。

### Decision 4: 零内部状态

**选择**: 不存储冷却、优先级排序、触发历史。

**理由**: 调用方管理状态。如果"今天已经聊过了"，调用方不再调 Evaluate。TriggerSystem 保持纯函数。

## Risks / Trade-offs

- [Trade-off] 不支持 AND 复合条件 → 可通过多条规则（同 triggerKey）模拟。如果需要 OR，配置多条规则即可（Evaluate 返回全部匹配）
