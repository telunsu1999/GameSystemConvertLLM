## Why

当前系统缺少游戏内时间概念。所有模块（AttributeModule, EventSystem, DataCollector, PromptTemplate）都能处理NPC状态和事件，但没有任何东西能回答"现在是什么时候"。后续的Scheduler、TriggerSystem、GameLoop 都需要一个统一的时间源。WorldClock作为最底层、零依赖的模块优先实现。

## What Changes

- 新增 `src/WorldClock/` 类库项目（netstandard2.1）
- 单一真相源：只存 Tick 总数，所有时间属性（分钟/小时/日/季/年/时段）均为计算派生
- JSON 配置驱动：分钟/Tick、天/季、季节名称、时间块定义均可配置
- 事件系统：OnTick, OnTimeBlockChanged, OnHourChanged, OnDayChanged, OnSeasonChanged, OnYearChanged
- 被动推进：`Advance(n)` 由外部调用，不自主运行

## Capabilities

### New Capabilities

- `world-clock`: 游戏内时间系统。提供 Tick 为基础的被动时钟，支持配置化的日历制度和时间块，通过事件通知外部时间边界变化。

### Modified Capabilities

<!-- None - 新模块，不修改现有 capability -->

## Impact

- 新增项目：`src/WorldClock/WorldClock.csproj`（依赖：无）
- 新增配置文件：`configs/clock/world_clock.json`
- 新增测试：`tests/WorldClock.Tests/`
- 不影响任何现有模块
