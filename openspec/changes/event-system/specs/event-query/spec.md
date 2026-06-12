## ADDED Requirements

### Requirement: EventQuery multi-dimension combination
系统 SHALL 支持所有查询维度任意组合，未设置的维度不过滤。

#### Scenario: NPC+Tags+Recent+SourceFilter+Sort 组合
- **WHEN** Query(NpcId="blacksmith", Tags=["mine"], RecentDays=7, SourceFilter=["witnessed"], SortBy="score_desc", Scorer=s, Limit=3)
- **THEN** 所有条件同时生效

#### Scenario: 空查询返回全部
- **WHEN** Query(new EventQuery()) —— 所有字段默认
- **THEN** 返回全部事件，按时间降序

### Requirement: IScoringStrategy interface
系统 SHALL 定义可替换的评分策略接口。

#### Scenario: 自定义评分
- **WHEN** 传入实现了 IScoringStrategy 的对象
- **THEN** Query 使用该策略评分排序

#### Scenario: 默认评分策略
- **WHEN** Scorer=null
- **THEN** 不使用评分，仅按 SortBy 的排序字段排序

### Requirement: Default scorer
系统 SHALL 提供默认评分实现：type 权重 + 时间衰减。

#### Scenario: 死亡事件比天气事件得分高
- **WHEN** 传入默认评分器，比较 death 和 weather 事件
- **THEN** death 事件 score > weather 事件 score

#### Scenario: 近期事件比远古事件得分高
- **WHEN** 同一类型事件，一个 1 天前、一个 100 天前
- **THEN** 1 天前的 score 更高
