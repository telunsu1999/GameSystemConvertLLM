# Adaptive Plan Complexity — Architecture

## Complexity Score Pipeline

```
┌─────────────────────────────────────────────────────────┐
│                    NPC Entity (per tick)                  │
│  Attributes │ GoalComponent │ EventSystem │ ActionResolver│
└────────┬──────────┬──────────┬──────────┬───────────────┘
         │          │          │          │
         ▼          ▼          ▼          ▼
┌─────────────────────────────────────────────────────────┐
│              EvaluateComplexity()                         │
│                                                          │
│  goal_count_score      × 0.30  (目标数量 / 最大)         │
│  goal_dependency_score × 0.20  (有交叉依赖?社交目标?)     │
│  semantic_richness     × 0.15  (语义数据条数)            │
│  event_urgency         × 0.10  (近期战斗/社交事件)        │
│  state_critical        × 0.10  (HP<30%? SP<20%? 醉酒?)   │
│  action_diversity      × 0.10  (可用 action ≥ 3 类?)     │
│  time_since_last_plan  × 0.05  (距上次 > 冷却?)          │
│                                                          │
│  ─────────────────────────────────────                   │
│  complexity_score: 0.0 ───────────────────── 1.0         │
└────────────────────────┬────────────────────────────────┘
                         │
         ┌───────────────┼───────────────┐
         ▼               ▼               ▼
    score < 0.3    0.3 ≤ score < 0.6    score ≥ 0.6
         │               │               │
    ┌────┴────┐    ┌─────┴─────┐    ┌────┴────┐
    │ Simple  │    │ Standard  │    │ Complex │
    └────┬────┘    └─────┬─────┘    └────┬────┘
         │               │               │
    ┌────┴──────────┐ ┌──┴──────────┐ ┌──┴──────────┐
    │ tokens: 128   │ │ tokens: 512 │ │ tokens: 1024│
    │ thinking: off │ │ think: off  │ │ think: opt  │
    │ tools: no     │ │ tools: yes  │ │ tools: req  │
    │ actions: 3    │ │ actions: 8  │ │ actions: all│
    │ cooldown:12t  │ │ cooldown:6t │ │ cooldown:3t │
    └───────────────┘ └─────────────┘ └─────────────┘
```

## Classification Example

```
铁卫 (简单NPC):                布洛克 (复杂NPC):
  goals=2 (巡逻+保护)             goals=4 (结婚+赚钱+工作+社交)
  events=0                       events=2 (有社交+旅行信号)
  semantic=2                     semantic=5
  HP=150, SP=80                  HP=100, SP=80
  actions=6                       actions=15
  ─────────────                  ─────────────
  score=0.25 → Simple            score=0.78 → Complex
  prompt: 文本,128 tokens         prompt: system+user+tools,1024 tokens
  output: patrol                  output: morning→work→talk→evening
```

## Fallback Chain

```
ComplexPlan 解析失败
  → 降级为 StandardPlan 重试
    → 降级为 SimplePlan 重试
      → 规则引擎 fallback (default action)
```
