# Entity System

## Purpose

The entity system provides a component-based architecture where `GameEntity` acts as a container for pluggable `IEntityComponent` instances. Entities are defined in JSON config and assembled at runtime by `EntityFactory`. This enables data-driven NPC creation without code changes.

## Requirements

### Requirement: Component-Based Entity Model

The system SHALL use a component-based architecture where `GameEntity` is a container holding zero or more `IEntityComponent` instances, each providing a distinct behavior domain.

#### Scenario: Create entity from JSON

- **GIVEN** a valid JSON entity config file at `configs/entities/<id>.json`
- **WHEN** `EntityFactory.CreateFromJson(configPath, rootDir)` is called
- **THEN** a `GameEntity` is returned with all specified components initialized:
  - `Attributes` with key-value pairs from `attrs[]`
  - `Scheduler` with schedule loaded from `schedule` path
  - `TriggerSystem` with rules loaded from `triggers` path
  - `ActionResolver` with built-in actions registered and JSON mappings loaded
  - `RecordModule`, `RelationModule`, `GoalComponent` as applicable

#### Scenario: Add component at runtime

- **GIVEN** an existing `GameEntity`
- **WHEN** `entity.Add<T>(component)` is called
- **THEN** the component is appended to the component list and indexed by type
- **AND** `entity.Get<T>()` returns the component
- **AND** `entity.Has<T>()` returns true

#### Scenario: Tick all components

- **GIVEN** a `GameEntity` with multiple components
- **WHEN** `entity.OnTick(ctx)` is called
- **THEN** every component's `OnTick(ctx)` is invoked in registration order
- **AND** `ctx.Entity` is set to the owning entity

### Requirement: Attribute Key-Value Store

The system SHALL provide an `Attributes` component that stores typed key-value pairs with tag-based categorization.

#### Scenario: Set and get attribute

- **GIVEN** an entity with `Attributes`
- **WHEN** `attrs.Set("hp", 150, "number", ["vital", "hp"])` is called
- **THEN** `attrs.Get("hp")` returns `{Value: 150, Tags: ["vital", "hp"]}`

#### Scenario: Query by tags

- **GIVEN** attributes with various tags
- **WHEN** `attrs.GetByTags(["vital"])` is called
- **THEN** all attributes tagged `vital` are returned

#### Scenario: Attribute change event

- **GIVEN** a subscribed `OnChanged` handler
- **WHEN** an attribute value is modified via `Set()`
- **THEN** the handler fires with (key, newValue)
- **AND** `GameLoop.OnNpcAttrChanged` propagates (entityId, key, value) to WebSocket

### Requirement: JSON-Driven Entity Configuration

The system SHALL define entities entirely through JSON configuration files with external references for schedules, triggers, actions, goals, and relations.

#### Scenario: Load entity with all subsystems

- **GIVEN** `configs/entities/guard.json` referencing schedule, triggers, actions, goals, relations files
- **WHEN** entity is loaded
- **THEN** all referenced subsystems are initialized
- **AND** missing optional files (goals, relations) do not cause errors

#### Scenario: Location attribute matches map nodes

- **GIVEN** a loaded map with defined POI nodes
- **WHEN** an entity's `location` attribute is set
- **THEN** the value MUST match one of the map's `poi_nodes[].id` values
- **AND** `MapSystem.RegisterEntity()` is called during `GameLoop.AddEntity()`
