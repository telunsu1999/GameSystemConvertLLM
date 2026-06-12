## ADDED Requirements

### Requirement: Collect with config file
系统 SHALL 支持从配置文件加载 CollectConfig 后收集数据。

#### Scenario: 配置驱动收集
- **WHEN** 调用 Collect("blacksmith", "dungeon_scene")
- **THEN** 从 configs/collect/dungeon_scene.json 加载配置，按配置收集属性和事件

#### Scenario: 配置文件不存在
- **WHEN** Collect("smith", "nonexistent")
- **THEN** 抛出 FileNotFoundException

### Requirement: Collect with direct config
系统 SHALL 支持直接传入 CollectConfig 对象收集数据。

#### Scenario: 接口直调
- **WHEN** 调用 Collect("smith", new CollectConfig{AttrTags=["vital"], ...})
- **THEN** 返回包含属性和事件的 CollectResult

### Requirement: CollectRaw returns raw data only
系统 SHALL 提供 CollectRaw 方法，只收集原始数据不调用语义引擎。

#### Scenario: 只收集不语义化
- **WHEN** 调用 CollectRaw(npcId, config)
- **THEN** 返回的 dict 包含属性值和 memory_events 数组，不含语义化文本

### Requirement: Event flattening
系统 SHALL 将事件展平为语义引擎可用的格式，包含 type/source/from 和事件 data 内容。

#### Scenario: 事件展平
- **WHEN** 某NPC的感知how="rumor"、from="guard"，事件data={who:"矿工"}
- **THEN** memory_events 中对应条目为 {type:"...", source:"rumor", from:"guard", who:"矿工"}
