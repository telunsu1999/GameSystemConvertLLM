## Why

WorldClock 提供了时间源，但缺少"什么时间该发生什么"的调度层。NPC 的日程、怪物的刷新、资源的重生、限时事件的触发——所有这些都需要一个通用的定时调度机制。Scheduler 作为每个实体独立持有的优先队列，通过订阅 WorldClock 事件驱动。

## What Changes

- 新增 `src/Scheduler/` 类库项目（netstandard2.1）
- 每个实体持有独立的 Scheduler 实例，实体销毁则 Scheduler 随之释放
- 参数化的 Action：`ActionType + Params` 避免 string 爆炸
- tags 支持全局查询和语义化（与 EventSystem/AttributeModule 对齐）
- 订阅 WorldClock.OnHourChanged / OnTick 驱动
- 纯存储+查询，不执行动作（执行交给 ActionResolver）
- 配置文件：JSON 定义每一类实体的定时任务列表

## Capabilities

### New Capabilities

- `scheduler`: 通用定时调度系统。支持每个实体独立持有的优先队列，参数化的 Action（ActionType + Params + Tags），通过订阅 WorldClock 事件驱动。

## Impact

- 新增项目：`src/Scheduler/Scheduler.csproj`（依赖：无，仅 netstandard2.1）
- 新增配置文件：`configs/schedules/{entity_id}.json`
- 新增测试：`tests/Scheduler.Tests/`
- 不影响任何现有模块
- 依赖 WorldClock 模块（Phase 1）的事件接口
