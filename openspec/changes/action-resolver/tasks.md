## 1. Project Setup

- [ ] 1.1 Create `src/ActionResolver/ActionResolver.csproj` (netstandard2.1)
- [ ] 1.2 Add references to AttributeEngine, EventSystem
- [ ] 1.3 Create `tests/ActionResolver.Tests/` console test project

## 2. Core Models

- [ ] 2.1 Define `IAction` interface (CanExecute / Execute / OnInterrupt / Interruptible / ActionType)
- [ ] 2.2 Define `ActionContext` struct (attrs, events, scheduler, snap)
- [ ] 2.3 Define `CurrentAction` class for state exposure
- [ ] 2.4 Define `ActionItem` class (ActionType + Params)
- [ ] 2.5 Define JSON models: ActionMapping, ActionStep, ActionChain, ParamSchema

## 3. GenericAction (JSON-driven)

- [ ] 3.1 Implement `GenericAction` — reads ActionMapping from JSON, executes steps
- [ ] 3.2 Support step types: set_attr, sub_attr, add_attr, record_event, schedule, notify
- [ ] 3.3 Support template substitution "{params.x}" and "{attr.x}"
- [ ] 3.4 Support `check` (preconditions) and `on_condition` (conditional chain)

## 4. Built-in C# Actions

- [ ] 4.1 Implement `GotoAction` — set moving=1 + record depart event + yield ArriveAction
- [ ] 4.2 Implement `ArriveAction` — set location + moving=0 + record arrive event
- [ ] 4.3 Implement `TakeDamageAction` — sub_attr hp + on_condition hp<=0 → death chain

## 5. ActionResolver Core

- [ ] 5.1 Implement `LoadMapping(jsonPath)` — load JSON → GenericAction
- [ ] 5.2 Implement `Register(IAction)` — register C# Action (overrides JSON)
- [ ] 5.3 Implement `Execute(ActionItem, ctx)` — dispatch to registered/Generic action
- [ ] 5.4 Implement `Interrupt(ctx)` — interrupt current action
- [ ] 5.5 Implement `Current` property — expose executing action state

## 6. Config & Tests

- [ ] 6.1 Create `configs/actions/guard.json` sample config
- [ ] 6.2 Test GenericAction step execution
- [ ] 6.3 Test template substitution
- [ ] 6.4 Test GotoAction → ArriveAction chain
- [ ] 6.5 Test interrupt
- [ ] 6.6 Test Current state
- [ ] 6.7 Test JSON LoadMapping
