## Why

需要一个独立、可外挂的属性模块，为任意实体（NPC/玩家/物品）提供 key-value 属性存储。属性带类型和标签，支持按标签批量检索，为后续的语义引擎和数据收集模块提供数据源。

## What Changes

- 新增独立项目 `attribute_module/`（netstandard2.1，零业务依赖）
- 核心类 `AttributeModule`：属性的增删改查，tag 反向索引
- 核心类 `AttributeValue`：{value, type, tags}
- 类型强制校验（number/string/bool）
- 不存在返回 null，不返回默认值

## Capabilities

### New Capabilities
- `attribute-storage`: 属性 CRUD + 标签索引 + 批量检索 + 导入导出

### Modified Capabilities
<!-- 无 -->

## Impact

- 新增项目: `attribute_module/AttributeModule/`
- 零外部依赖
- 后续语义引擎、数据收集模块引用此项目
