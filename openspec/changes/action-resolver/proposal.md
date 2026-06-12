## Why

Scheduler 和 TriggerSystem 都产出"该执行什么"，但缺少统一的执行层。ActionResolver 作为动作调度器，管理单个 Action 的生命周期（启动/执行/中断/完成），支持 JSON 配置的简单 Action 和 C# IAction 的复杂 Action，并将当前执行状态暴露给语义管道供 LLM 参考。

## What Changes

- 新增 `src/ActionResolver/` 类库项目（netstandard2.1）
- IAction 接口定义动作生命周期
- JSON 配置支持 GenericAction（简单步骤序列）
- 复杂 Action 通过 C# IAction 类实现（goto / trade / take_damage）
- 支持中断机制（interruptible + on_interrupt）
- 暴露当前执行状态（Current getter）供 DataCollector 收集
- 每实体独立持有 ActionResolver 实例

## Capabilities

### New Capabilities

- `action-resolver`: 动作执行调度系统。每实体独立持有，管理 Action 的完整生命周期。JSON 配置简单 Action，C# IAction 实现复杂 Action。支持中断，暴露当前状态。

## Impact

- 新增项目：`src/ActionResolver/ActionResolver.csproj`（依赖：AttributeModule, EventSystem）
- 新增配置文件：`configs/actions/{entity_id}.json`
- 新增测试：`tests/ActionResolver.Tests/`
