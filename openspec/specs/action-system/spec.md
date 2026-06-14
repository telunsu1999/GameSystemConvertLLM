# Action System

## Purpose

The action system provides both one-shot and continuous (multi-tick) action execution. Actions can be defined in C# (for performance-critical stateful behavior like movement) or in JSON (for configurable stateless behavior). The `ActionResolver` manages action lifecycle including execution, interruption, and per-tick advancement.

## Requirements

### Requirement: One-Shot Action Execution

The system SHALL support actions that execute atomically within a single tick and may produce child actions scheduled for future ticks.

#### Scenario: Execute JSON-defined action

- **GIVEN** a JSON action definition with `steps[]`
- **WHEN** `ActionResolver.Execute(actionItem, attrs, scheduler, snap)` is called
- **THEN** each step in `steps[]` is executed in order
- **AND** child actions returned by `Execute()` are added to `Scheduler`
- **AND** if no children, `_current` is cleared (action complete)

#### Scenario: Action blocked by check condition

- **GIVEN** an action with `check` conditions
- **WHEN** `CanExecute(ctx)` returns false
- **THEN** the action is NOT executed
- **AND** `ExecuteResult.Completed` is false

#### Scenario: On-condition chain

- **GIVEN** an action with `on_condition` defined
- **WHEN** the main steps complete AND `on_condition.condition` evaluates true
- **THEN** the `on_condition.steps` are executed as a chain

### Requirement: Continuous Action Execution

The system SHALL support `IContinuousAction` — actions that persist across multiple ticks, receiving `OnTick` callbacks each tick until `IsCompleted` is true.

#### Scenario: Movement as continuous action

- **GIVEN** a `MoveToAction` targeting a reachable location
- **WHEN** `Execute()` is called
- **THEN** path is computed via `MapSystem.FindPathOnGraph()`
- **AND** initial state is set (moving=1, destination, travel_progress="0/N")
- **AND** `_current` remains set (action stays alive)
- **AND** no child actions are produced

#### Scenario: Per-tick progress update

- **GIVEN** a `MoveToAction` in progress with remaining path
- **WHEN** `OnTick()` is called each game tick
- **THEN** `travel_progress` is updated each tick
- **AND** `_ticksToNextNode` decrements
- **AND** when a node is reached, `location` is updated and position is re-registered in `MapSystem`

#### Scenario: Continuous action completion

- **GIVEN** a `MoveToAction` reaching its final destination
- **WHEN** the last `OnTick()` completes
- **THEN** `IsCompleted` is set to true
- **AND** `ActionResolver` clears `_current`
- **AND** final state is set (moving=0, travel_progress="N/N")

#### Scenario: Interrupt continuous action

- **GIVEN** a `MoveToAction` in progress (interruptible=true)
- **WHEN** `ActionResolver.Interrupt()` is called
- **THEN** `OnInterrupt()` is invoked
- **AND** state is cleaned up (moving=0)
- **AND** `IsCompleted` is set to true

### Requirement: Dual Action Definition (C# + JSON)

The system SHALL support both C# built-in actions (registered via `ActionResolver.Register()`) and JSON-defined actions (loaded via `ActionResolver.LoadMapping()`).

#### Scenario: Built-in action resolution

- **GIVEN** `MoveToAction` registered with actionType "move_to"
- **WHEN** `Resolve("move_to")` is called
- **THEN** the C# `MoveToAction` instance is returned (priority over JSON)

#### Scenario: JSON action resolution

- **GIVEN** an action defined in JSON with actionType "eat"
- **WHEN** `Resolve("eat")` is called and no C# action matches
- **THEN** a `GenericAction` wrapping the JSON `ActionMapping` is returned

### Requirement: Interruption Support

The system SHALL support interrupting the current action, with configurable cleanup steps defined per action.

#### Scenario: Interrupt with cleanup

- **GIVEN** an action with `on_interrupt` steps defined
- **WHEN** `ActionResolver.Interrupt()` is called while the action is current
- **THEN** the `on_interrupt` steps are executed
- **AND** `_current` is cleared
