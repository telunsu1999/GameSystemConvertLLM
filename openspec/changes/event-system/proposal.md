## Why

NPC决策需要知道"发生过什么"，而不仅仅是"当前状态是什么"。属性模块提供状态，事件系统提供记忆。事件作为独立实体存在，随时间被NPC发现和传播。

## What Changes

- 新增独立项目 `event_system/`（netstandard2.1，零业务依赖）
- `EventSystem`：事件CRUD + 多维感知管理 + 标签检索 + 评分排序
- `EventScorer`：可替换的评分策略接口 + 默认实现
- `EventQuery`：组合查询条件对象，任意维度自由组合

## Capabilities

### New Capabilities
- `event-storage`: 事件记录、感知管理、传播
- `event-query`: 多维度组合查询（NPC/标签/时间/已知状态/来源/排序/limit）

### Modified Capabilities
<!-- 无 -->

## Impact

- 新增项目: `event_system/EventSystem/`
- 新增项目: `event_system/EventScorer/`
- 零外部依赖
- 后续数据收集模块引用此项目
