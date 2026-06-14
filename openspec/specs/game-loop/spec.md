# Game Loop & World Clock

## Purpose

The game loop drives the simulation by advancing a world clock tick by tick, iterating all entities and their components, processing scheduled actions, and broadcasting state to connected WebSocket clients.

## Requirements

### Requirement: Tick-Based Game Loop

The system SHALL advance game time in discrete ticks, where each tick drives all entities and processes their scheduled actions.

#### Scenario: Single tick advancement

- **GIVEN** a `GameLoop` with entities and a `WorldClock`
- **WHEN** `AdvanceTicks(1, callback)` is called
- **THEN** the clock advances by 1 tick
- **AND** each entity's `OnTick(ctx)` is called
- **AND** each entity's `Scheduler.Check()` is invoked
- **AND** due actions are executed via `ActionResolver`
- **AND** the callback receives the updated `StateSnapshot`

#### Scenario: Multi-tick batch

- **GIVEN** `AdvanceTicks(144, callback)` (one game day)
- **WHEN** the batch completes
- **THEN** the clock advances 144 ticks
- **AND** the callback fires once with the final state

### Requirement: World Clock

The system SHALL model game time with configurable ticks-per-minute, days-per-season, seasons, and time blocks (morning/afternoon/evening/night).

#### Scenario: Clock advances

- **GIVEN** a clock at Tick 0 with `minutes_per_tick: 10`
- **WHEN** `Advance(6)` is called
- **THEN** Tick becomes 6, Minute becomes 0, Hour becomes 1

#### Scenario: Time block transitions

- **GIVEN** a clock configured with time_blocks including `{name:"night", start_hour:20, end_hour:6}`
- **WHEN** the hour transitions from 19 to 20
- **THEN** `OnTimeBlockChanged` event fires with the new block "night"

#### Scenario: Season transitions

- **GIVEN** `days_per_season: 30` and 4 seasons
- **WHEN** enough ticks pass to cross season boundary
- **THEN** `OnSeasonChanged` event fires

### Requirement: Entity Lifecycle Management

The system SHALL support adding entities at runtime, loading them from JSON directories, and wiring their components to the game loop.

#### Scenario: Load entities from directory

- **GIVEN** a directory with valid entity JSON files
- **WHEN** `LoadEntities(entityDir, rootDir, llmClient)` is called
- **THEN** each file is parsed and a `GameEntity` is created
- **AND** each entity gets an `LlmPlanner` component
- **AND** goal progress is wired to `OnGoalProgressChanged`

#### Scenario: Add entity manually

- **GIVEN** a pre-built `GameEntity`
- **WHEN** `AddEntity(entity)` is called
- **THEN** the entity is added to the entities dictionary
- **AND** `MapSystem` is wired to the entity's `ActionResolver`
- **AND** entity position is registered in `MapSystem`
- **AND** attribute change events are wired to `OnNpcAttrChanged`

### Requirement: State Broadcasting

The system SHALL build a serializable `StateSnapshot` containing all NPC states and broadcast it to WebSocket clients after each tick batch.

#### Scenario: Build state snapshot

- **GIVEN** 3 entities with various attributes and goals
- **WHEN** `BuildState()` is called
- **THEN** the snapshot includes tick, year, month, day, hour, minute, season, timeBlock
- **AND** each NPC's attributes, goals, upcoming schedule, and current action are included

#### Scenario: Map data broadcast

- **GIVEN** a loaded `MapSystem`
- **WHEN** `BuildMapData()` is called
- **THEN** the result includes grid terrain data, terrain definitions with colors, POI nodes, and entity positions

### Requirement: Auto-Tick Mode

The system SHALL support an automatic tick loop with configurable interval and optional blocking when any NPC is waiting for LLM response.

#### Scenario: Start auto-tick

- **GIVEN** `StartAutoTick(2000, callback)` called
- **WHEN** 2 seconds elapse
- **THEN** one tick is advanced
- **AND** the callback fires with updated state
- **AND** this repeats every 2 seconds

#### Scenario: Block on LLM thinking

- **GIVEN** `BlockOnLlmThinking = true` and an NPC with `llm_thinking=1`
- **WHEN** the auto-tick timer fires
- **THEN** the tick is SKIPPED (no advancement)
