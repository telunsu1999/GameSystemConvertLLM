## Context

ActionResolver 是 GameLoop 第四层——统一执行 Scheduler 和 TriggerSystem 产出的 Action。设计目标：让简单场景零代码配置，复杂场景可编程扩展。

## Goals / Non-Goals

**Goals:**
- 每实体独立持有 ActionResolver
- IAction 接口：CanExecute / Execute(返回子Action) / OnInterrupt
- JSON 配置走 GenericAction（步骤序列）
- 复杂 Action C# 实现 IAction 并 Register 注册
- 中断支持：interruptible + on_interrupt 步骤
- 暴露 Current 状态供外部查询

**Non-Goals:**
- 不处理表现层（Unity 动画/声音）
- 不做网络同步

## Decisions

### Decision 1: IAction 接口

**选择**: `IAction` 接口定义 `CanExecute / Execute(返回子Action) / OnInterrupt`

**备选**: 单一 Execute 方法 + 事件回调。

**理由**: Execute 返回 `IEnumerable<IAction>` 天然表达"goto 产生子 Action arrive"。中断机制见接口。

### Decision 2: JSON GenericAction + C# IAction 共存

**选择**: LoadMapping 加载 JSON → 生成 GenericAction。复杂 Action 通过 Register 覆盖同名的 GenericAction。

**理由**: 90% 场景是简单 Action（改属性+发事件），无需写代码。goto/trade/take_damage 这些有中间状态和条件链的才需要 C#。

### Decision 3: 状态暴露

**选择**: `Current` 属性返回 `CurrentAction` 对象（ActionType / Params / Status / StartedTick）

**理由**: DataCollector 收集 → SemanticEngine 语义化 → LLM 看到"正在去酒馆的路上"。不改现有模块 API。

## Risks / Trade-offs

- [Risk] 中断逻辑复杂（需取消已提交的 scheduler）→ 第一版仅支持简单中断
- [Trade-off] IAction + JSON 双路径增加认知负担 → 文档明确"什么时候用哪个"
