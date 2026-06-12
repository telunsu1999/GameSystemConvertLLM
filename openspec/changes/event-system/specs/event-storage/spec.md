## ADDED Requirements

### Requirement: Record creates an event
系统 SHALL 提供 Record(type, tags, data) 方法，内部生成唯一ID并返回。

#### Scenario: 记录死亡事件
- **WHEN** Record("death", ["mine","danger"], {who:"矿工"})
- **THEN** 返回非空事件ID，事件 time 为当前时间

#### Scenario: 初始无感知
- **WHEN** Record 后立即调用 Query(NpcId=任意)
- **THEN** 返回空（无人感知）

### Requirement: Perceive adds a perception
系统 SHALL 在指定事件上添加感知记录。

#### Scenario: NPC目击事件
- **WHEN** Perceive(eventId, "guard", "witnessed")
- **THEN** Query(NpcId="guard") 返回该事件

#### Scenario: 重复感知
- **WHEN** 同一NPC对同一事件多次调用 Perceive
- **THEN** 追加感知条目（不覆盖）

### Requirement: Spread propagates knowledge
系统 SHALL 支持一个NPC将已知事件传播给另一个NPC。

#### Scenario: 卫兵告诉铁匠
- **WHEN** Perceive(eventId, "guard", "witnessed") 后调用 Spread(eventId, "guard", "blacksmith", "told_by")
- **THEN** 铁匠的感知中 how="told_by", from="guard"

#### Scenario: 源NPC未知事件时传播失败
- **WHEN** Spread(eventId, "guard", "blacksmith") 但 guard 不知道此事件
- **THEN** 抛出异常或忽略

### Requirement: Query with NPC filter
系统 SHALL 支持按 NPC ID 过滤事件。

#### Scenario: 查询某NPC已知事件
- **WHEN** Query(NpcId="blacksmith", Known=true)
- **THEN** 仅返回该NPC在感知列表中的事件

### Requirement: Query with Tags filter
系统 SHALL 支持按标签过滤（事件 tags 与查询 tags 取交集）。

#### Scenario: 查询矿洞相关事件
- **WHEN** Query(Tags=["mine"])
- **THEN** 返回所有 tags 含 "mine" 的事件

### Requirement: Query with RecentDays
系统 SHALL 支持按时间过滤最近N天的事件。

#### Scenario: 最近7天
- **WHEN** Query(RecentDays=7)
- **THEN** 仅返回7天内的事件

### Requirement: Query with SourceFilter
系统 SHALL 支持按感知方式过滤。

#### Scenario: 只查亲眼目睹的
- **WHEN** Query(NpcId="guard", SourceFilter=["witnessed"])
- **THEN** 仅返回 guard 的感知中 how="witnessed" 的事件

### Requirement: Query sorting
系统 SHALL 支持按时间或评分排序。

#### Scenario: 按评分降序
- **WHEN** Query(SortBy="score_desc", Scorer=myScorer)
- **THEN** 结果按 Score 降序排列

#### Scenario: 按时间升序
- **WHEN** Query(SortBy="time_asc")
- **THEN** 早的事件在前

### Requirement: Query limit
系统 SHALL 支持限制返回数量。

#### Scenario: 只要前5条
- **WHEN** Query(Tags=["mine"], Limit=5, SortBy="time_desc")
- **THEN** 返回最多5条
