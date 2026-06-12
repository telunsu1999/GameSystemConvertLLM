## ADDED Requirements

### Requirement: Set creates or overwrites an attribute
系统 SHALL 提供 Set(name, value, type, tags) 方法。类型不匹配 MUST 抛出 ArgumentException。

#### Scenario: 创建新属性
- **WHEN** Set("currentHP", 85, "number", ["vital","hp"])
- **THEN** Get("currentHP") 返回 {value:85, type:"number", tags:["vital","hp"]}

#### Scenario: 覆盖已有属性
- **WHEN** 已有 currentHP=85，调用 Set("currentHP", 50, "number", ["vital","hp","combat"])
- **THEN** 值变为50，tags更新，旧tags从索引中移除新tags加入

#### Scenario: 类型不匹配
- **WHEN** Set("hp", "hello", "number", [])
- **THEN** 抛出 ArgumentException

### Requirement: SetValue only changes value
系统 SHALL 提供 SetValue(name, value) 方法。属性 MUST 已存在且类型匹配。

#### Scenario: 修改数值
- **WHEN** 已有 currentHP=85，调用 SetValue("currentHP", 50)
- **THEN** value=50，type和tags不变

#### Scenario: 属性不存在
- **WHEN** 调用 SetValue("nonexistent", 100)
- **THEN** 抛出 ArgumentException

### Requirement: SetTags only changes tags
系统 SHALL 提供 SetTags(name, tags) 方法。MUST 更新 tag 索引。

#### Scenario: 修改标签
- **WHEN** 已有 currentHP tags=["vital","hp"]，调用 SetTags("currentHP", ["vital","hp","debuff"])
- **THEN** tags 更新，tagIndex 中 "debuff" 新增 currentHP

### Requirement: Remove deletes attribute
系统 SHALL 提供 Remove(name) 方法。MUST 同时清理tag索引。

#### Scenario: 删除属性
- **WHEN** 已有 currentHP，调用 Remove("currentHP")
- **THEN** Get("currentHP") 返回 null，tagIndex 中不再包含 currentHP

### Requirement: Get returns null for missing attributes
系统 SHALL 返回 null 而非默认值。

#### Scenario: 查询不存在的属性
- **WHEN** Get("nonexistent")
- **THEN** 返回 null

### Requirement: GetByTags filters by tag intersection
系统 SHALL 利用 tag 索引高效检索。

#### Scenario: 单tag检索
- **WHEN** 已有 currentHP tags=["vital","hp"]，maxHP tags=["vital","hp"]，gold tags=["currency"]
- **WHEN** 调用 GetByTags(["vital"])
- **THEN** 返回 {currentHP, maxHP}

#### Scenario: 多tag交集
- **WHEN** 调用 GetByTags(["vital","combat"])
- **THEN** 返回所有包含 vital 或 combat 的属性（并集）

### Requirement: LoadFrom merges attributes
系统 SHALL 合并导入：同名覆盖，独有保留。

#### Scenario: 部分覆盖
- **WHEN** 已有 currentHP=85，调用 LoadFrom({currentHP:50, strength:12})
- **THEN** currentHP=50（覆盖），strength=12（新增）

### Requirement: Export returns all attributes
系统 SHALL 导出全部属性为 Dictionary<string,AttributeValue>。

#### Scenario: 导出
- **WHEN** 有 currentHP 和 gold 两个属性
- **THEN** Export() 返回包含两者的字典
