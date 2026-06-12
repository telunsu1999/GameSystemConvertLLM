## Context

属性模块是纯存储层。无计算、无序列化、无业务逻辑。任何实体挂载一个 AttributeModule 实例即可获得属性系统。

## Goals / Non-Goals

**Goals:**
- 属性值带类型声明（number/string/bool）
- tag 反向索引，GetByTags O(1)
- 类型强制校验
- LoadFrom 合并策略（同名覆盖，独有保留）
- 不存在返回 null

**Non-Goals:**
- 不涉及属性计算、公式、推导
- 不涉及序列化（外部负责）
- 不涉及临时加成/Modifier（外部直接改值）
- 不涉及事件通知

## Decisions

### D1: tag 反向索引

维护 `Dictionary<string, HashSet<string>>`（tag → 属性名集合）。
Set/SetTags/Remove 时同步更新索引。GetByTags 直接查索引，O(1)。

### D2: 类型校验

```
"number": value must be int/long/float/double
"string": value must be string
"bool":   value must be bool
```

类型不匹配抛出 ArgumentException。

### D3: LoadFrom 合并

```csharp
// 已有: currentHP=85, strength=12
// 导入: currentHP=50, gold=100
// 结果: currentHP=50 (覆盖), strength=12 (保留), gold=100 (新增)
```

### D4: AttributeValue 不可变

value/type/tags 通过 Set 整体替换，不暴露内部可变引用。

## Risks / Trade-offs

- [Risk] tag 索引与属性数据不同步 → Mitigation: Set/SetTags/Remove 三方法里集中维护索引
- [Risk] 属性数量上千时 HashSet 内存开销 → 当前场景几十个属性，不构成问题
