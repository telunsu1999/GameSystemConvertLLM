## Why

Scheduler 解决了"什么时候该做什么"，但游戏中大量行为是条件驱动的——"玩家进入视野→打招呼"、"HP归零→死亡"、"踩中陷阱→受伤"。这些不是时间触发的，是状态变化触发的。TriggerSystem 作为每个实体独立持有的条件评估器，在其他模块发生变更时被调用，返回匹配的规则列表。简单的 direct 规则直接产出 Action，llm 规则组装为选项交给 DecisionManager + LLM 做最终选择。

## What Changes

- 新增 `src/TriggerSystem/` 类库项目（netstandard2.1）
- 每实体独立持有 TriggerSystem 实例
- 纯被动：外部调用 `Evaluate()` 查询匹配规则，不轮询、不存储状态
- 双模式：`mode:"direct"` 直接产出 Action，`mode:"llm"` 产出 LLM 选项
- 条件模型独立（不与 SemanticEngine 耦合）
- JSON 配置加载

## Capabilities

### New Capabilities

- `trigger-system`: 条件触发系统。每实体独立持有，被动评估。根据状态变化返回匹配的 direct 规则（直接执行）和 llm 规则（交给 LLM 选择）。

## Impact

- 新增项目：`src/TriggerSystem/TriggerSystem.csproj`（依赖：无）
- 新增配置文件：`configs/triggers/{entity_id}.json`
- 新增测试：`tests/TriggerSystem.Tests/`
- 不影响任何现有模块
