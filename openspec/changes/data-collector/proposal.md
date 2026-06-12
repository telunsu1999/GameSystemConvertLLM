## Why

属性模块和事件系统各自独立，需要聚合层统一收集数据并喂给语义引擎。DataCollector 是这两者的胶水，同时支持配置驱动和接口直调。

## What Changes

- 新增 `data_collector/` 项目，依赖 attribute_module + event_system + semantic_engine
- `Collect(npcId, config)` → 收集属性+事件 → 语义化 → CollectResult
- `CollectRaw(npcId, config)` → 只收集原始数据，不语义化
- 配置文件统一到 `configs/` 目录

## Capabilities

### New Capabilities
- `data-collection`: 属性+事件聚合 + 语义化，支持配置驱动和接口直调

### Modified Capabilities
<!-- 无 -->

## Impact

- 新增项目: `data_collector/DataCollector/`
- 新建目录: `configs/collect/`
- 移动: `semantic_engine/.../rules/` → `configs/semantic/`
- 依赖: attribute_module, event_system, semantic_engine, Newtonsoft.Json
