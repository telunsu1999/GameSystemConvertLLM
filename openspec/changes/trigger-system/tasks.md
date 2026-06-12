## 1. Project Setup

- [ ] 1.1 Create `src/TriggerSystem/TriggerSystem.csproj` (netstandard2.1, zero dependencies)
- [ ] 1.2 Create `tests/TriggerSystem.Tests/` console test project
- [ ] 1.3 Create `configs/triggers/guard.json` sample config

## 2. Core Models

- [ ] 2.1 Implement `Condition` class (Field, Op, Value)
- [ ] 2.2 Implement `TriggerRule` class (Mode, Conditions, ActionType, Params, Description, Tags)
- [ ] 2.3 Implement `TriggerResult` class (DirectActions, LlmOptions)
- [ ] 2.4 Implement `TriggerConfig` JSON model for file loading

## 3. TriggerSystem Core

- [ ] 3.1 Implement `LoadRules(jsonPath)` — load from JSON file
- [ ] 3.2 Implement `Evaluate(changeType, changeKey, snap, attrs, events)` — passive evaluation
- [ ] 3.3 Implement condition evaluation: eq / ne / gt / gte / lt / lte for numbers and strings
- [ ] 3.4 Classify matched rules into DirectActions vs LlmOptions based on mode

## 4. Sample Configs

- [ ] 4.1 Create `configs/triggers/guard.json` (combat + social triggers)
- [ ] 4.2 Create `configs/triggers/merchant.json` (trade + social triggers)

## 5. Tests

- [ ] 5.1 Test LoadRules from valid config
- [ ] 5.2 Test Evaluate with matching conditions
- [ ] 5.3 Test Evaluate with non-matching conditions
- [ ] 5.4 Test all condition operators (eq, ne, gt, gte, lt, lte)
- [ ] 5.5 Test string value conditions
- [ ] 5.6 Test DirectActions vs LlmOptions classification
- [ ] 5.7 Test Evaluate statelessness (same input → same output)
- [ ] 5.8 Test Tags accessibility on returned rules
