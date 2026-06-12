## ADDED Requirements

### Requirement: Tick-based time advancement
The system SHALL maintain a single `Tick` counter as the sole source of truth for game time.
Time advancement SHALL be passive: external code calls `Advance(n)` to move forward n ticks.

#### Scenario: Advance single tick
- **WHEN** `Advance(1)` is called at tick 0
- **THEN** `Tick` becomes 1

#### Scenario: Advance multiple ticks
- **WHEN** `Advance(10)` is called at tick 50
- **THEN** `Tick` becomes 60

#### Scenario: Tick is readonly externally
- **WHEN** code attempts to set `Tick` property
- **THEN** property is get-only, no setter exposed

### Requirement: Derived time properties
The system SHALL compute all time properties from `Tick` and configuration values.
Properties SHALL include: Minute, Hour, Day, Season, Year, TimeBlock.

#### Scenario: Hour at tick 0
- **WHEN** config has minutes_per_tick=10 and tick is 0
- **THEN** `Hour` is 0, `Minute` is 0

#### Scenario: Hour after 6 ticks
- **WHEN** config has minutes_per_tick=10 and tick is 6
- **THEN** `Hour` is 1, `Minute` is 0

#### Scenario: Day wraps after 144 ticks
- **WHEN** config has minutes_per_tick=10, tick is 144
- **THEN** `Day` is 2, `Hour` is 0

#### Scenario: Season wraps after configured days
- **WHEN** config has days_per_season=30, tick represents day 31
- **THEN** Season changes from Spring(0) to Summer(1), Day resets to 1

#### Scenario: Year increments after 4 seasons
- **WHEN** all 4 seasons complete
- **THEN** `Year` increments by 1

### Requirement: JSON configuration loading
The system SHALL load clock configuration from a JSON file via `FromConfig(jsonPath)`.

#### Scenario: Load valid config
- **WHEN** `FromConfig("configs/clock/world_clock.json")` is called
- **THEN** a WorldClock instance is returned with configured minutes_per_tick, days_per_season, seasons, and time_blocks

#### Scenario: Load missing config file
- **WHEN** `FromConfig("nonexistent.json")` is called
- **THEN** an exception is thrown

#### Scenario: Config with invalid time_blocks coverage
- **WHEN** time_blocks have gaps (e.g., hour 6-8 and 10-12 but not 9)
- **THEN** construction fails with an exception

### Requirement: TimeBlock derivation
The system SHALL derive current `TimeBlock` from the Hour property and configured time_blocks.

#### Scenario: Hour falls in morning block
- **WHEN** config has time_block "Morning" for hours 6-8 and current Hour is 7
- **THEN** `TimeBlock` returns "Morning"

#### Scenario: Hour at boundary start
- **WHEN** time_block "Afternoon" starts at hour 14 and current Hour is 14
- **THEN** `TimeBlock` returns "Afternoon"

#### Scenario: Hour at boundary end
- **WHEN** time_block "Afternoon" ends at hour 17 and current Hour is 17
- **THEN** `TimeBlock` returns "Afternoon"

### Requirement: Event callbacks on time boundaries
The system SHALL fire event callbacks when time crosses specific boundaries during Advance().
Events SHALL fire in order from fine-grained to coarse-grained (TimeBlock → Hour → Day → Season → Year).

#### Scenario: No boundary crossed
- **WHEN** `Advance(3)` ticks within the same hour and same time_block
- **THEN** `OnTick` fires 3 times, no other events fire

#### Scenario: Hour boundary crossed
- **WHEN** `Advance` crosses an hour boundary
- **THEN** `OnHourChanged` fires with snapshot of new time

#### Scenario: Day boundary crossed
- **WHEN** `Advance` crosses midnight (Hour 23 → 0)
- **THEN** `OnDayChanged` fires after `OnHourChanged`

#### Scenario: Multiple boundaries in one Advance
- **WHEN** `Advance(200)` crosses hour, day, and season boundaries
- **THEN** events fire in order: OnHourChanged, OnDayChanged, OnSeasonChanged

### Requirement: TickSnapshot for event payload
The system SHALL provide a `TickSnapshot` struct containing the full time state at the moment of an event.

#### Scenario: Snapshot at specific tick
- **WHEN** `Snapshot()` is called at tick 100
- **THEN** returns TickSnapshot with Tick=100 and all derived properties matching that tick

#### Scenario: Event carries correct snapshot
- **WHEN** `OnDayChanged` fires
- **THEN** the TickSnapshot parameter reflects the new day's time state, not the old one
