## Context

DataCollector 是属性模块、事件系统、语义引擎三者的聚合层。不写业务逻辑，纯粹的"收集→组装→喂给引擎"。

## Goals / Non-Goals

**Goals:**
- 属性按标签收集，展平为 dict
- 事件按标签+时间+NPC收集，展平为 memory_events 格式
- 支持配置文件驱动和接口直调
- 返回原始数据和语义化文本

**Non-Goals:**
- 不做字段名翻译
- 不做业务决策
- 不调用LLM

## Decisions

### D1: 事件展平策略

事件的 Data dict + Perception 信息合并为一条 memory_event 记录。Perception.How → source 字段。

### D2: 配置文件统一

`configs/semantic/` — 语义规则
`configs/collect/` — 收集配置

### D3: 两个接口分离

Collect() 返回 CollectResult(含语义化文本)。
CollectRaw() 只返回 dict，外部可自行处理。
